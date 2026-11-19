using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.BusinessLogic;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using NLog;
using Procurement.Erp;

namespace Procurement.Services.PurchaseRequests
{
    /// <summary>Result of loading the requests grid.</summary>
    public class RequestListResult
    {
        public IReadOnlyList<RequestRow> Rows { get; set; }

        /// <summary>How many orders the MAX query itself returned (only set by "Query").</summary>
        public int? MaxOrderCount { get; set; }

        public string StatusText => MaxOrderCount.HasValue
            ? $"{Rows.Count} aanvraag/aanvragen getoond ({MaxOrderCount} orders uit MAX opgehaald met huidig filter)."
            : $"{Rows.Count} aanvraag/aanvragen getoond (statusfilter).";
    }

    /// <summary>Offers of the one line of a request.</summary>
    public class OfferListResult
    {
        /// <summary>The line the offers belong to; null when the request has no line.</summary>
        public int? LineId { get; set; }
        public IReadOnlyList<OfferRow> Rows { get; set; } = new List<OfferRow>();
    }

    /// <summary>Counts after sourcing one or more requests.</summary>
    public class SourcingSummary
    {
        public int ReadyToOrder { get; set; }
        public int WaitingApproval { get; set; }
        public int Exception { get; set; }
        public int Failed { get; set; }
        public int SkippedOrdered { get; set; }

        public override string ToString()
        {
            var summary = $"Sourcing klaar: {ReadyToOrder} klaar om te bestellen, {WaitingApproval} wacht op goedkeuring, {Exception} met fout";
            if (Failed > 0) summary += $", {Failed} mislukt";
            if (SkippedOrdered > 0) summary += $", {SkippedOrdered} overgeslagen (al besteld)";
            return summary + ".";
        }
    }

    /// <summary>
    /// The logic of the Purchase Requests screen (spec §8.1) without the screen: filtering, building
    /// the grid rows, sourcing a batch, choosing an offer, changing a quantity. Moved out of
    /// MainForm on 18 nov 2026 (web app plan, phase 1) so the desktop and the web app behave the same.
    /// Confirmations ("are you sure?") and showing errors stay in the screens.
    /// </summary>
    public class PurchaseRequestScreenService
    {
        private static readonly Logger QueryLog = LogManager.GetLogger("Procurement.Query");
        private readonly ProcurementServices _services;

        public PurchaseRequestScreenService(ProcurementServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>
        /// Only the requests already synced locally; never touches MAX. Applies the same due date and
        /// range filter that "Query" sends to MAX, rebuilt locally, so toggling a status checkbox
        /// doesn't bring back what Query had filtered out. Manual TEST- requests ignore that filter.
        /// </summary>
        public async Task<RequestListResult> LoadLocalAsync(RequestListFilter filter)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            var allRequests = await _services.PurchaseRequestRepository.GetAllAsync();
            var requests = ApplyRangeAndDueDateFilters(allRequests.Where(r => filter.Statuses.Contains(r.Status)), filter)
                .Where(r => filter.ShowTestRequests || !IsManualTestRequest(r.ErpRequestNumber))
                .ToList();
            return new RequestListResult { Rows = await BuildRowsAsync(requests) };
        }

        /// <summary>
        /// "Query": fetches open orders from MAX with the filter (incl. Part_Vendor mapping sync) and
        /// syncs them to local requests. Ordered never comes back here: placed orders are in the Orders screen.
        /// </summary>
        public async Task<RequestListResult> QueryMaxAsync(RequestListFilter filter)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            ApplyFiltersToErpConnector(filter);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var requests = await _services.Engine.GetOpenPurchaseRequestsAsync();
            QueryLog.Info("Query: {0} open aanvragen na synchronisatie ({1} ms)", requests.Count, clock.ElapsedMilliseconds);
            var filtered = requests.Where(r => filter.Statuses.Contains(r.Status))
                .Where(r => filter.ShowTestRequests || !IsManualTestRequest(r.ErpRequestNumber))
                .ToList();
            var rows = await BuildRowsAsync(filtered);
            QueryLog.Info("Query: {0} rijen opgebouwd ({1} ms)", rows.Count, clock.ElapsedMilliseconds);
            return new RequestListResult
            {
                Rows = rows,
                MaxOrderCount = _services.ErpConnector.LastMaxOrderCount
            };
        }

