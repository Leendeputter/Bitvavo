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
    /// for a second "kind" of purchase request. Existing rows are never updated after the first
    /// sync (MVP scope) — if a MAX order's quantity/status/etc. changes later, that's not
    /// reflected here yet.
    ///
    /// CreatePurchaseOrderAsync/UpdatePurchaseOrderStatusAsync are unrelated to MAX (this
    /// prototype's own PurchaseOrder tracking, spec §6 step 9) and are delegated to
    /// MockErpConnector rather than duplicated.
    /// </summary>
    public class MaxErpConnector : IErpConnector
    {
        private readonly PurchaseRequestRepository _purchaseRequestRepository;
        private readonly MaxOrderRepository _maxOrderRepository;
        private readonly MaxVendorPartRepository _maxVendorPartRepository;
        private readonly SupplierRepository _supplierRepository;
        private readonly SupplierProductMappingRepository _supplierProductMappingRepository;
        private readonly MockErpConnector _inner;

        /// <summary>MAX Order_Master.STATUS_10 values to include — "1" = Planned, "2" = Approved. Defaults to Approved only; the UI's status checkboxes update this.</summary>
        public HashSet<string> IncludedOrderStatuses { get; set; } = new HashSet<string> { "2" };

        public MaxErpConnector(
            PurchaseRequestRepository purchaseRequestRepository,
            PurchaseOrderRepository purchaseOrderRepository,
            MaxOrderRepository maxOrderRepository,
            MaxVendorPartRepository maxVendorPartRepository,
            SupplierRepository supplierRepository,
            SupplierProductMappingRepository supplierProductMappingRepository)
        {
            _purchaseRequestRepository = purchaseRequestRepository ?? throw new ArgumentNullException(nameof(purchaseRequestRepository));
            _maxOrderRepository = maxOrderRepository ?? throw new ArgumentNullException(nameof(maxOrderRepository));
            _maxVendorPartRepository = maxVendorPartRepository ?? throw new ArgumentNullException(nameof(maxVendorPartRepository));
            _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
            _supplierProductMappingRepository = supplierProductMappingRepository ?? throw new ArgumentNullException(nameof(supplierProductMappingRepository));
            _inner = new MockErpConnector(purchaseRequestRepository, purchaseOrderRepository);
        }

        public async Task<IReadOnlyList<PurchaseRequest>> GetOpenPurchaseRequestsAsync()
        {
            var maxOrders = await _maxOrderRepository.GetOpenOrdersAsync(IncludedOrderStatuses);

            foreach (var order in maxOrders)
            {
                var existing = await _purchaseRequestRepository.FindByErpRequestNumberAsync(order.OrderNumber);
                if (existing != null) continue;

                // MAX's own data isn't guaranteed to fit our column lengths (e.g. VIEWER_01 isn't
                // reliably a clean manufacturer part number yet — see MaxOrder.ManufacturerPartNumber)
                // — truncate defensively so one oversized/dirty MAX row can't fail EF6 validation
                // and abort the whole sync for every other order in the batch.
                var request = new PurchaseRequest
                {
                    ErpRequestNumber = Truncate(order.OrderNumber, 50),
                    RequiredDate = order.DueDate,
                    Project = Truncate(order.Reference, 100),
                    Lines = new List<PurchaseRequestLine>
                    {
                        new PurchaseRequestLine
                        {
                            ErpArticleId = Truncate(order.PartId, 50),
                            ManufacturerPartNumber = Truncate(order.ManufacturerPartNumber, 100),
                            Description = Truncate(order.Description, 500),
                            RequestedQuantity = order.CurrentQty,
                            RequiredDate = order.DueDate
                        }
                    }
                };

                await _purchaseRequestRepository.AddAsync(request);
            }

            await SyncVendorPartMappingsAsync(maxOrders);

            return await _purchaseRequestRepository.GetOpenAsync();
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

        public Task<string> CreatePurchaseOrderAsync(PurchaseOrderDraft draft) => _inner.CreatePurchaseOrderAsync(draft);

        public Task UpdatePurchaseOrderStatusAsync(string erpPoNumber, string status) => _inner.UpdatePurchaseOrderStatusAsync(erpPoNumber, status);

        private static string Truncate(string value, int maxLength) =>
            string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}
