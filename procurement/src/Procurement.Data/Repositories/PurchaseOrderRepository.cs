using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Procurement.Core.Models;

namespace Procurement.Data.Repositories
{
    /// <summary>
    /// One repository for the merged PurchaseOrder entity — replaces what used to be
    /// PurchaseOrderRepository + SupplierOrderRepository (spec correction, aug 2026: a PO always has
    /// exactly one supplier, same as MAX's own PurchaseOrder concept — see PurchaseOrder.cs).
    /// </summary>
    public class PurchaseOrderRepository
    {
        private readonly ProcurementDbContext _context;

        public PurchaseOrderRepository(ProcurementDbContext context)
        {
            _context = context;
        }

        public Task<PurchaseOrder> AddAsync(PurchaseOrder order) => _context.RunGuardedAsync(async () =>
        {
            _context.PurchaseOrders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        });

        public Task<PurchaseOrder> GetByErpPoNumberAsync(string erpPoNumber) => _context.RunGuardedAsync(() =>
            _context.PurchaseOrders
                .Include(po => po.Lines)
                .FirstOrDefaultAsync(po => po.ErpPoNumber == erpPoNumber));

        public Task<PurchaseOrder> GetByIdAsync(int id) => _context.RunGuardedAsync(() =>
            _context.PurchaseOrders
                .Include(po => po.Lines)
                .FirstOrDefaultAsync(po => po.Id == id));

        /// <summary>POs relevant to a purchase request — found by joining on which of that request's line IDs ended up on a PurchaseOrderLine, since a PO can now span lines from several requests (grouped by supplier, not by request — see PurchaseOrder.cs).</summary>
        public Task<IReadOnlyList<PurchaseOrder>> GetByPurchaseRequestLineIdsAsync(IReadOnlyList<int> purchaseRequestLineIds) => _context.RunGuardedAsync(async () =>
        {
            IReadOnlyList<PurchaseOrder> result = await _context.PurchaseOrders
                .Include(po => po.Lines)
                .Where(po => po.Lines.Any(l => purchaseRequestLineIds.Contains(l.PurchaseRequestLineId)))
                .ToListAsync();
            return result;
        });

        /// <summary>
        /// Whether a purchase request line has already been placed on a PO — used to exclude
        /// already-ordered lines from a new "Order plaatsen" batch (ProcurementEngine.PlaceOrdersAsync).
        /// Note: this goes true the moment the ERP PO row is created, before the supplier-side
        /// submission is attempted — if that submission then throws, the line still counts as
        /// ordered rather than being retried automatically here. That mirrors how the pre-merge
        /// order-placement path never retried a failed supplier submission either; a stuck PO is
        /// visible via Order Details (Status stays Created).
        /// </summary>
        public Task<bool> HasOrderForLineAsync(int purchaseRequestLineId) => _context.RunGuardedAsync(() =>
            _context.PurchaseOrderLines.AnyAsync(l => l.PurchaseRequestLineId == purchaseRequestLineId));

        public Task UpdateStatusByErpPoNumberAsync(string erpPoNumber, PurchaseOrderStatus status) => _context.RunGuardedAsync(async () =>
        {
            var order = await _context.PurchaseOrders.FirstOrDefaultAsync(po => po.ErpPoNumber == erpPoNumber);
            if (order == null) return;
            order.Status = status;
            await _context.SaveChangesAsync();
        });

        public Task UpdateStatusAsync(int id, PurchaseOrderStatus status, DateTime? confirmedAt = null) => _context.RunGuardedAsync(async () =>
        {
            var order = await _context.PurchaseOrders.FindAsync(id);
            if (order == null) return;
            order.Status = status;
            if (confirmedAt.HasValue) order.ConfirmedAt = confirmedAt;
            await _context.SaveChangesAsync();
        });

        /// <summary>Applies the result of adapter.CreateOrderAsync to the PO row created just before it (spec §10, now on PurchaseOrder itself).</summary>
        public Task ApplySupplierOrderResultAsync(int purchaseOrderId, SupplierOrderResult result) => _context.RunGuardedAsync(async () =>
        {
            var order = await _context.PurchaseOrders.FindAsync(purchaseOrderId);
            if (order == null) return;
            order.SupplierOrderNumber = result.SupplierOrderNumber;
            order.Status = result.Status;
            order.SubmittedAt = result.SubmittedAt;
            order.OrderTotal = result.OrderTotal;
            order.Currency = result.Currency;
            await _context.SaveChangesAsync();
        });

        /// <summary>Running per-year sequence number used in the idempotency key format PROC-{jaar}-{volgnummer}-{SUPPLIERCODE} (spec §10).</summary>
        public Task<int> GetNextSequenceForYearAsync(int year) => _context.RunGuardedAsync(async () =>
        {
            var count = await _context.PurchaseOrders.CountAsync(o => o.CreatedAt.Year == year);
            return count + 1;
        });
    }
}
