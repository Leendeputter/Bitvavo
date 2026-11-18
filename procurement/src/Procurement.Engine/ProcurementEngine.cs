using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.BusinessLogic;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Interfaces;
using Procurement.Core.Models;
using Procurement.Data.Repositories;

namespace Procurement.Engine
{
    public enum LineOutcome
    {
        /// <summary>Line has a SupplierSelection — sourced and either auto-approved or already handled. Placing the actual order is a separate, later step (ProcurementEngine.PlaceOrdersAsync) — spec correction, aug 2026.</summary>
        Selected,
        PendingApproval,
        Exception
    }

    /// <summary>
    /// Orchestrates the fase 1 workflow from spec §6: Purchase Request → Sourcing → Selectie →
    /// ERP PO → Supplier Order → Confirmation. Never depends on a concrete supplier — every
    /// supplier-specific call goes through <see cref="ISupplierAdapter"/>.
    /// </summary>
    public class ProcurementEngine
    {
        private readonly IErpConnector _erp;
        private readonly List<ISupplierAdapter> _adapters;
        private readonly IAuditLogger _auditLogger;
        private readonly PurchaseRequestRepository _purchaseRequestRepository;
        private readonly SupplierProductMappingRepository _mappingRepository;
        private readonly SupplierOfferRepository _offerRepository;
        private readonly SupplierSelectionRepository _selectionRepository;
        private readonly ApprovalRequestRepository _approvalRepository;
        private readonly PurchaseOrderRepository _purchaseOrderRepository;
        private readonly PolicyRepository _policyRepository;

        /// <summary>Lines sourced as part of an article group during the current SourcePurchaseRequestAsync call.</summary>
        private readonly Dictionary<int, LineOutcome> _groupedThisRun = new Dictionary<int, LineOutcome>();

        public ProcurementEngine(
            IErpConnector erp,
            IEnumerable<ISupplierAdapter> adapters,
            IAuditLogger auditLogger,
            PurchaseRequestRepository purchaseRequestRepository,
            SupplierProductMappingRepository mappingRepository,
            SupplierOfferRepository offerRepository,
            SupplierSelectionRepository selectionRepository,
            ApprovalRequestRepository approvalRepository,
            PurchaseOrderRepository purchaseOrderRepository,
            PolicyRepository policyRepository)
        {
            _erp = erp ?? throw new ArgumentNullException(nameof(erp));
            _adapters = adapters?.ToList() ?? new List<ISupplierAdapter>();
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _purchaseRequestRepository = purchaseRequestRepository ?? throw new ArgumentNullException(nameof(purchaseRequestRepository));
            _mappingRepository = mappingRepository ?? throw new ArgumentNullException(nameof(mappingRepository));
            _offerRepository = offerRepository ?? throw new ArgumentNullException(nameof(offerRepository));
            _selectionRepository = selectionRepository ?? throw new ArgumentNullException(nameof(selectionRepository));
            _approvalRepository = approvalRepository ?? throw new ArgumentNullException(nameof(approvalRepository));
            _purchaseOrderRepository = purchaseOrderRepository ?? throw new ArgumentNullException(nameof(purchaseOrderRepository));
            _policyRepository = policyRepository ?? throw new ArgumentNullException(nameof(policyRepository));
        }

        public Task<IReadOnlyList<PurchaseRequest>> GetOpenPurchaseRequestsAsync() => _erp.GetOpenPurchaseRequestsAsync();

        public Task<IReadOnlyList<SupplierOffer>> GetOffersForLineAsync(int purchaseRequestLineId) =>
            _offerRepository.GetByLineIdAsync(purchaseRequestLineId);

        public Task<IReadOnlyList<ApprovalRequest>> GetPendingApprovalsAsync() => _approvalRepository.GetPendingAsync();

        public Task<SupplierSelection> GetSelectionForLineAsync(int purchaseRequestLineId) =>
            _selectionRepository.GetByLineIdAsync(purchaseRequestLineId);

        /// <summary>Spec §6 steps 1-11 for every line of one purchase request. Stops a line at the approval gate (step 8) when the policy requires a human.</summary>
        public async Task SourcePurchaseRequestAsync(int purchaseRequestId)
        {
            var request = await _purchaseRequestRepository.GetByIdAsync(purchaseRequestId);
            if (request == null)
                throw new InvalidOperationException($"Purchase request {purchaseRequestId} not found.");

            ValidateRequest(request);

            await _purchaseRequestRepository.UpdateStatusAsync(request.Id, PurchaseRequestStatus.Sourcing);
            await _auditLogger.LogAsync("PurchaseRequest", request.Id.ToString(), "SOURCING_STARTED", "Success");

            var activeAdapters = await GetActiveAdaptersAsync();
            var approvalPolicy = await _policyRepository.GetApprovalPolicyAsync()
                ?? new ApprovalPolicy { MaxOrderValueForAutoApproval = 0, MaxPriceVariancePercentage = 0 };

            var anyPendingApproval = false;
            var anyException = false;
            _groupedThisRun.Clear();

            foreach (var line in request.Lines)
            {
                // Already handled as part of another line's article group in this run (18 nov 2026).
                var outcome = _groupedThisRun.TryGetValue(line.Id, out var groupedOutcome)
                    ? groupedOutcome
                    : await SourceAndProcessLineAsync(line, activeAdapters, approvalPolicy);
                if (outcome == LineOutcome.PendingApproval) anyPendingApproval = true;
                if (outcome == LineOutcome.Exception) anyException = true;
            }

            // Sourcing only gets a line as far as a SupplierSelection (spec correction, aug 2026:
            // ordering is a separate, explicit step — see PlaceOrdersAsync) — so the "everything
            // went fine" outcome here is ReadyToOrder, not Ordered.
            var finalStatus =
                anyPendingApproval ? PurchaseRequestStatus.WaitingApproval :
                anyException ? PurchaseRequestStatus.Exception :
                PurchaseRequestStatus.ReadyToOrder;

            await _purchaseRequestRepository.UpdateStatusAsync(request.Id, finalStatus);
            await _auditLogger.LogAsync("PurchaseRequest", request.Id.ToString(), "SOURCING_COMPLETED", finalStatus.ToString());
        }

        /// <summary>
        /// Spec §8.2: user picks a different offer than the engine's proposal. A reason used to be
        /// required here (part of the audit trail) — dropped (user request, 20 okt 2026) once offers
        /// became directly selectable via a checkbox in MainForm's grid instead of only through the
        /// old OfferComparisonForm dialog's reason box, for a lower-friction pick. ReasonSummary still
        /// records who made the choice, just without a mandatory free-text justification attached.
        /// </summary>
        public async Task SelectOfferManuallyAsync(int purchaseRequestLineId, int selectedOfferId, string reason, string user)
        {
            var offer = await _offerRepository.GetByIdAsync(selectedOfferId);
            if (offer == null || offer.PurchaseRequestLineId != purchaseRequestLineId)
                throw new InvalidOperationException("Offer does not belong to the given purchase request line.");

            var reasonSummary = string.IsNullOrWhiteSpace(reason)
                ? $"Handmatig gekozen door {user}"
                : $"Handmatig gekozen door {user}: {reason}";

            await SelectOnLineAsync(purchaseRequestLineId, offer, reasonSummary, user, replaceExisting: false);

            // Sourcing per article (18 nov 2026): the same choice on every other line of the bundle
            // that isn't ordered yet and hasn't chosen outside the bundle.
            foreach (var sibling in await GetBundleSiblingsAsync(offer))
            {
                var existing = await _selectionRepository.GetByLineIdAsync(sibling.PurchaseRequestLineId);
                if (existing != null && existing.SelectedOffer?.BundleKey != offer.BundleKey) continue;
                await SelectOnLineAsync(sibling.PurchaseRequestLineId, sibling, reasonSummary + " (via bundel)", user, replaceExisting: true);
            }
        }