        /// <summary>The offers of the (one) line of a request, with the current choice ticked.</summary>
        public async Task<OfferListResult> GetOffersAsync(int requestId)
        {
            var request = await _services.PurchaseRequestRepository.GetByIdAsync(requestId);
            var line = request?.Lines.FirstOrDefault();
            if (line == null) return new OfferListResult();

            var offers = await _services.Engine.GetOffersForLineAsync(line.Id);
            var selection = await _services.Engine.GetSelectionForLineAsync(line.Id);

            return new OfferListResult
            {
                LineId = line.Id,
                Rows = offers.Select(o => new OfferRow
                {
                    Id = o.Id,
                    Select = selection != null && selection.SelectedOfferId == o.Id,
                    SupplierCode = o.SupplierCode,
                    // Combination offer (full reels + rest, 18 nov 2026): both part numbers and the split.
                    SupplierPartNumber = o.IsCombination ? $"{o.SupplierPartNumber} + {o.RemainderSupplierPartNumber}" : o.SupplierPartNumber,
                    MatchConfidence = o.MatchConfidence.ToString(),
                    OfferedQuantity = o.OfferedQuantity,
                    AttritionQuantity = o.AttritionQuantity,
                    SurplusQuantity = o.SurplusQuantity,
                    // Sourcing per article (18 nov 2026): quantities are for all lines of the bundle together.
                    Bundle = o.BundleLineCount > 1 ? $"{o.BundleLineCount} regels, totaal {o.BundleNeededQuantity:N0}"
                        : o.BundleNeededQuantity > 0 ? $"totaal {o.BundleNeededQuantity:N0}" : string.Empty,
                    UnitPrice = o.UnitPrice,
                    Currency = o.Currency,
                    Packaging = o.IsCombination ? o.Composition : o.PackagingType.ToString(),
                    Reel = o.ReelType?.ToString() ?? "-",
                    ReelingFee = o.ReelingFee,
                    LandedCost = o.LandedCost,
                    ComparisonCost = SupplierSelectionEngine.EffectiveComparisonCost(o),
                    LeadTimeDays = o.LeadTimeDays,
                    AvailableQuantity = o.AvailableQuantity,
                    DueDate = o.EstimatedDeliveryDate
                }).ToList()
            };
        }

        /// <summary>
        /// Sources the requests one after another (never in parallel: one DbContext). One failure
        /// doesn't stop the rest. WaitingApproval is re-sourced too (18 nov 2026); Ordered is skipped,
        /// since re-sourcing would push a placed order back to ReadyToOrder.
        /// </summary>
        /// <param name="progress">Optional; receives "Sourcing 3/10..." texts.</param>
        public async Task<SourcingSummary> SourceRequestsAsync(IReadOnlyList<int> requestIds, IProgress<string> progress = null)
        {
            var summary = new SourcingSummary();
            // Lines of the same article are only bundled within this selection (19 nov 2026).
            _services.Engine.BeginSourcingRun(requestIds);
            try
            {
                for (var i = 0; i < requestIds.Count; i++)
                {
                    progress?.Report($"Sourcing {i + 1}/{requestIds.Count}...");
                    try
                    {
                        var current = await _services.PurchaseRequestRepository.GetByIdAsync(requestIds[i]);
                        if (current == null) continue;
                        if (current.Status == PurchaseRequestStatus.Ordered)
                        {
                            summary.SkippedOrdered++;
                            continue;
                        }

                        await _services.Engine.SourcePurchaseRequestAsync(requestIds[i]);
                        var updated = await _services.PurchaseRequestRepository.GetByIdAsync(requestIds[i]);
                        if (updated == null) continue;
                        if (updated.Status == PurchaseRequestStatus.ReadyToOrder) summary.ReadyToOrder++;
                        else if (updated.Status == PurchaseRequestStatus.WaitingApproval) summary.WaitingApproval++;
                        else if (updated.Status == PurchaseRequestStatus.Exception) summary.Exception++;
                    }
                    catch (Exception)
                    {
                        summary.Failed++;
                    }
                }
            }
            finally
            {
                _services.Engine.EndSourcingRun();
            }
            return summary;
        }

