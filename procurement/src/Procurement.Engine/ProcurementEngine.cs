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

            foreach (var line in request.Lines)
            {
                var outcome = await SourceAndProcessLineAsync(line, activeAdapters, approvalPolicy);
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

        /// <summary>Spec §8.2: user picks a different offer than the engine's proposal. Requires a reason, which becomes part of the audit trail.</summary>
        public async Task SelectOfferManuallyAsync(int purchaseRequestLineId, int selectedOfferId, string reason, string user)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A reason is required for a manual offer selection.", nameof(reason));

            var offer = await _offerRepository.GetByIdAsync(selectedOfferId);
            if (offer == null || offer.PurchaseRequestLineId != purchaseRequestLineId)
                throw new InvalidOperationException("Offer does not belong to the given purchase request line.");

            var line = await GetLineAsync(purchaseRequestLineId);

            var selection = new SupplierSelection
            {
                PurchaseRequestLineId = purchaseRequestLineId,
                SelectedOfferId = offer.Id,
                ReasonSummary = $"Handmatig gekozen door {user}: {reason}",
                Mode = SelectionMode.Manual
            };
            await _selectionRepository.AddAsync(selection);
            await _auditLogger.LogAsync("PurchaseRequestLine", purchaseRequestLineId.ToString(), "SUPPLIER_SELECTED_MANUAL", "Success",
                offer.SupplierCode, responsePayload: selection.ReasonSummary, userOrSystem: user);

            await RecomputeRequestStatusIfFullySelectedAsync(line.PurchaseRequestId);
        }

        /// <summary>Spec §8.3: decide a line that failed the auto-approve criteria.</summary>
        public async Task ApproveAsync(int approvalRequestId, bool approve, string comment, string user)
        {
            var pending = (await _approvalRepository.GetPendingAsync()).FirstOrDefault(a => a.Id == approvalRequestId);
            if (pending == null)
                throw new InvalidOperationException($"Approval request {approvalRequestId} not found or already decided.");

            await _approvalRepository.DecideAsync(
                approvalRequestId,
                approve ? ApprovalDecision.Approved : ApprovalDecision.Rejected,
                comment,
                user);

            await _auditLogger.LogAsync("ApprovalRequest", approvalRequestId.ToString(),
                approve ? "APPROVAL_GRANTED" : "APPROVAL_REJECTED", "Success",
                responsePayload: comment, userOrSystem: user);

            if (!approve) return;

            var offer = await _offerRepository.GetByIdAsync(pending.ProposedOfferId);
            var line = await GetLineAsync(pending.PurchaseRequestLineId);

            var selection = new SupplierSelection
            {
                PurchaseRequestLineId = pending.PurchaseRequestLineId,
                SelectedOfferId = offer.Id,
                ReasonSummary = $"Handmatig goedgekeurd door {user}. Reden(en) voor goedkeuring: {pending.Reasons}. Commentaar: {comment}",
                Mode = SelectionMode.Manual
            };
            await _selectionRepository.AddAsync(selection);

            await RecomputeRequestStatusIfFullySelectedAsync(line.PurchaseRequestId);
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

            var packagingPolicy = await _policyRepository.GetPackagingPolicyAsync("Default");

            var mappings = await ResolveProductMappingsAsync(line, activeAdapters);
            if (mappings.Count == 0)
            {
                await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "SOURCING_NO_MATCH", "Exception");
                return LineOutcome.Exception;
            }

            var offers = new List<SupplierOffer>();
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
                    var offer = await BuildOfferAsync(adapter, line, kvp.Value.SupplierPartNumber, kvp.Value.Confidence, packagingPolicy);
                    offers.Add(offer);
                }
                catch (SupplierException ex)
                {
                    await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "OFFER_BUILD_FAILED", "Error",
                        adapter.SupplierCode, error: ex.Message);
                }
            }

            if (offers.Count == 0)
            {
                await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "SOURCING_NO_OFFERS", "Exception");
                return LineOutcome.Exception;
            }

            await _offerRepository.AddRangeAsync(offers);
            await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "OFFERS_BUILT", "Success",
                requestPayload: $"OfferCount={offers.Count}");

            var selectionResult = SupplierSelectionEngine.SelectBestOffer(offers, line, packagingPolicy);
            if (selectionResult.SelectedOffer == null)
            {
                await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "SELECTION_FAILED", "Exception",
                    error: selectionResult.ReasonSummary);
                return LineOutcome.Exception;
            }

            var preference = await _policyRepository.GetSupplierPreferenceAsync(selectionResult.SelectedOffer.SupplierCode);
            var referencePrice = ComputeReferenceUnitPrice(offers, selectionResult.SelectedOffer);
            var evaluation = ApprovalEvaluator.Evaluate(selectionResult.SelectedOffer, line, approvalPolicy, preference, referencePrice);

            if (evaluation.IsAutoApproved)
            {
                var selection = new SupplierSelection
                {
                    PurchaseRequestLineId = line.Id,
                    SelectedOfferId = selectionResult.SelectedOffer.Id,
                    ReasonSummary = selectionResult.ReasonSummary,
                    Mode = SelectionMode.Automatic
                };
                await _selectionRepository.AddAsync(selection);
                await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "SUPPLIER_SELECTED", "Success",
                    selectionResult.SelectedOffer.SupplierCode, responsePayload: selectionResult.ReasonSummary);

                return LineOutcome.Selected;
            }

            var approval = new ApprovalRequest
            {
                PurchaseRequestLineId = line.Id,
                ProposedOfferId = selectionResult.SelectedOffer.Id,
                Reasons = string.Join("; ", evaluation.Reasons)
            };
            await _approvalRepository.AddAsync(approval);
            await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "APPROVAL_REQUIRED", "Pending",
                selectionResult.SelectedOffer.SupplierCode, responsePayload: approval.Reasons);
            return LineOutcome.PendingApproval;
        }

        private static decimal? ComputeReferenceUnitPrice(IReadOnlyList<SupplierOffer> offers, SupplierOffer selected)
        {
            var others = offers.Where(o => !ReferenceEquals(o, selected)).ToList();
            return others.Count == 0 ? (decimal?)null : others.Average(o => o.UnitPrice);
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
            if (existing != null)
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

                var best = products?.OrderByDescending(p => p.MatchConfidence).FirstOrDefault();
                if (best == null)
                {
                    await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "PRODUCT_NOT_FOUND", "Exception", adapter.SupplierCode);
                    return (adapter, null, MatchConfidence.Unknown);
                }

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
                await _auditLogger.LogAsync("SupplierProductMapping", mapping.Id.ToString(), "PRODUCT_SEARCHED", "Success", adapter.SupplierCode,
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

        private async Task<SupplierOffer> BuildOfferAsync(
            ISupplierAdapter adapter, PurchaseRequestLine line, string supplierPartNumber, MatchConfidence confidence, PackagingPolicy policy)
        {
            var pricing = await adapter.GetPricingAsync(supplierPartNumber, line.RequestedQuantity);
            var availability = await adapter.GetAvailabilityAsync(supplierPartNumber);

            IReadOnlyList<SupplierPackagingOption> packagingOptions = new List<SupplierPackagingOption>();
            if (adapter.Capabilities.Packaging == CapabilityStatus.Supported)
                packagingOptions = await adapter.GetPackagingOptionsAsync(supplierPartNumber, line.RequestedQuantity);

            var chosenPackaging = ChoosePackagingOption(packagingOptions, line, policy);
            var orderMultiple = chosenPackaging != null && chosenPackaging.PackagingQuantity > 0
                ? chosenPackaging.PackagingQuantity
                : Math.Max(pricing.OrderMultiple, 1);

            var orderedQuantity = QuantityCalculator.CalculateOrderedQuantity(
                line.RequestedQuantity, orderMultiple, pricing.MinimumOrderQuantity);

            var reelingFee = chosenPackaging?.ReelingFee ?? 0m;
            var landedCost = LandedCostCalculator.Calculate(orderedQuantity, pricing.UnitPrice, reelingFee, shippingCost: 0m, tax: 0m);

            var offer = new SupplierOffer
            {
                PurchaseRequestLineId = line.Id,
                SupplierCode = adapter.SupplierCode,
                SupplierPartNumber = supplierPartNumber,
                Manufacturer = line.Manufacturer,
                ManufacturerPartNumber = line.ManufacturerPartNumber,
                RequestedQuantity = line.RequestedQuantity,
                OfferedQuantity = orderedQuantity,
                UnitPrice = pricing.UnitPrice,
                Currency = pricing.Currency ?? "EUR",
                TotalPrice = orderedQuantity * pricing.UnitPrice,
                MinimumOrderQuantity = pricing.MinimumOrderQuantity,
                OrderMultiple = orderMultiple,
                PackagingType = chosenPackaging?.PackagingType ?? PackagingType.Any,
                PackagingQuantity = chosenPackaging?.PackagingQuantity ?? 0,
                ReelType = chosenPackaging?.ReelType,
                ReelingFee = reelingFee,
                AvailableQuantity = availability.AvailableQuantity,
                LeadTimeDays = availability.LeadTimeDays,
                EstimatedDeliveryDate = availability.EstimatedDeliveryDate,
                ShippingCost = 0m,
                Tax = 0m,
                LandedCost = landedCost,
                MatchConfidence = confidence,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };

            return offer;
        }

        private static SupplierPackagingOption ChoosePackagingOption(
            IReadOnlyList<SupplierPackagingOption> options, PurchaseRequestLine line, PackagingPolicy policy)
        {
            if (options == null || options.Count == 0) return null;

            if (line.ReelRequirement)
            {
                var reelOption = options.FirstOrDefault(o => o.PackagingType == PackagingType.OriginalReel)
                    ?? options.FirstOrDefault(o => o.ReelType.HasValue);
                if (reelOption != null) return reelOption;
            }

            var preferred = policy != null ? options.FirstOrDefault(o => o.PackagingType == policy.PreferredPackaging) : null;
            return preferred ?? options[0];
        }

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
        /// Spec correction, aug 2026: placing orders is decoupled from sourcing/approval, and no
        /// longer one PO per line — it's grouped by supplier (never by project/customer, that's not
        /// how this business buys), one PO per supplier, exactly matching MAX's own PurchaseOrder
        /// concept (see PurchaseOrder.cs). Gathers every line of the given requests that has a
        /// SupplierSelection but isn't on a PO yet, groups those by SupplierCode, and places one
        /// (possibly multi-line, possibly multi-request) PO per group.
        /// </summary>
        public async Task PlaceOrdersAsync(IReadOnlyList<int> purchaseRequestIds, string user)
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
                Lines = items.Select(i => new PurchaseOrderDraftLine
                {
                    PurchaseRequestLineId = i.Line.Id,
                    SupplierPartNumber = i.Offer.SupplierPartNumber,
                    Quantity = i.Offer.OfferedQuantity,
                    UnitPrice = i.Offer.UnitPrice,
                    LineTotal = i.Offer.OfferedQuantity * i.Offer.UnitPrice
                }).ToList()
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
                Lines = items.Select(i => new SupplierOrderRequestLine
                {
                    SupplierPartNumber = i.Offer.SupplierPartNumber,
                    Quantity = i.Offer.OfferedQuantity,
                    UnitPrice = i.Offer.UnitPrice,
                    PackagingType = i.Offer.PackagingType
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
                    var statusLine = status.Lines?.FirstOrDefault(l => l.SupplierPartNumber == line.SupplierPartNumber);
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