        private async Task SelectOnLineAsync(int purchaseRequestLineId, SupplierOffer offer, string reasonSummary, string user, bool replaceExisting)
        {
            var line = await GetLineAsync(purchaseRequestLineId);
            if (replaceExisting)
                await _selectionRepository.DeleteByLineIdAsync(purchaseRequestLineId);

            // A manual choice replaces a pending proposal.
            await _approvalRepository.DeletePendingForLineAsync(purchaseRequestLineId);

            var selection = new SupplierSelection
            {
                PurchaseRequestLineId = purchaseRequestLineId,
                SelectedOfferId = offer.Id,
                ReasonSummary = reasonSummary,
                Mode = SelectionMode.Manual
            };
            await _selectionRepository.AddAsync(selection);
            await _auditLogger.LogAsync("PurchaseRequestLine", purchaseRequestLineId.ToString(), "SUPPLIER_SELECTED_MANUAL", "Success",
                offer.SupplierCode, responsePayload: selection.ReasonSummary, userOrSystem: user);

            await RecomputeRequestStatusIfFullySelectedAsync(line.PurchaseRequestId);
        }

        /// <summary>The same offer (VariantKey) on the other lines of its bundle, for lines not on a PO yet. Empty when not bundled.</summary>
        private async Task<List<SupplierOffer>> GetBundleSiblingsAsync(SupplierOffer offer)
        {
            var result = new List<SupplierOffer>();
            if (string.IsNullOrEmpty(offer?.BundleKey)) return result;

            foreach (var sibling in await _offerRepository.GetByBundleKeyAsync(offer.BundleKey))
            {
                if (sibling.PurchaseRequestLineId == offer.PurchaseRequestLineId || sibling.VariantKey != offer.VariantKey) continue;
                if (await _purchaseOrderRepository.HasOrderForLineAsync(sibling.PurchaseRequestLineId)) continue;
                result.Add(sibling);
            }
            return result;
        }

        /// <summary>
        /// Reverts a line back to "no offer selected" — the inverse of SelectOfferManuallyAsync (user
        /// request, 20 okt 2026: unticking "Select" in MainForm's offers grid should actually clear
        /// the choice, not just revert itself back to checked on the next refresh). SupplierSelection
        /// rows are deleted outright rather than kept as inactive history — there's no "no offer"
        /// representation for SelectedOfferId (a required int), and the audit log (a separate,
        /// append-only table) already records SUPPLIER_SELECTED_MANUAL/_AUTO regardless, so nothing
        /// about who chose what when is actually lost. Workflow status moves back to Sourcing, since
        /// RecomputeRequestStatusIfFullySelectedAsync only reaches ReadyToOrder once every line has a
        /// selection again. Refused once the request is already Ordered — a real ERP PO exists for it
        /// by then, so clearing the selection here wouldn't undo that anyway.
        /// </summary>
        public async Task DeselectOfferAsync(int purchaseRequestLineId, string user)
        {
            var line = await GetLineAsync(purchaseRequestLineId);
            var request = await _purchaseRequestRepository.GetByIdAsync(line.PurchaseRequestId);
            if (request != null && request.Status == PurchaseRequestStatus.Ordered)
                throw new InvalidOperationException("Deze aanvraag is al besteld — de offertekeuze kan niet meer ongedaan gemaakt worden.");

            var current = await _selectionRepository.GetByLineIdAsync(purchaseRequestLineId);
            await ClearSelectionAsync(line, user);

            // Bundle (18 nov 2026): unticking one line clears the bundle's choice on its other lines too.
            if (current?.SelectedOffer != null)
            {
                foreach (var sibling in await GetBundleSiblingsAsync(current.SelectedOffer))
                {
                    var siblingSelection = await _selectionRepository.GetByLineIdAsync(sibling.PurchaseRequestLineId);
                    if (siblingSelection?.SelectedOffer?.BundleKey != current.SelectedOffer.BundleKey) continue;
                    await ClearSelectionAsync(await GetLineAsync(sibling.PurchaseRequestLineId), user);
                }
            }
        }

        private async Task ClearSelectionAsync(PurchaseRequestLine line, string user)
        {
            await _selectionRepository.DeleteByLineIdAsync(line.Id);
            await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "SUPPLIER_SELECTION_CLEARED", "Success", userOrSystem: user);

            await _purchaseRequestRepository.UpdateStatusAsync(line.PurchaseRequestId, PurchaseRequestStatus.Sourcing);
            await _auditLogger.LogAsync("PurchaseRequest", line.PurchaseRequestId.ToString(), "SELECTION_CLEARED_BACK_TO_SOURCING", "Success", userOrSystem: user);
        }

        /// <summary>Spec §8.3: decide a line that failed the auto-approve criteria.</summary>
        public async Task ApproveAsync(int approvalRequestId, bool approve, string comment, string user)
        {
            var allPending = await _approvalRepository.GetPendingAsync();
            var pending = allPending.FirstOrDefault(a => a.Id == approvalRequestId);
            if (pending == null)
                throw new InvalidOperationException($"Approval request {approvalRequestId} not found or already decided.");

            // Sourcing per article (18 nov 2026): deciding one line of a bundle decides the bundle.
            var toDecide = new List<ApprovalRequest> { pending };
            var bundleKey = pending.ProposedOffer?.BundleKey;
            if (!string.IsNullOrEmpty(bundleKey))
                toDecide.AddRange(allPending.Where(a => a.Id != pending.Id && a.ProposedOffer?.BundleKey == bundleKey));

            foreach (var request in toDecide)
            {
                var requestComment = request.Id == pending.Id ? comment : $"{comment} (via bundel, verzoek {pending.Id})";
                await _approvalRepository.DecideAsync(
                    request.Id,
                    approve ? ApprovalDecision.Approved : ApprovalDecision.Rejected,
                    requestComment,
                    user);

                await _auditLogger.LogAsync("ApprovalRequest", request.Id.ToString(),
                    approve ? "APPROVAL_GRANTED" : "APPROVAL_REJECTED", "Success",
                    responsePayload: requestComment, userOrSystem: user);

                if (!approve) continue;

                var offer = await _offerRepository.GetByIdAsync(request.ProposedOfferId);
                var line = await GetLineAsync(request.PurchaseRequestLineId);

                var selection = new SupplierSelection
                {
                    PurchaseRequestLineId = request.PurchaseRequestLineId,
                    SelectedOfferId = offer.Id,
                    ReasonSummary = $"Handmatig goedgekeurd door {user}. Reden(en) voor goedkeuring: {request.Reasons}. Commentaar: {requestComment}",
                    Mode = SelectionMode.Manual
                };
                await _selectionRepository.AddAsync(selection);

                await RecomputeRequestStatusIfFullySelectedAsync(line.PurchaseRequestId);
            }
        }

