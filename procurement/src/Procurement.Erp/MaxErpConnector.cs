using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Procurement.Core.Interfaces;
using Procurement.Core.Models;
using Procurement.Data.Repositories;

namespace Procurement.Erp
{
    /// <summary>
    /// IErpConnector backed by real MAX purchase orders (Order_Master/Part_Master, via
    /// MaxOrderRepository) instead of only the manually-entered test data from §8.1.
    ///
    /// GetOpenPurchaseRequestsAsync syncs MAX orders into this app's own PurchaseRequest/
    /// PurchaseRequestLine tables (insert-if-not-exists by ErpRequestNumber = Order_Master's
    /// order number) rather than returning them directly — everything downstream
    /// (ProcurementEngine, approval, order placement) already works entirely in terms of our own
    /// PurchaseRequest.Id, so syncing first keeps that unchanged instead of forking the workflow
    /// for a second "kind" of purchase request. Workflow-relevant fields on an existing row are
    /// never updated after the first sync (MVP scope) — if a MAX order's quantity/status/etc.
    /// changes later, that's not reflected here yet. The purely-informational MAX mirror fields
    /// (see ApplyDisplayOnlyFields) are the one exception: those refresh on every sync, since
    /// nothing downstream reads them and refreshing keeps MainForm's grid showing current MAX
    /// values instead of whatever was captured the first time that order was seen.
    ///
    /// CreatePurchaseOrderAsync/UpdatePurchaseOrderStatusAsync (spec §6 step 9) delegate to
    /// MockErpConnector by default (UseMockPurchaseOrders=true — this prototype's own
    /// PurchaseOrder tracking) or to MaxPurchaseOrderRepository once that's implemented and the
    /// flag is flipped to false — see that class's comment for why it isn't yet.
    /// </summary>
    public class MaxErpConnector : IErpConnector
    {
        private readonly PurchaseRequestRepository _purchaseRequestRepository;
        private readonly MaxOrderRepository _maxOrderRepository;
        private readonly MaxVendorPartRepository _maxVendorPartRepository;
        private readonly SupplierRepository _supplierRepository;
        private readonly SupplierProductMappingRepository _supplierProductMappingRepository;
        private readonly MaxPurchaseOrderRepository _maxPurchaseOrderRepository;
        private readonly MockErpConnector _inner;

        /// <summary>MAX Order_Master.STATUS_10 values to include — "1" = Planned, "2" = Approved. Defaults to Approved only; the UI's status checkboxes update this.</summary>
        public HashSet<string> IncludedOrderStatuses { get; set; } = new HashSet<string> { "2" };

        /// <summary>
        /// Whether "Order plaatsen" writes to this app's own Procurement_PurchaseOrder table (true,
        /// the default and only currently-verified option) or attempts a real MAX PO via
        /// MaxPurchaseOrderRepository (false).
        ///
        /// DO NOT flip this to false yet. MaxPurchaseOrderRepository's PO header creation
        /// (AddPOHeading) is implemented from a working example, but line creation (AddPODetail)
        /// and — critically — how a line actually gets linked to the header it's meant to belong to
        /// are still unconfirmed (see that class's comment for the exact open questions). Flipping
        /// this now would call real MAX API methods against the live administration with that
        /// linkage unverified, which is exactly the "silently wrong data in a shared ERP" risk this
        /// flag exists to prevent.
        /// </summary>
        public bool UseMockPurchaseOrders { get; set; } = true;

        /// <summary>Optional Order_Master.CURDUE_10 range — set from MainForm's "Due Date Range" group box (only applied when its Enable checkbox is checked).</summary>
        public DateTime? DueDateFilterStart { get; set; }
        public DateTime? DueDateFilterEnd { get; set; }

        /// <summary>Optional "Select By" range filter — set from MainForm's "Filter" group box.</summary>
        public MaxOrderRangeField? RangeFilterField { get; set; }
        public string RangeFilterStart { get; set; }
        public string RangeFilterEnd { get; set; }

        /// <summary>How many rows the *last* GetOpenPurchaseRequestsAsync call's MAX query itself returned, before any local merging — lets MainForm show this separately from the total row count shown, so a "the filter does nothing" report can be diagnosed without a debugger: if this number doesn't shrink when a filter is applied, the SQL-level filter (MaxOrderRepository) is the place to look; if it does shrink but the grid still shows more rows than that, the local-merge logic below is the place to look.</summary>
        public int LastMaxOrderCount { get; private set; }

