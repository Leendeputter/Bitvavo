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
    /// Spec §4: implements IErpConnector on top of this prototype's own SQL Server database
    /// instead of real UniPro tables. Test purchase requests are entered through the UI
    /// (§8.1) and treated by the rest of the engine exactly as if they came from the real ERP.
    /// Swapping this out for a UniProErpConnector later is the only change needed to go live.
    /// </summary>
    public class MockErpConnector : IErpConnector
    {
        private readonly PurchaseRequestRepository _purchaseRequestRepository;
        private readonly PurchaseOrderRepository _purchaseOrderRepository;

        public MockErpConnector(PurchaseRequestRepository purchaseRequestRepository, PurchaseOrderRepository purchaseOrderRepository)
        {
            _purchaseRequestRepository = purchaseRequestRepository ?? throw new ArgumentNullException(nameof(purchaseRequestRepository));
            _purchaseOrderRepository = purchaseOrderRepository ?? throw new ArgumentNullException(nameof(purchaseOrderRepository));
        }

        public async Task<IReadOnlyList<PurchaseRequest>> GetOpenPurchaseRequestsAsync()
        {
            return await _purchaseRequestRepository.GetOpenAsync();
        }

        public async Task<string> CreatePurchaseOrderAsync(PurchaseOrderDraft draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));

            var erpPoNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant()}";

            var order = new PurchaseOrder
            {
                ErpPoNumber = erpPoNumber,
                SupplierCode = draft.SupplierCode,
                Currency = draft.Currency ?? "EUR",
                IdempotencyKey = draft.IdempotencyKey,
                Status = PurchaseOrderStatus.Submitted,
                OrderTotal = draft.Lines.Sum(l => l.LineTotal),
                Lines = draft.Lines.Select(l => new PurchaseOrderLine
                {
                    PurchaseRequestLineId = l.PurchaseRequestLineId,
                    SupplierPartNumber = l.SupplierPartNumber,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    LineTotal = l.LineTotal
                }).ToList()
            };

            await _purchaseOrderRepository.AddAsync(order);
            return erpPoNumber;
        }

        public async Task UpdatePurchaseOrderStatusAsync(string erpPoNumber, string status)
        {
            if (string.IsNullOrWhiteSpace(erpPoNumber)) throw new ArgumentNullException(nameof(erpPoNumber));

            if (!Enum.TryParse<PurchaseOrderStatus>(status, ignoreCase: true, out var parsedStatus))
                throw new ArgumentException($"Unknown purchase order status '{status}'.", nameof(status));

            await _purchaseOrderRepository.UpdateStatusByErpPoNumberAsync(erpPoNumber, parsedStatus);
        }
    }
}