        private static void ValidateRequest(PurchaseRequest request)
        {
            if (request.Lines == null || request.Lines.Count == 0)
                throw new InvalidOperationException($"Purchase request {request.Id} has no lines.");

            foreach (var line in request.Lines)
            {
                if (string.IsNullOrWhiteSpace(line.ManufacturerPartNumber))
                    throw new InvalidOperationException($"Line {line.Id}: ManufacturerPartNumber is required.");
                if (line.RequestedQuantity <= 0)
                    throw new InvalidOperationException($"Line {line.Id}: RequestedQuantity must be positive.");
            }
        }

        private async Task<List<ISupplierAdapter>> GetActiveAdaptersAsync()
        {
            var preferences = await _policyRepository.GetSupplierPreferencesAsync();
            var activeCodes = new HashSet<string>(
                preferences.Where(p => p.Active).Select(p => p.SupplierCode),
                StringComparer.OrdinalIgnoreCase);

            // No preferences configured yet (fresh database) — fall back to every registered adapter.
            return activeCodes.Count == 0
                ? _adapters.ToList()
                : _adapters.Where(a => activeCodes.Contains(a.SupplierCode)).ToList();
        }

        private async Task<PurchaseRequestLine> GetLineAsync(int purchaseRequestLineId)
        {
            // PurchaseRequestLine has no dedicated repository (it's always accessed through its
            // parent PurchaseRequest); a small linear scan over open requests is fine at prototype scale.
            var openRequests = await _purchaseRequestRepository.GetAllAsync();
            foreach (var request in openRequests)
            {
                var line = request.Lines.FirstOrDefault(l => l.Id == purchaseRequestLineId);
                if (line != null) return line;
            }
            throw new InvalidOperationException($"Purchase request line {purchaseRequestLineId} not found.");
        }

        private async Task<LineOutcome> SourceAndProcessLineAsync(
            PurchaseRequestLine line, IReadOnlyList<ISupplierAdapter> activeAdapters, ApprovalPolicy approvalPolicy)
        {
            // A line that already has a SupplierSelection was already sourced, selected and (at
            // least attempted to be) ordered — either automatically or via manual approval/
            // override. Re-running the pipeline for it would place a second order for the same
            // line, so treat it as already handled instead.
            var existingSelection = await _selectionRepository.GetByLineIdAsync(line.Id);
            if (existingSelection != null)
            {
                await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "SOURCING_SKIPPED_ALREADY_SELECTED", "Success",
                    responsePayload: $"SupplierSelectionId={existingSelection.Id}");
                return LineOutcome.Selected;
            }

            // Sourcing per article (user decision, 18 nov 2026): every open line of the same article
            // is sourced together — offers and the packaging choice on the summed need, so a full reel
            // is compared against the total demand instead of against one line. A line without an
            // article id (manual test request) is a group of one.
            var group = new List<PurchaseRequestLine>();
            if (!string.IsNullOrWhiteSpace(line.ErpArticleId))
                group.AddRange(await _purchaseRequestRepository.GetOpenLinesForArticleAsync(line.ErpArticleId));
            if (group.All(l => l.Id != line.Id))
                group.Add(line);
            group = group.OrderBy(l => l.RequiredDate ?? DateTime.MaxValue).ThenBy(l => l.Id).ToList();

            // Sourcing again replaces the previous offers instead of adding to them (18 nov 2026).
            foreach (var member in group)
            {
                var removedOffers = await _offerRepository.DeleteForResourcingAsync(member.Id);
                if (removedOffers > 0)
                    await _auditLogger.LogAsync("PurchaseRequestLine", member.Id.ToString(), "OLD_OFFERS_REMOVED", "Success",
                        responsePayload: $"Removed={removedOffers}");
            }