        public MaxErpConnector(
            PurchaseRequestRepository purchaseRequestRepository,
            PurchaseOrderRepository purchaseOrderRepository,
            MaxOrderRepository maxOrderRepository,
            MaxVendorPartRepository maxVendorPartRepository,
            SupplierRepository supplierRepository,
            SupplierProductMappingRepository supplierProductMappingRepository,
            MaxPurchaseOrderRepository maxPurchaseOrderRepository)
        {
            _purchaseRequestRepository = purchaseRequestRepository ?? throw new ArgumentNullException(nameof(purchaseRequestRepository));
            _maxOrderRepository = maxOrderRepository ?? throw new ArgumentNullException(nameof(maxOrderRepository));
            _maxVendorPartRepository = maxVendorPartRepository ?? throw new ArgumentNullException(nameof(maxVendorPartRepository));
            _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
            _supplierProductMappingRepository = supplierProductMappingRepository ?? throw new ArgumentNullException(nameof(supplierProductMappingRepository));
            _maxPurchaseOrderRepository = maxPurchaseOrderRepository ?? throw new ArgumentNullException(nameof(maxPurchaseOrderRepository));
            _inner = new MockErpConnector(purchaseRequestRepository, purchaseOrderRepository);
        }

        public async Task<IReadOnlyList<PurchaseRequest>> GetOpenPurchaseRequestsAsync()
        {
            var filter = new MaxOrderQueryFilter
            {
                Statuses = IncludedOrderStatuses,
                DueDateStart = DueDateFilterStart,
                DueDateEnd = DueDateFilterEnd,
                RangeField = RangeFilterField,
                RangeStart = RangeFilterStart,
                RangeEnd = RangeFilterEnd
            };
            var maxOrders = await _maxOrderRepository.GetOpenOrdersAsync(filter);
            LastMaxOrderCount = maxOrders.Count;

            foreach (var order in maxOrders)
            {
                var existing = await _purchaseRequestRepository.FindByErpRequestNumberAsync(order.OrderNumber);
                if (existing != null)
                {
                    // These fields were added after some requests were already synced (and the
                    // dedup-by-ErpRequestNumber above otherwise never revisits an existing row),
                    // so without this they'd stay blank forever for anything synced by an older
                    // build. They're purely a MAX mirror for display (MainForm's requests grid) —
                    // nothing in the sourcing/matching/approval logic reads them — so refreshing
                    // them from the latest MAX values on every sync is safe.
                    ApplyDisplayOnlyFields(existing, order);
                    continue;
                }

                // MAX's own data isn't guaranteed to fit our column lengths (e.g. VIEWER_01 isn't
                // reliably a clean manufacturer part number yet — see MaxOrder.ManufacturerPartNumber)
                // — truncate defensively so one oversized/dirty MAX row can't fail EF6 validation
                // and abort the whole sync for every other order in the batch.
                var request = new PurchaseRequest
                {
                    ErpRequestNumber = Truncate(order.OrderNumber, 50),
                    RequiredDate = order.DueDate,
                    Project = Truncate(order.Reference, 100),
                    MaxOrderStatus = Truncate(order.Status, 10),
                    Lines = new List<PurchaseRequestLine>
                    {
                        new PurchaseRequestLine
                        {
                            ErpArticleId = Truncate(order.PartId, 50),
                            ManufacturerPartNumber = Truncate(order.ManufacturerPartNumber, 100),
                            Description = Truncate(order.Description, 500),
                            RequestedQuantity = order.CurrentQty,
                            RequiredDate = order.DueDate,
                            PartType = Truncate(order.PartType, 10),
                            Revision = Truncate(order.Revision, 20),
                            Firm = order.Firm,
                            Cost = order.Cost,
                            CostConv = order.CostConv,
                            Customer = Truncate(order.Customer, 50),
                            StockId = Truncate(order.StockId, 50),
                            Desc1 = Truncate(order.Desc1, 250),
                            Desc2 = Truncate(order.Desc2, 250)
                        }
                    }
                };

                await _purchaseRequestRepository.AddAsync(request);
            }

            await _purchaseRequestRepository.SaveAsync();
            await SyncVendorPartMappingsAsync(maxOrders);

            // GetOpenAsync() returns *every* still-open local request regardless of this call's
            // filter (statuses/Due Date range/Select By) — a request synced by an earlier,
            // unfiltered Query never disappears from local storage just because the current filter
            // no longer matches it. Without this, the Due Date Range / Filter group boxes would
            // visibly do nothing: the grid would keep showing everything ever synced.
            //
            // Manually added test requests should stay visible regardless of the current MAX
            // filter (they were never part of any MAX query result to begin with) — but that check
            // must NOT be "PurchaseRequest.MaxOrderStatus == null", which was the actual bug here:
            // MaxOrderStatus only gets (re)populated for a row when that row is present in the
            // *current* maxOrders batch (see ApplyDisplayOnlyFields above and the AddAsync branch
            // below) — so a MAX-sourced row synced before that column existed, which hasn't
            // happened to match a filtered Query since, still has MaxOrderStatus == null and would
            // incorrectly be treated as "manual, always show" — exactly the bypass that made every
            // filter look like it did nothing, and also why its Desc1/Desc2/etc. stayed blank (same
            // never-refreshed row). NewPurchaseRequestForm's "TEST-" ErpRequestNumber prefix is set
            // once at creation and never depends on being refreshed later, so it doesn't have this
            // problem.
            var matchedOrderNumbers = new HashSet<string>(maxOrders.Select(o => Truncate(o.OrderNumber, 50)));
            var openRequests = await _purchaseRequestRepository.GetOpenAsync();
            return openRequests
                .Where(r => IsManualTestRequest(r.ErpRequestNumber) || matchedOrderNumbers.Contains(r.ErpRequestNumber))
                .ToList();
        }