        /// <summary>Tick = choose this offer; untick the chosen one = undo the choice (back to Sourcing).</summary>
        public Task SetOfferSelectedAsync(int lineId, int offerId, bool selected)
        {
            var user = _services.Session.UserName;
            return selected
                ? _services.Engine.SelectOfferManuallyAsync(lineId, offerId, reason: null, user)
                : _services.Engine.DeselectOfferAsync(lineId, user);
        }

        /// <summary>A different total typed on an offer (18 nov 2026): re-source the line and its bundle for that total.</summary>
        public Task ChangeQuantityAsync(int lineId, int newTotalQuantity) =>
            _services.Engine.ResourceLineWithQuantityAsync(lineId, newTotalQuantity, _services.Session.UserName);

        public async Task<IReadOnlyList<RequestRow>> BuildRowsAsync(IReadOnlyList<PurchaseRequest> requests)
        {
            // Every MAX order syncs to exactly one line, so the first line is the line.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var selectionsByLineId = await GetSelectionsByLineIdAsync(requests.Select(r => r.Lines.FirstOrDefault()?.Id));
            QueryLog.Info("Rijen: keuzes opgehaald ({0} ms)", clock.ElapsedMilliseconds);
            var poDueDatesByKey = await _services.MaxOrderRepository.CalculatePoDueDatesAsync(
                requests.Select(r => r.Lines.FirstOrDefault())
                    .Where(l => l != null && l.RequiredDate.HasValue && !string.IsNullOrEmpty(l.ErpArticleId))
                    .Select(l => (l.RequiredDate.Value, l.ErpArticleId))
                    .ToList());
            QueryLog.Info("Rijen: PO DueDates berekend ({0} ms)", clock.ElapsedMilliseconds);
            var preferredVendorIdsByPartId = await _services.MaxOrderRepository.GetPreferredVendorIdsByPartIdsAsync(
                requests.Select(r => r.Lines.FirstOrDefault()?.ErpArticleId).Where(p => !string.IsNullOrEmpty(p)).ToList());

            QueryLog.Info("Rijen: voorkeursleveranciers opgehaald ({0} ms)", clock.ElapsedMilliseconds);

            return requests.Select(r =>
            {
                var line = r.Lines.FirstOrDefault();
                var selection = line != null && selectionsByLineId.TryGetValue(line.Id, out var s) ? s : null;
                var selectedOffer = selection?.SelectedOffer;
                return new RequestRow
                {
                    Id = r.Id,
                    Order = r.ErpRequestNumber,
                    Workflow = r.Status.ToString(),
                    Status = r.MaxOrderStatus,
                    Firm = line?.Firm ?? false,
                    Type = line?.PartType,
                    PartID = line?.ErpArticleId,
                    Rev = line?.Revision,
                    Desc1 = line?.Desc1,
                    Desc2 = line?.Desc2,
                    Quantity = line?.RequestedQuantity ?? 0,
                    Cost = line?.Cost,
                    Cnv = line?.CostConv,
                    ExtCost = ComputeExtCost(line?.RequestedQuantity ?? 0, line?.Cost, line?.CostConv),
                    DueDate = r.RequiredDate,
                    PoDueDate = GetPoDueDate(line, poDueDatesByKey),
                    Reference = r.Project,
                    ManufacturingPart = line?.ManufacturerPartNumber,
                    Customer = line?.Customer,
                    StockID = line?.StockId,
                    RecVendor = line?.ErpArticleId != null && preferredVendorIdsByPartId.TryGetValue(line.ErpArticleId, out var preferredVendorId)
                        ? preferredVendorId
                        : null,
                    SelectedSupplier = selectedOffer?.SupplierCode,
                    SelectedPrice = selectedOffer?.UnitPrice,
                    SelectedQuantity = selectedOffer?.OfferedQuantity,
                    SelectedDueDate = selectedOffer?.EstimatedDeliveryDate
                };
            }).ToList();
        }

