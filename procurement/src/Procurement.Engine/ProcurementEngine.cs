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
        Ordered,
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
        private readonly SupplierOrderRepository _supplierOrderRepository;
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
            SupplierOrderRepository supplierOrderRepository,
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
            _supplierOrderRepository = supplierOrderRepository ?? throw new ArgumentNullException(nameof(supplierOrderRepository));
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

            var finalStatus =
                anyPendingApproval ? PurchaseRequestStatus.WaitingApproval :
                anyException ? PurchaseRequestStatus.Exception :
                PurchaseRequestStatus.Ordered;

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

            await PlaceOrderForSelectionAsync(line, offer);
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

            await PlaceOrderForSelectionAsync(line, offer);
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
                return LineOutcome.Ordered;
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

                await PlaceOrderForSelectionAsync(line, selectionResult.SelectedOffer);
                return LineOutcome.Ordered;
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

        /// <summary>Spec §6 steps 9-11: ERP PO always created first, then the supplier order (idempotent), then status is reflected back to the ERP.</summary>
        private async Task PlaceOrderForSelectionAsync(PurchaseRequestLine line, SupplierOffer offer)
        {
            var lineTotal = offer.OfferedQuantity * offer.UnitPrice;
            var draft = new PurchaseOrderDraft
            {
                PurchaseRequestId = line.PurchaseRequestId,
                SupplierCode = offer.SupplierCode,
                Lines = new List<PurchaseOrderDraftLine>
                {
                    new PurchaseOrderDraftLine
                    {
                        PurchaseRequestLineId = line.Id,
                        SupplierPartNumber = offer.SupplierPartNumber,
                        Quantity = offer.OfferedQuantity,
                        UnitPrice = offer.UnitPrice,
                        LineTotal = lineTotal
                    }
                }
            };

            string erpPoNumber;
            try
            {
                erpPoNumber = await _erp.CreatePurchaseOrderAsync(draft);
                await _auditLogger.LogAsync("PurchaseOrder", erpPoNumber, "ERP_PO_CREATED", "Success", offer.SupplierCode,
                    requestPayload: $"PurchaseRequestLineId={line.Id}; Qty={offer.OfferedQuantity}");
            }
            catch (Exception ex)
            {
                await _auditLogger.LogAsync("PurchaseRequestLine", line.Id.ToString(), "ERP_PO_CREATE_FAILED", "Error",
                    offer.SupplierCode, error: ex.Message);
                throw;
            }

            var purchaseOrder = await _purchaseOrderRepository.GetByErpPoNumberAsync(erpPoNumber);

            var adapter = _adapters.FirstOrDefault(a => a.SupplierCode == offer.SupplierCode);
            if (adapter == null || adapter.Capabilities.Ordering != CapabilityStatus.Supported)
            {
                await _auditLogger.LogAsync("PurchaseOrder", erpPoNumber, "SUPPLIER_ORDER_SKIPPED", "ManualProcess", offer.SupplierCode,
                    error: "Ordering capability niet ondersteund door deze adapter; order moet handmatig geplaatst worden.");
            }
            else
            {
                await PlaceSupplierOrderAsync(purchaseOrder, offer, adapter);
            }

            // The line now has a placed (or manually-handed-off) order; once every line of the
            // parent request reaches that point, the request itself should stop showing up as
            // "open" — otherwise it stays selectable in the UI and a repeat "Sourcing starten"
            // click re-runs the whole pipeline and places a second order for the same line.
            await RecomputeRequestStatusIfFullyOrderedAsync(line.PurchaseRequestId);
        }

        private async Task RecomputeRequestStatusIfFullyOrderedAsync(int purchaseRequestId)
        {
            var request = await _purchaseRequestRepository.GetByIdAsync(purchaseRequestId);
            if (request == null || request.Lines.Count == 0) return;

            foreach (var requestLine in request.Lines)
            {
                var selection = await _selectionRepository.GetByLineIdAsync(requestLine.Id);
                if (selection == null) return;
            }

            await _purchaseRequestRepository.UpdateStatusAsync(purchaseRequestId, PurchaseRequestStatus.Ordered);
            await _auditLogger.LogAsync("PurchaseRequest", purchaseRequestId.ToString(), "ALL_LINES_ORDERED", "Success");
        }

        private async Task PlaceSupplierOrderAsync(PurchaseOrder purchaseOrder, SupplierOffer offer, ISupplierAdapter adapter)
        {
            // Spec §10: never blindly resubmit — if an order already exists for this PO+supplier
            // (e.g. a retry after a timeout), just refresh its status instead of creating a new one.
            var existing = await _supplierOrderRepository.FindByErpPoAndSupplierAsync(purchaseOrder.ErpPoNumber, offer.SupplierCode);
            if (existing != null)
            {
                await RefreshOrderStatusAsync(existing, adapter);
                return;
            }

            var year = DateTime.UtcNow.Year;
            var sequence = await _supplierOrderRepository.GetNextSequenceForYearAsync(year);
            var idempotencyKey = IdempotencyKeyGenerator.Generate(year, sequence, offer.SupplierCode);

            var request = new SupplierOrderRequest
            {
                SupplierCode = offer.SupplierCode,
                ErpPoNumber = purchaseOrder.ErpPoNumber,
                Currency = offer.Currency,
                Lines = new List<SupplierOrderRequestLine>
                {
                    new SupplierOrderRequestLine
                    {
                        SupplierPartNumber = offer.SupplierPartNumber,
                        Quantity = offer.OfferedQuantity,
                        UnitPrice = offer.UnitPrice,
                        PackagingType = offer.PackagingType
                    }
                }
            };

            await _auditLogger.LogAsync("SupplierOrder", purchaseOrder.ErpPoNumber, "SUPPLIER_ORDER_SUBMITTING", "InProgress", offer.SupplierCode,
                requestPayload: DescribeOrderRequest(request), userOrSystem: "system");

            SupplierOrderResult result;
            try
            {
                result = await adapter.CreateOrderAsync(request, idempotencyKey);
            }
            catch (SupplierException ex)
            {
                await _auditLogger.LogAsync("SupplierOrder", purchaseOrder.ErpPoNumber, "SUPPLIER_ORDER_FAILED", "Error",
                    offer.SupplierCode, error: $"{ex.ErrorCode}: {ex.Message}");
                throw;
            }

            var order = new SupplierOrder
            {
                ErpPoId = purchaseOrder.Id,
                ErpPoNumber = purchaseOrder.ErpPoNumber,
                SupplierCode = offer.SupplierCode,
                SupplierOrderNumber = result.SupplierOrderNumber,
                IdempotencyKey = idempotencyKey,
                OrderVersion = 1,
                OrderDate = DateTime.UtcNow,
                Currency = result.Currency,
                OrderTotal = result.OrderTotal,
                Status = result.Status,
                SubmittedAt = result.SubmittedAt,
                Lines = new List<SupplierOrderLine>
                {
                    new SupplierOrderLine
                    {
                        SupplierPartNumber = offer.SupplierPartNumber,
                        Quantity = offer.OfferedQuantity,
                        UnitPrice = offer.UnitPrice
                    }
                }
            };

            await _supplierOrderRepository.AddAsync(order);
            await _auditLogger.LogAsync("SupplierOrder", order.SupplierOrderNumber ?? purchaseOrder.ErpPoNumber, "SUPPLIER_ORDER_CREATED",
                "Success", offer.SupplierCode, responsePayload: $"Status={result.Status}; OrderTotal={result.OrderTotal} {result.Currency}");

            await _erp.UpdatePurchaseOrderStatusAsync(purchaseOrder.ErpPoNumber, PurchaseOrderStatus.FullyOrdered.ToString());
        }

        private async Task RefreshOrderStatusAsync(SupplierOrder existing, ISupplierAdapter adapter)
        {
            if (adapter.Capabilities.OrderStatus != CapabilityStatus.Supported) return;

            var status = await adapter.GetOrderStatusAsync(existing.SupplierOrderNumber);
            await _supplierOrderRepository.UpdateStatusAsync(existing.Id, status.Status, status.ConfirmedAt);
            await _auditLogger.LogAsync("SupplierOrder", existing.SupplierOrderNumber, "SUPPLIER_ORDER_STATUS_REFRESHED", "Success",
                existing.SupplierCode, responsePayload: $"Status={status.Status}");
        }

        private static string DescribeOrderRequest(SupplierOrderRequest request)
        {
            var lines = string.Join(", ", request.Lines.Select(l => $"{l.SupplierPartNumber} x{l.Quantity} @ {l.UnitPrice}"));
            return $"ErpPoNumber={request.ErpPoNumber}; SupplierCode={request.SupplierCode}; Lines=[{lines}]";
        }
    }
}