            // Packaging policy + attrition per component type/category (user request, 17 nov 2026).
            // Attrition stays per line (each line is its own production run), summed for the group.
            var maxUnitCost = ApprovalEvaluator.GetReferenceUnitPrice(await _erp.GetPartCostAsync(line.ErpArticleId));
            var classification = await GetComponentClassificationAsync(line);
            var packagingPolicy = PackagingRules.ResolvePolicy(await _policyRepository.GetPackagingPoliciesAsync(), classification);
            var attritionRule = AttritionCalculator.FindRule(await _policyRepository.GetAttritionRulesAsync(), classification, maxUnitCost);
            var attritionPerLine = group.ToDictionary(l => l.Id, l => AttritionCalculator.CalculateExtra(l.RequestedQuantity, attritionRule));
            var totalRequested = group.Sum(l => l.RequestedQuantity);
            var totalAttrition = attritionPerLine.Values.Sum();
            await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "PACKAGING_POLICY_RESOLVED", "Success",
                responsePayload: $"Type={classification?.ComponentType ?? "-"}; Category={classification?.Category ?? "-"}; " +
                                 $"Policy={packagingPolicy?.ComponentCategory ?? "-"}; MaxUnitCost={maxUnitCost?.ToString() ?? "-"}; " +
                                 $"AttritionRule={(attritionRule == null ? "-" : attritionRule.AppliesTo)}; Extra={totalAttrition}; " +
                                 $"GroupLines={group.Count}; GroupRequested={totalRequested}");

            var mappings = await ResolveProductMappingsAsync(line, activeAdapters);
            if (mappings.Count == 0)
            {
                await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "SOURCING_NO_MATCH", "Exception");
                return LineOutcome.Exception;
            }

            var templates = new List<SupplierOffer>();
            foreach (var kvp in mappings)
            {
                var adapter = kvp.Key;
                if (adapter.Capabilities.Pricing != CapabilityStatus.Supported ||
                    adapter.Capabilities.Availability != CapabilityStatus.Supported)
                {
                    // Spec §5: a missing capability is skipped, never a hard failure.
                    continue;
                }

                try
                {
                    templates.AddRange(await BuildOffersAsync(adapter, line, kvp.Value.SupplierPartNumber, kvp.Value.Confidence,
                        packagingPolicy, totalRequested, totalAttrition, maxUnitCost));
                }
                catch (SupplierException ex)
                {
                    await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "OFFER_BUILD_FAILED", "Error",
                        adapter.SupplierCode, requestPayload: $"SupplierPartNumber={kvp.Value.SupplierPartNumber}", error: ex.Message);
                }
            }

            if (templates.Count == 0)
            {
                await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "SOURCING_NO_OFFERS", "Exception");
                return LineOutcome.Exception;
            }

            // The choice is made once, on the group totals; the earliest line is the reference for
            // the required date (the strictest one).
            var referenceLine = group[0];
            var selectionResult = SupplierSelectionEngine.SelectBestOffer(templates, referenceLine, packagingPolicy, maxUnitCost);

            // Every line gets its own copy of the group's offers (same quantities and prices, its own
            // requested quantity and attrition), linked by BundleKey: choosing, approving and ordering
            // one line of the bundle does the same for the others.
            var bundleKey = group.Count > 1 ? Guid.NewGuid().ToString("N") : null;
            var copiesPerLine = new Dictionary<int, List<SupplierOffer>>();
            foreach (var member in group)
            {
                var copies = templates.Select(t => CopyForLine(t, member, attritionPerLine[member.Id], bundleKey, group.Count, totalRequested + totalAttrition)).ToList();
                copiesPerLine[member.Id] = copies;
            }
            await _offerRepository.AddRangeAsync(copiesPerLine.Values.SelectMany(c => c).ToList());
            foreach (var member in group)
                await _auditLogger.LogAsync("PurchaseRequestLine", member.Id.ToString(), "OFFERS_BUILT", "Success",
                    requestPayload: $"OfferCount={templates.Count}; GroupLines={group.Count}" + (bundleKey != null ? $"; BundleKey={bundleKey}" : ""));

            if (selectionResult.SelectedOffer == null)
            {
                foreach (var member in group)
                {
                    await _auditLogger.LogAsync("PurchaseRequestLine", member.Id.ToString(), "SELECTION_FAILED", "Exception",
                        error: selectionResult.ReasonSummary);
                    if (member.Id != line.Id)
                        await _purchaseRequestRepository.UpdateStatusAsync(member.PurchaseRequestId, PurchaseRequestStatus.Exception);
                }
                foreach (var member in group) _groupedThisRun[member.Id] = LineOutcome.Exception;
                return LineOutcome.Exception;
            }

            var selectedIndex = templates.IndexOf(selectionResult.SelectedOffer);
            var selected = selectionResult.SelectedOffer;
            var reasonSummary = group.Count > 1
                ? $"{selectionResult.ReasonSummary} (samen gesourced met {group.Count - 1} andere regel(s) van dit artikel, totaal {totalRequested + totalAttrition} nodig)"
                : selectionResult.ReasonSummary;

            var preference = await _policyRepository.GetSupplierPreferenceAsync(selected.SupplierCode);
            var evaluation = ApprovalEvaluator.Evaluate(selected, referenceLine, approvalPolicy, preference, maxUnitCost);

            if (evaluation.IsAutoApproved)
            {
                foreach (var member in group)
                {
                    var memberOffer = copiesPerLine[member.Id][selectedIndex];
                    await _selectionRepository.AddAsync(new SupplierSelection
                    {
                        PurchaseRequestLineId = member.Id,
                        SelectedOfferId = memberOffer.Id,
                        ReasonSummary = reasonSummary,
                        Mode = SelectionMode.Automatic
                    });
                    await _auditLogger.LogAsync("PurchaseRequestLine", member.Id.ToString(), "SUPPLIER_SELECTED", "Success",
                        selected.SupplierCode, responsePayload: reasonSummary);
                    if (member.Id != line.Id)
                        await RecomputeRequestStatusIfFullySelectedAsync(member.PurchaseRequestId);
                    _groupedThisRun[member.Id] = LineOutcome.Selected;
                }

                return LineOutcome.Selected;
            }

            var reasons = string.Join("; ", evaluation.Reasons);
            foreach (var member in group)
            {
                var approval = new ApprovalRequest
                {
                    PurchaseRequestLineId = member.Id,
                    ProposedOfferId = copiesPerLine[member.Id][selectedIndex].Id,
                    Reasons = reasons
                };
                await _approvalRepository.AddAsync(approval);
                await _auditLogger.LogAsync("PurchaseRequestLine", member.Id.ToString(), "APPROVAL_REQUIRED", "Pending",
                    selected.SupplierCode, responsePayload: reasons);
                if (member.Id != line.Id)
                    await _purchaseRequestRepository.UpdateStatusAsync(member.PurchaseRequestId, PurchaseRequestStatus.WaitingApproval);
                _groupedThisRun[member.Id] = LineOutcome.PendingApproval;
            }
            return LineOutcome.PendingApproval;
        }

        /// <summary>One line's copy of a group offer: the group's quantities/prices, this line's own requested quantity and attrition.</summary>
        private static SupplierOffer CopyForLine(SupplierOffer template, PurchaseRequestLine line, int attritionQuantity,
            string bundleKey, int bundleLineCount, int bundleNeededQuantity)
        {
            return new SupplierOffer
            {
                PurchaseRequestLineId = line.Id,
                SupplierCode = template.SupplierCode,
                SupplierPartNumber = template.SupplierPartNumber,
                Manufacturer = template.Manufacturer,
                ManufacturerPartNumber = template.ManufacturerPartNumber,
                RequestedQuantity = line.RequestedQuantity,
                OfferedQuantity = template.OfferedQuantity,
                AttritionQuantity = attritionQuantity,
                SurplusQuantity = template.SurplusQuantity,
                UnitPrice = template.UnitPrice,
                MainUnitPrice = template.MainUnitPrice,
                Currency = template.Currency,
                TotalPrice = template.TotalPrice,
                MinimumOrderQuantity = template.MinimumOrderQuantity,
                OrderMultiple = template.OrderMultiple,
                PackagingType = template.PackagingType,
                PackagingQuantity = template.PackagingQuantity,
                ReelType = template.ReelType,
                ReelingFee = template.ReelingFee,
                RemainderSupplierPartNumber = template.RemainderSupplierPartNumber,
                RemainderQuantity = template.RemainderQuantity,
                RemainderUnitPrice = template.RemainderUnitPrice,
                RemainderPackagingType = template.RemainderPackagingType,
                AvailableQuantity = template.AvailableQuantity,
                LeadTimeDays = template.LeadTimeDays,
                EstimatedDeliveryDate = template.EstimatedDeliveryDate,
                ShippingCost = template.ShippingCost,
                Tax = template.Tax,
                LandedCost = template.LandedCost,
                ComparisonCost = template.ComparisonCost,
                MatchConfidence = template.MatchConfidence,
                ExpiresAt = template.ExpiresAt,
                BundleKey = bundleKey,
                BundleLineCount = bundleKey != null ? bundleLineCount : 0,
                BundleNeededQuantity = bundleKey != null ? bundleNeededQuantity : 0
            };
        }

        private async Task<Dictionary<ISupplierAdapter, (string SupplierPartNumber, MatchConfidence Confidence)>> ResolveProductMappingsAsync(
            PurchaseRequestLine line, IReadOnlyList<ISupplierAdapter> activeAdapters)
        {
            var searchTasks = activeAdapters.Select(adapter => ResolveOneMappingAsync(line, adapter));
            var resolved = await Task.WhenAll(searchTasks);

            var results = new Dictionary<ISupplierAdapter, (string, MatchConfidence)>();
            foreach (var (adapter, supplierPartNumber, confidence) in resolved)
            {
                if (supplierPartNumber != null)
                    results[adapter] = (supplierPartNumber, confidence);
            }
            return results;
        }

        private async Task<(ISupplierAdapter Adapter, string SupplierPartNumber, MatchConfidence Confidence)> ResolveOneMappingAsync(
            PurchaseRequestLine line, ISupplierAdapter adapter)
        {
            var existing = await _mappingRepository.FindAsync(line.ErpArticleId, adapter.SupplierCode);

            // A cached mapping with a blank SupplierPartNumber is unusable (BuildOfferAsync's
            // GetPricingAsync/GetAvailabilityAsync throw "SupplierPartNumber is required" on it,
            // logged as OFFER_BUILD_FAILED) — found in practice (okt 2026): once such a row exists,
            // every future sourcing attempt for that article/supplier silently reused it and never
            // searched again, since the branch below never even runs. Treated the same as "no mapping
            // yet" so a fresh search gets a chance to self-heal it (see the update-in-place branch
            // below) instead of a one-time bad search result blocking that article/supplier forever.
            var hasUsableMapping = existing != null && !string.IsNullOrWhiteSpace(existing.SupplierPartNumber);
            if (hasUsableMapping)
                return (adapter, existing.SupplierPartNumber, existing.MatchConfidence);

            if (adapter.Capabilities.ProductSearch != CapabilityStatus.Supported)
                return (adapter, null, MatchConfidence.Unknown);

            try
            {
                var products = await adapter.SearchProductsAsync(new ProductSearchRequest
                {
                    Manufacturer = line.Manufacturer,
                    ManufacturerPartNumber = line.ManufacturerPartNumber,
                    Description = line.Description,
                    Quantity = line.RequestedQuantity
                });

                // Also excludes a match whose own SupplierPartNumber is blank (e.g. a marketplace-only
                // listing with no direct part number) — same "unusable" reasoning as above, applied
                // here so a mapping like that never gets cached in the first place.
                var best = products?
                    .Where(p => !string.IsNullOrWhiteSpace(p.SupplierPartNumber))
                    .OrderByDescending(p => p.MatchConfidence)
                    .FirstOrDefault();
                if (best == null)
                {
                    await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "PRODUCT_NOT_FOUND", "Exception", adapter.SupplierCode);
                    return (adapter, null, MatchConfidence.Unknown);
                }

                int mappingId;
                if (existing != null)
                {
                    // Update in place, never AddAsync here — (ErpArticleId, SupplierCode) has a
                    // unique index, so a second insert for the same pair would violate it.
                    await _mappingRepository.UpdateSupplierPartAsync(existing.Id, best.SupplierPartNumber, best.Manufacturer, best.ManufacturerPartNumber, best.MatchConfidence);
                    mappingId = existing.Id;
                }
                else
                {
                    var mapping = new SupplierProductMapping
                    {
                        ErpArticleId = line.ErpArticleId,
                        SupplierCode = adapter.SupplierCode,
                        SupplierPartNumber = best.SupplierPartNumber,
                        Manufacturer = best.Manufacturer,
                        ManufacturerPartNumber = best.ManufacturerPartNumber,
                        MatchConfidence = best.MatchConfidence
                    };
                    await _mappingRepository.AddAsync(mapping);
                    mappingId = mapping.Id;
                }

                await _auditLogger.LogAsync("SupplierProductMapping", mappingId.ToString(), "PRODUCT_SEARCHED", "Success", adapter.SupplierCode,
                    requestPayload: $"Manufacturer={line.Manufacturer}; MPN={line.ManufacturerPartNumber}",
                    responsePayload: $"SupplierPartNumber={best.SupplierPartNumber}; Confidence={best.MatchConfidence}");

                return (adapter, best.SupplierPartNumber, best.MatchConfidence);
            }
            catch (SupplierException ex)
            {
                await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "PRODUCT_SEARCH_FAILED", "Error",
                    adapter.SupplierCode, error: ex.Message);
                return (adapter, null, MatchConfidence.Unknown);
            }
        }

        /// <summary>
        /// UniPro_OmnifyPartType lookup (MAX). A failure (e.g. the view doesn't exist in this company
        /// database) must not stop sourcing: it's logged and the article falls under "Default".
        /// </summary>
        private async Task<ComponentClassification> GetComponentClassificationAsync(PurchaseRequestLine line)
        {
            try
            {
                return await _erp.GetComponentClassificationAsync(line.ErpArticleId);
            }
            catch (Exception ex)
            {
                await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "COMPONENT_CLASSIFICATION_FAILED", "Error",
                    requestPayload: $"PartId={line.ErpArticleId}", error: ex.Message);
                return null;
            }
        }

        /// <summary>
        /// One offer per packaging variant the supplier has for the part (17 nov 2026: every variant is
        /// its own offer and cost decides, see PackagingRules), or one offer when the adapter reports no
        /// variants. Variants the packaging policy doesn't allow are still built, so the selection can
        /// reject them with a reason that shows up in the offers grid.
        /// </summary>
        private async Task<List<SupplierOffer>> BuildOffersAsync(
            ISupplierAdapter adapter, PurchaseRequestLine line, string supplierPartNumber, MatchConfidence confidence,
            PackagingPolicy policy, int requestedQuantity, int attritionQuantity, decimal? maxUnitCost)
        {
            // requestedQuantity/attritionQuantity are the totals of the article group (18 nov 2026).
            var neededQuantity = requestedQuantity + attritionQuantity;
            var pricing = await adapter.GetPricingAsync(supplierPartNumber, neededQuantity);
            var availability = await adapter.GetAvailabilityAsync(supplierPartNumber);

            IReadOnlyList<SupplierPackagingOption> packagingOptions = new List<SupplierPackagingOption>();
            if (adapter.Capabilities.Packaging == CapabilityStatus.Supported)
                packagingOptions = await adapter.GetPackagingOptionsAsync(supplierPartNumber, neededQuantity);

            var variants = packagingOptions != null && packagingOptions.Count > 0
                ? packagingOptions.ToList()
                : new List<SupplierPackagingOption> { null };

            var offers = new List<SupplierOffer>();
            var originalReelRequired = line.ReelRequirement &&
                (policy?.OriginalReelRequired ?? line.PackagingRequirement == PackagingRequirement.OriginalReel);

            // A Digi-Reel/re-reel or cut tape is never longer than a full reel (user, 18 nov 2026:
            // "DigiKey heeft geen reels waar er 23495 oppassen, dus dit kan helemaal niet"). From a full
            // reel's worth upward, those variants only make sense as the rest of a combination offer.
            var fullReelSize = variants
                .Where(v => v != null && v.PackagingType == PackagingType.OriginalReel && v.PackagingQuantity > 0)
                .Select(v => v.PackagingQuantity)
                .DefaultIfEmpty(0)
                .Max();

            foreach (var variant in variants)
            {
                if (fullReelSize > 0 && neededQuantity >= fullReelSize && variant != null &&
                    (variant.PackagingType == PackagingType.ReReel || variant.PackagingType == PackagingType.CutTape))
                {
                    // Still used as the rest of a combination offer (CombinationOfferBuilder), just not on its own.
                    continue;
                }

                var offer = BuildVariantOffer(adapter.SupplierCode, line, supplierPartNumber, confidence, policy,
                    requestedQuantity, attritionQuantity, maxUnitCost, pricing, availability, variant);
                offers.Add(offer);

                // Full reels + rest from another variant of this supplier (18 nov 2026).
                var combination = CombinationOfferBuilder.TryBuild(offer, variant, packagingOptions, neededQuantity,
                    policy, originalReelRequired, maxUnitCost);
                if (combination != null)
                    offers.Add(combination);
            }

            return offers;
        }

        private static SupplierOffer BuildVariantOffer(
            string supplierCode, PurchaseRequestLine line, string supplierPartNumber, MatchConfidence confidence, PackagingPolicy policy,
            int requestedQuantity, int attritionQuantity, decimal? maxUnitCost, SupplierPricing pricing, SupplierAvailability availability, SupplierPackagingOption variant)
        {
            var neededQuantity = requestedQuantity + attritionQuantity;
            var ownPriceBreaks = variant?.PriceBreaks != null && variant.PriceBreaks.Count > 0 ? variant.PriceBreaks : null;

            var minimumOrderQuantity = variant != null && variant.MinimumOrderQuantity > 0
                ? variant.MinimumOrderQuantity
                : ownPriceBreaks?.Min(pb => pb.BreakQuantity) ?? pricing.MinimumOrderQuantity;

            // Only a full (original) reel rounds up to whole reels; cut tape, re-reel, tray and tube
            // are sold per piece above the MOQ.
            int orderMultiple;
            if (variant != null && variant.PackagingType == PackagingType.OriginalReel && variant.PackagingQuantity > 0)
                orderMultiple = variant.PackagingQuantity;
            else if (ownPriceBreaks != null)
                orderMultiple = 1;
            else
                orderMultiple = Math.Max(pricing.OrderMultiple, 1);

            var orderedQuantity = QuantityCalculator.CalculateOrderedQuantity(neededQuantity, orderMultiple, minimumOrderQuantity);
            var unitPrice = PriceAt(ownPriceBreaks ?? pricing.PriceBreaks, orderedQuantity) ?? pricing.UnitPrice;
            var reelingFee = variant?.ReelingFee ?? 0m;
            var landedCost = LandedCostCalculator.Calculate(orderedQuantity, unitPrice, reelingFee, shippingCost: 0m, tax: 0m);
            var surplusQuantity = Math.Max(orderedQuantity - neededQuantity, 0);
            var comparisonCost = PackagingRules.CalculateComparisonCost(
                landedCost, surplusQuantity, maxUnitCost ?? unitPrice, policy?.SurplusPenaltyFactor ?? 0m);

            return new SupplierOffer
            {
                PurchaseRequestLineId = line.Id,
                SupplierCode = supplierCode,
                SupplierPartNumber = string.IsNullOrWhiteSpace(variant?.SupplierPartNumber) ? supplierPartNumber : variant.SupplierPartNumber,
                Manufacturer = line.Manufacturer,
                ManufacturerPartNumber = line.ManufacturerPartNumber,
                RequestedQuantity = requestedQuantity,
                OfferedQuantity = orderedQuantity,
                AttritionQuantity = attritionQuantity,
                SurplusQuantity = surplusQuantity,
                UnitPrice = unitPrice,
                Currency = pricing.Currency ?? "EUR",
                TotalPrice = orderedQuantity * unitPrice,
                MinimumOrderQuantity = minimumOrderQuantity,
                OrderMultiple = orderMultiple,
                PackagingType = variant?.PackagingType ?? PackagingType.Any,
                PackagingQuantity = variant?.PackagingQuantity ?? 0,
                ReelType = variant?.ReelType,
                ReelingFee = reelingFee,
                // A variant's own stock where the supplier reports it (DigiKey per package type),
                // otherwise the part's overall availability.
                AvailableQuantity = variant != null && variant.AvailableQuantity > 0 ? variant.AvailableQuantity : availability.AvailableQuantity,
                LeadTimeDays = availability.LeadTimeDays,
                EstimatedDeliveryDate = availability.EstimatedDeliveryDate,
                ShippingCost = 0m,
                Tax = 0m,
                LandedCost = landedCost,
                ComparisonCost = comparisonCost,
                MatchConfidence = confidence,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };
        }

        private static decimal? PriceAt(IReadOnlyCollection<PriceBreak> priceBreaks, int quantity) =>
            CombinationOfferBuilder.PriceAt(priceBreaks, quantity);

        private async Task RecomputeRequestStatusIfFullySelectedAsync(int purchaseRequestId)
        {
            var request = await _purchaseRequestRepository.GetByIdAsync(purchaseRequestId);
            if (request == null || request.Lines.Count == 0) return;

            foreach (var requestLine in request.Lines)
            {
                var selection = await _selectionRepository.GetByLineIdAsync(requestLine.Id);
                if (selection == null) return;
            }

            await _purchaseRequestRepository.UpdateStatusAsync(purchaseRequestId, PurchaseRequestStatus.ReadyToOrder);
            await _auditLogger.LogAsync("PurchaseRequest", purchaseRequestId.ToString(), "ALL_LINES_SELECTED", "Success");
        }

        /// <summary>
        /// False: PlaceOrdersAsync refuses to run (no MAX PO, nothing sent to a supplier). Set from
        /// App.config "Ordering.Enabled" (17 nov 2026: test search/select against real data first).
        /// Default true here so tests and other callers are unaffected; the app passes the config value.
        /// </summary>
        public bool OrderingEnabled { get; set; } = true;

        /// <summary>
        /// Spec correction, aug 2026: placing orders is decoupled from sourcing/approval, and no
        /// longer one PO per line — it's grouped by supplier (never by project/customer, that's not
        /// how this business buys), one PO per supplier, exactly matching MAX's own PurchaseOrder
        /// concept (see PurchaseOrder.cs). Gathers every line of the given requests that has a
        /// SupplierSelection but isn't on a PO yet, groups those by SupplierCode, and places one
        /// (possibly multi-line, possibly multi-request) PO per group.
        /// </summary>
        public async Task PlaceOrdersAsync(IReadOnlyList<int> purchaseRequestIds, string user)
        {
            if (!OrderingEnabled)
                throw new InvalidOperationException(
                    "Bestellen staat uit (App.config: Ordering.Enabled = false). Er is geen order geplaatst.");

            var candidates = await GatherOrderCandidatesAsync(purchaseRequestIds);

            if (candidates.Count == 0)
            {
                await _auditLogger.LogAsync("PurchaseOrder", "-", "PLACE_ORDERS_NOTHING_TO_DO", "Success", userOrSystem: user);
                return;
            }

            foreach (var group in candidates.GroupBy(c => c.Offer.SupplierCode))
            {
                await PlaceSupplierPurchaseOrderAsync(group.Key, group.ToList(), user);
            }

            foreach (var requestId in candidates.Select(c => c.Line.PurchaseRequestId).Distinct())
            {
                await RecomputeRequestStatusAfterOrderPlacementAsync(requestId);
            }
        }

        /// <summary>
        /// Read-only preview of which supplier(s) PlaceOrdersAsync would actually place orders
        /// against for this same selection — used by MainForm to decide, before placing anything,
        /// whether to show Farnell's ship-to-address picker (user request, 21 okt 2026). Shares
        /// PlaceOrdersAsync's own candidate-gathering so the two can never disagree about what
        /// counts as "about to be ordered".
        /// </summary>
        public async Task<IReadOnlyList<string>> PreviewSupplierCodesForOrderAsync(IReadOnlyList<int> purchaseRequestIds)
        {
            var candidates = await GatherOrderCandidatesAsync(purchaseRequestIds);
            return candidates.Select(c => c.Offer.SupplierCode).Distinct().ToList();
        }

        private async Task<List<(PurchaseRequestLine Line, SupplierOffer Offer)>> GatherOrderCandidatesAsync(IReadOnlyList<int> purchaseRequestIds)
        {
            var candidates = new List<(PurchaseRequestLine Line, SupplierOffer Offer)>();

            foreach (var requestId in purchaseRequestIds)
            {
                var request = await _purchaseRequestRepository.GetByIdAsync(requestId);
                if (request == null) continue;

                foreach (var line in request.Lines)
                {
                    var selection = await _selectionRepository.GetByLineIdAsync(line.Id);
                    if (selection == null) continue;
                    if (await _purchaseOrderRepository.HasOrderForLineAsync(line.Id)) continue;

                    var offer = await _offerRepository.GetByIdAsync(selection.SelectedOfferId);
                    if (offer == null) continue;

                    candidates.Add((line, offer));
                }
            }

            // Sourcing per article (18 nov 2026): a bundle is always ordered whole, also when only one
            // of its requests is selected — otherwise that one line alone would get the full reel(s).
            var bundleKeys = candidates.Select(c => c.Offer.BundleKey).Where(k => !string.IsNullOrEmpty(k)).Distinct().ToList();
            foreach (var bundleKey in bundleKeys)
            {
                foreach (var sibling in await _offerRepository.GetByBundleKeyAsync(bundleKey))
                {
                    if (candidates.Any(c => c.Line.Id == sibling.PurchaseRequestLineId)) continue;
                    var selection = await _selectionRepository.GetByLineIdAsync(sibling.PurchaseRequestLineId);
                    if (selection == null || selection.SelectedOfferId != sibling.Id) continue;
                    if (await _purchaseOrderRepository.HasOrderForLineAsync(sibling.PurchaseRequestLineId)) continue;

                    var siblingLine = await _purchaseRequestRepository.GetLineByIdAsync(sibling.PurchaseRequestLineId);
                    if (siblingLine == null) continue;
                    var siblingOffer = await _offerRepository.GetByIdAsync(sibling.Id);
                    candidates.Add((siblingLine, siblingOffer));
                }
            }

            return candidates;
        }

        /// <summary>Spec §6 steps 9-11, now per supplier group instead of per line: ERP PO first (always, for every group), then the supplier-side submission (skipped as a manual process if the adapter doesn't support Ordering), then status reflected back to the ERP.</summary>
        private async Task PlaceSupplierPurchaseOrderAsync(string supplierCode, List<(PurchaseRequestLine Line, SupplierOffer Offer)> items, string user)
        {
            var year = DateTime.UtcNow.Year;
            var sequence = await _purchaseOrderRepository.GetNextSequenceForYearAsync(year);
            var idempotencyKey = IdempotencyKeyGenerator.Generate(year, sequence, supplierCode);

            var draft = new PurchaseOrderDraft
            {
                SupplierCode = supplierCode,
                Currency = items[0].Offer.Currency,
                IdempotencyKey = idempotencyKey,
                Lines = BuildBundledDraftLines(items)
            };

            string erpPoNumber;
            try
            {
                erpPoNumber = await _erp.CreatePurchaseOrderAsync(draft);
                await _auditLogger.LogAsync("PurchaseOrder", erpPoNumber, "ERP_PO_CREATED", "Success", supplierCode,
                    requestPayload: $"Lines={items.Count}", userOrSystem: user);
            }
            catch (Exception ex)
            {
                await _auditLogger.LogAsync("PurchaseOrder", "-", "ERP_PO_CREATE_FAILED", "Error", supplierCode,
                    error: ex.Message, userOrSystem: user);
                throw;
            }

            var adapter = _adapters.FirstOrDefault(a => a.SupplierCode == supplierCode);
            if (adapter == null || adapter.Capabilities.Ordering != CapabilityStatus.Supported)
            {
                await _auditLogger.LogAsync("PurchaseOrder", erpPoNumber, "SUPPLIER_ORDER_SKIPPED", "ManualProcess", supplierCode,
                    error: "Ordering capability niet ondersteund door deze adapter; order moet handmatig geplaatst worden.");
                return;
            }

            var request = new SupplierOrderRequest
            {
                SupplierCode = supplierCode,
                ErpPoNumber = erpPoNumber,
                Currency = draft.Currency,
                // One supplier order line per supplier part number: a bundle of request lines for the
                // same article goes to the supplier as one line with the total (see OrderBundler).
                Lines = draft.Lines
                    .GroupBy(l => l.SupplierPartNumber)
                    .Select(g => new SupplierOrderRequestLine
                    {
                        SupplierPartNumber = g.Key,
                        Quantity = g.Sum(l => l.Quantity),
                        UnitPrice = g.First().UnitPrice,
                        PackagingType = PackagingTypeFor(items, g.Key)
                    }).ToList()
            };

            await _auditLogger.LogAsync("PurchaseOrder", erpPoNumber, "SUPPLIER_ORDER_SUBMITTING", "InProgress", supplierCode,
                requestPayload: DescribeOrderRequest(request), userOrSystem: user);

            SupplierOrderResult result;
            try
            {
                result = await adapter.CreateOrderAsync(request, idempotencyKey);
            }
            catch (SupplierException ex)
            {
                await _auditLogger.LogAsync("PurchaseOrder", erpPoNumber, "SUPPLIER_ORDER_FAILED", "Error",
                    supplierCode, error: $"{ex.ErrorCode}: {ex.Message}");
                throw;
            }

            var purchaseOrder = await _purchaseOrderRepository.GetByErpPoNumberAsync(erpPoNumber);
            await _purchaseOrderRepository.ApplySupplierOrderResultAsync(purchaseOrder.Id, result);
            await _auditLogger.LogAsync("PurchaseOrder", result.SupplierOrderNumber ?? erpPoNumber, "SUPPLIER_ORDER_CREATED",
                "Success", supplierCode, responsePayload: $"Status={result.Status}; OrderTotal={result.OrderTotal} {result.Currency}");

            await _erp.UpdatePurchaseOrderStatusAsync(erpPoNumber, result.Status.ToString());
        }

        /// <summary>
        /// PO lines for one supplier group. Request lines with the same supplier part number are a
        /// bundle (user request, 16 nov 2026): their requested quantities are summed and rounded up to
        /// MOQ/order multiple once instead of per line, so 10 lines of 100 pieces with a 5000 MOQ
        /// become one reel of 5000, not ten. Draft lines stay per request line (local tracking), earliest
        /// required date first with the rounding surplus on it; MaxPurchaseOrderRepository and the
        /// supplier request below merge each bundle into one line with the total. A
        /// single line is a bundle of one, which gives the same quantity as its offer. Unit price: the
        /// price of the bundle's offer with the largest quantity (closest price break to the total).
        /// </summary>
        private static PackagingType PackagingTypeFor(List<(PurchaseRequestLine Line, SupplierOffer Offer)> items, string supplierPartNumber)
        {
            var main = items.FirstOrDefault(i => i.Offer.SupplierPartNumber == supplierPartNumber);
            if (main.Offer != null) return main.Offer.PackagingType;
            var rest = items.FirstOrDefault(i => i.Offer.RemainderSupplierPartNumber == supplierPartNumber);
            return rest.Offer?.RemainderPackagingType ?? PackagingType.Any;
        }

        private static List<PurchaseOrderDraftLine> BuildBundledDraftLines(List<(PurchaseRequestLine Line, SupplierOffer Offer)> items)
        {
            var result = new List<PurchaseOrderDraftLine>();

            // A combination offer (full reels + rest, 18 nov 2026) already holds the exact quantities:
            // per line, or per bundle of lines sourced together (BundleKey). Its reels and rest are
            // spread over the lines earliest required date first, surplus on the earliest line.
            foreach (var combination in items.Where(i => i.Offer.IsCombination).GroupBy(i => i.Offer.BundleKey ?? $"line:{i.Line.Id}"))
            {
                var members = combination
                    .OrderBy(i => i.Line.RequiredDate ?? DateTime.MaxValue)
                    .ThenBy(i => i.Line.Id)
                    .ToList();
                var offer = members[0].Offer;
                var pools = new[]
                {
                    (PartNumber: offer.SupplierPartNumber, Quantity: offer.MainQuantity, UnitPrice: offer.MainUnitPrice),
                    (PartNumber: offer.RemainderSupplierPartNumber, Quantity: offer.RemainderQuantity, UnitPrice: offer.RemainderUnitPrice)
                };

                var allocation = OrderBundler.AllocateAcrossPools(
                    members.Select(i => i.Line.RequestedQuantity + i.Offer.AttritionQuantity).ToList(),
                    pools.Select(pool => pool.Quantity).ToList());

                for (var n = 0; n < members.Count; n++)
                {
                    for (var p = 0; p < pools.Length; p++)
                    {
                        if (allocation[n][p] <= 0) continue;
                        result.Add(new PurchaseOrderDraftLine
                        {
                            PurchaseRequestLineId = members[n].Line.Id,
                            SupplierPartNumber = pools[p].PartNumber,
                            Quantity = allocation[n][p],
                            UnitPrice = pools[p].UnitPrice,
                            LineTotal = allocation[n][p] * pools[p].UnitPrice
                        });
                    }
                }
            }

            foreach (var bundle in items.Where(i => !i.Offer.IsCombination).GroupBy(i => i.Offer.SupplierPartNumber))
            {
                var members = bundle
                    .OrderBy(i => i.Line.RequiredDate ?? DateTime.MaxValue)
                    .ThenBy(i => i.Line.Id)
                    .ToList();
                var priceOffer = members.OrderByDescending(i => i.Offer.OfferedQuantity).First().Offer;
                var orderMultiple = members.Max(i => i.Offer.OrderMultiple);
                var minimumOrderQuantity = members.Max(i => i.Offer.MinimumOrderQuantity);

                var quantities = OrderBundler.AllocateQuantities(
                    // Each line's own attrition extra (17 nov 2026) is part of what it needs, before the
                    // bundle is rounded to MOQ/reel once.
                    members.Select(i => i.Line.RequestedQuantity + i.Offer.AttritionQuantity).ToList(), earliestIndex: 0, orderMultiple, minimumOrderQuantity);

                for (var n = 0; n < members.Count; n++)
                {
                    result.Add(new PurchaseOrderDraftLine
                    {
                        PurchaseRequestLineId = members[n].Line.Id,
                        SupplierPartNumber = bundle.Key,
                        Quantity = quantities[n],
                        UnitPrice = priceOffer.UnitPrice,
                        LineTotal = quantities[n] * priceOffer.UnitPrice
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// "Orderbevestiging verwerken" (MainForm): checks every PO awaiting confirmation with its
        /// supplier, applies whatever comes back automatically (local tracking always, plus MAX's
        /// Confirming/Reference/duedate fields in real mode — see IErpConnector.
        /// ApplyOrderConfirmationAsync), and returns only the lines that need a human look —
        /// quantity/price deviating from what was ordered, or a delivery date confirmed later than
        /// requested. Everything else was already handled by the time this returns; there's nothing
        /// further to review for a "clean" confirmation. A PO whose adapter doesn't support
        /// OrderStatus, or that was never actually submitted to the supplier (no
        /// SupplierOrderNumber — e.g. a ManualProcess adapter), is skipped rather than attempted.
        /// </summary>
        public async Task<OrderConfirmationResult> ProcessOrderConfirmationsAsync(string user)
        {
            var result = new OrderConfirmationResult();
            var candidates = await _purchaseOrderRepository.GetAwaitingConfirmationAsync();

            foreach (var order in candidates)
            {
                var adapter = _adapters.FirstOrDefault(a => a.SupplierCode == order.SupplierCode);
                if (adapter == null || adapter.Capabilities.OrderStatus != CapabilityStatus.Supported)
                {
                    result.SkippedOrderCount++;
                    continue;
                }

                SupplierOrderStatus status;
                try
                {
                    status = await adapter.GetOrderStatusAsync(order.SupplierOrderNumber);
                }
                catch (SupplierException ex)
                {
                    await _auditLogger.LogAsync("PurchaseOrder", order.ErpPoNumber, "ORDER_CONFIRMATION_CHECK_FAILED", "Error",
                        order.SupplierCode, error: $"{ex.ErrorCode}: {ex.Message}", userOrSystem: user);
                    result.SkippedOrderCount++;
                    continue;
                }

                await _erp.ApplyOrderConfirmationAsync(order, status);
                result.CheckedOrderCount++;
                await _auditLogger.LogAsync("PurchaseOrder", order.ErpPoNumber, "ORDER_CONFIRMATION_APPLIED", "Success",
                    order.SupplierCode, responsePayload: $"Status={status.Status}", userOrSystem: user);

                foreach (var line in order.Lines)
                {
                    var statusLine = OrderBundler.ConfirmationForLine(status, order.Lines, line);
                    if (statusLine == null) continue;

                    var requestLine = await _purchaseRequestRepository.GetLineByIdAsync(line.PurchaseRequestLineId);

                    var exception = new OrderConfirmationException
                    {
                        ErpPoNumber = order.ErpPoNumber,
                        SupplierCode = order.SupplierCode,
                        SupplierPartNumber = line.SupplierPartNumber,
                        OrderedQuantity = line.Quantity,
                        ConfirmedQuantity = statusLine.ConfirmedQuantity,
                        OrderedUnitPrice = line.UnitPrice,
                        ConfirmedUnitPrice = statusLine.ConfirmedUnitPrice,
                        RequiredDate = requestLine?.RequiredDate,
                        ConfirmedShipDate = statusLine.EstimatedShipDate
                    };

                    if (exception.QuantityMismatch || exception.PriceMismatch || exception.DeliveryIsLate)
                        result.Exceptions.Add(exception);
                }
            }

            return result;
        }

        /// <summary>A request only moves to Ordered once every one of its lines is covered by a PO — a request whose ReadyToOrder lines only partially made it into this batch (the caller chose a subset) stays as-is instead of being marked done early.</summary>
        private async Task RecomputeRequestStatusAfterOrderPlacementAsync(int purchaseRequestId)
        {
            var request = await _purchaseRequestRepository.GetByIdAsync(purchaseRequestId);
            if (request == null || request.Lines.Count == 0) return;

            foreach (var requestLine in request.Lines)
            {
                if (!await _purchaseOrderRepository.HasOrderForLineAsync(requestLine.Id)) return;
            }

            await _purchaseRequestRepository.UpdateStatusAsync(purchaseRequestId, PurchaseRequestStatus.Ordered);
            await _auditLogger.LogAsync("PurchaseRequest", purchaseRequestId.ToString(), "ALL_LINES_ORDERED", "Success");
        }

        private static string DescribeOrderRequest(SupplierOrderRequest request)
        {
            var lines = string.Join(", ", request.Lines.Select(l => $"{l.SupplierPartNumber} x{l.Quantity} @ {l.UnitPrice}"));
            return $"ErpPoNumber={request.ErpPoNumber}; SupplierCode={request.SupplierCode}; Lines=[{lines}]";
        }
    }
}