        public static bool IsManualTestRequest(string erpRequestNumber) =>
            !string.IsNullOrEmpty(erpRequestNumber) && erpRequestNumber.StartsWith("TEST-", StringComparison.Ordinal);

        /// <summary>Quantity * Cost / Cost Conv; null when unknown or Conv is 0.</summary>
        public static decimal? ComputeExtCost(int quantity, decimal? cost, decimal? costConv)
        {
            if (!cost.HasValue || !costConv.HasValue || costConv.Value == 0m) return null;
            return quantity * cost.Value / costConv.Value;
        }

        public static IReadOnlyList<PurchaseRequest> ApplyRangeAndDueDateFilters(IEnumerable<PurchaseRequest> requests, RequestListFilter filter)
        {
            IEnumerable<PurchaseRequest> result = requests.ToList();

            if (filter.HasDueDateFilter)
            {
                var start = filter.DueDateStart.Value.Date;
                var end = filter.DueDateEnd.Value.Date;
                result = result.Where(r => IsManualTestRequest(r.ErpRequestNumber) ||
                    (r.RequiredDate.HasValue && r.RequiredDate.Value.Date >= start && r.RequiredDate.Value.Date <= end));
            }

            if (filter.HasRange)
            {
                result = result.Where(r => IsManualTestRequest(r.ErpRequestNumber) ||
                    MatchesRange(r, filter.RangeField, filter.TrimmedRangeStart, filter.TrimmedRangeEnd));
            }

            return result.ToList();
        }

        private static bool MatchesRange(PurchaseRequest request, MaxOrderRangeField field, string rangeStart, string rangeEnd)
        {
            string value;
            switch (field)
            {
                case MaxOrderRangeField.Part:
                    value = request.Lines?.FirstOrDefault()?.ErpArticleId;
                    break;
                case MaxOrderRangeField.Customer:
                    value = request.Lines?.FirstOrDefault()?.Customer;
                    break;
                default:
                    value = request.ErpRequestNumber;
                    break;
            }

            if (string.IsNullOrEmpty(value)) return false;
            if (!string.IsNullOrEmpty(rangeStart) && string.CompareOrdinal(value, rangeStart) < 0) return false;
            if (!string.IsNullOrEmpty(rangeEnd) && string.CompareOrdinal(value, rangeEnd) > 0) return false;
            return true;
        }

        private void ApplyFiltersToErpConnector(RequestListFilter filter)
        {
            var erp = _services.ErpConnector;
            var statuses = new HashSet<string>();
            if (filter.IncludePlanned) statuses.Add("1");
            if (filter.IncludeApproved) statuses.Add("2");
            erp.IncludedOrderStatuses = statuses;

            erp.DueDateFilterStart = filter.HasDueDateFilter ? filter.DueDateStart.Value.Date : (DateTime?)null;
            erp.DueDateFilterEnd = filter.HasDueDateFilter ? filter.DueDateEnd.Value.Date : (DateTime?)null;
            erp.RangeFilterField = filter.HasRange ? filter.RangeField : (MaxOrderRangeField?)null;
            erp.RangeFilterStart = filter.TrimmedRangeStart;
            erp.RangeFilterEnd = filter.TrimmedRangeEnd;
        }

        /// <summary>All selections in one go (was one query per line until 19 nov 2026).</summary>
        private Task<IReadOnlyDictionary<int, SupplierSelection>> GetSelectionsByLineIdAsync(IEnumerable<int?> lineIds) =>
            _services.Engine.GetSelectionsForLinesAsync(lineIds.Where(id => id.HasValue).Select(id => id.Value).ToList());

        private static DateTime? GetPoDueDate(
            PurchaseRequestLine line,
            IReadOnlyDictionary<(DateTime MrpDate, string PartNumber), DateTime> poDueDatesByKey)
        {
            if (line == null || !line.RequiredDate.HasValue || string.IsNullOrEmpty(line.ErpArticleId)) return null;
            return poDueDatesByKey.TryGetValue((line.RequiredDate.Value, line.ErpArticleId), out var poDueDate)
                ? poDueDate
                : (DateTime?)null;
        }
    }
}