        private static bool IsManualTestRequest(string erpRequestNumber) =>
            !string.IsNullOrEmpty(erpRequestNumber) && erpRequestNumber.StartsWith("TEST-", StringComparison.Ordinal);

        private static void ApplyDisplayOnlyFields(PurchaseRequest existing, MaxOrder order)
        {
            existing.MaxOrderStatus = Truncate(order.Status, 10);

            var line = existing.Lines.FirstOrDefault();
            if (line == null) return;

            line.PartType = Truncate(order.PartType, 10);
            line.Revision = Truncate(order.Revision, 20);
            line.Firm = order.Firm;
            line.Cost = order.Cost;
            line.CostConv = order.CostConv;
            line.Customer = Truncate(order.Customer, 50);
            line.StockId = Truncate(order.StockId, 50);
            line.Desc1 = Truncate(order.Desc1, 250);
            line.Desc2 = Truncate(order.Desc2, 250);
            // Description is Desc1+Desc2 joined at the time MAX was queried — refreshed here too
            // (unlike other workflow fields) so the padded-space bug fixed in MaxOrderRepository
            // actually clears for rows synced by an older build, instead of keeping the stale,
            // un-trimmed text forever.
            line.Description = Truncate(order.Description, 500);
            // Same reasoning applies to these two: a row synced before the trailing-period cleanup
            // (MaxOrderRepository.CleanCodeField) was added kept showing "5100565..." forever,
            // because ErpArticleId/ManufacturerPartNumber were originally treated as
            // "workflow-relevant, set once at creation" — but they're really just a MAX mirror same
            // as everything else in this method, so they belong here too.
            line.ErpArticleId = Truncate(order.PartId, 50);
            line.ManufacturerPartNumber = Truncate(order.ManufacturerPartNumber, 100);
        }

        /// <summary>
        /// Seeds SupplierProductMapping with Verified confidence from MAX's own Part_Vendor
        /// cross-reference (known article-number -> supplier-part-code links maintained in MAX),
        /// so ProcurementEngine.ResolveOneMappingAsync can skip fuzzy adapter matching entirely for
        /// these lines instead of always falling back to manual approval (see
        /// MaxOrder.ManufacturerPartNumber's TODO). Scoped to only the parts in the current
        /// open-orders batch, and never overwrites an existing mapping — a manual correction made
        /// via the Supplier-mapping screen (§8.5) always wins over a re-sync.
        /// </summary>
        private async Task SyncVendorPartMappingsAsync(IReadOnlyList<MaxOrder> maxOrders)
        {
            var partIds = maxOrders.Select(o => o.PartId).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
            if (partIds.Count == 0) return;

            var suppliers = await _supplierRepository.GetAllAsync();
            var vendorIdToSupplierCode = suppliers
                .Where(s => !string.IsNullOrEmpty(s.VendorId))
                .ToDictionary(s => s.VendorId, s => s.SupplierCode);
            if (vendorIdToSupplierCode.Count == 0) return;

            var vendorParts = await _maxVendorPartRepository.GetForPartsAsync(partIds);

            foreach (var vendorPart in vendorParts)
            {
                if (string.IsNullOrEmpty(vendorPart.VendorId) || !vendorIdToSupplierCode.TryGetValue(vendorPart.VendorId, out var supplierCode))
                    continue;

                var erpArticleId = Truncate(vendorPart.PartId, 50);
                var existing = await _supplierProductMappingRepository.FindAsync(erpArticleId, supplierCode);
                if (existing != null) continue;

                await _supplierProductMappingRepository.AddAsync(new SupplierProductMapping
                {
                    ErpArticleId = erpArticleId,
                    SupplierCode = supplierCode,
                    SupplierPartNumber = Truncate(vendorPart.VendorPart, 100),
                    MatchConfidence = MatchConfidence.Verified
                });
            }
        }

        public Task<string> CreatePurchaseOrderAsync(PurchaseOrderDraft draft) => UseMockPurchaseOrders
            ? _inner.CreatePurchaseOrderAsync(draft)
            : _maxPurchaseOrderRepository.CreatePurchaseOrderAsync(draft);

        public Task UpdatePurchaseOrderStatusAsync(string erpPoNumber, string status) => UseMockPurchaseOrders
            ? _inner.UpdatePurchaseOrderStatusAsync(erpPoNumber, status)
            : _maxPurchaseOrderRepository.UpdateStatusAsync(erpPoNumber, status);

        private static string Truncate(string value, int maxLength) =>
            string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}
