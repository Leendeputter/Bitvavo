using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;
using Procurement.Core.Enums;

namespace Procurement.Data.Repositories
{
    public class SupplierOrderRepository
    {
        private readonly ProcurementDbContext _context;

        public SupplierOrderRepository(ProcurementDbContext context)
        {
            _context = context;
        }

        public async Task<SupplierOrder> FindByIdempotencyKeyAsync(string idempotencyKey)
        {
            return await _context.SupplierOrders
                .Include(o => o.Lines)
                .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey);
        }

        /// <summary>Next OrderVersion for the ErpPoNumber+SupplierCode combination — spec §10 uniqueness key.</summary>
        public async Task<int> GetNextOrderVersionAsync(string erpPoNumber, string supplierCode)
        {
            var existing = await _context.SupplierOrders
                .Where(o => o.ErpPoNumber == erpPoNumber && o.SupplierCode == supplierCode)
                .Select(o => (int?)o.OrderVersion)
                .ToListAsync();

            return existing.Count == 0 ? 1 : existing.Max().GetValueOrDefault() + 1;
        }

        public async Task<SupplierOrder> AddAsync(SupplierOrder order)
        {
            _context.SupplierOrders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task<IReadOnlyList<SupplierOrder>> GetByErpPoIdAsync(int erpPoId)
        {
            return await _context.SupplierOrders
                .Include(o => o.Lines)
                .Where(o => o.ErpPoId == erpPoId)
                .ToListAsync();
        }

        /// <summary>Most recent existing order for a PO+supplier, if any — used to avoid resubmitting on retry (spec §10).</summary>
        public async Task<SupplierOrder> FindByErpPoAndSupplierAsync(string erpPoNumber, string supplierCode)
        {
            return await _context.SupplierOrders
                .Include(o => o.Lines)
                .Where(o => o.ErpPoNumber == erpPoNumber && o.SupplierCode == supplierCode)
                .OrderByDescending(o => o.OrderVersion)
                .FirstOrDefaultAsync();
        }

        /// <summary>Running per-year sequence number used in the idempotency key format PROC-{jaar}-{volgnummer}-{SUPPLIERCODE}.</summary>
        public async Task<int> GetNextSequenceForYearAsync(int year)
        {
            var count = await _context.SupplierOrders.CountAsync(o => o.OrderDate.Year == year);
            return count + 1;
        }

        public async Task UpdateStatusAsync(int id, SupplierOrderStatusEnum status, System.DateTime? confirmedAt = null)
        {
            var order = await _context.SupplierOrders.FindAsync(id);
            if (order == null) return;
            order.Status = status;
            if (confirmedAt.HasValue) order.ConfirmedAt = confirmedAt;
            await _context.SaveChangesAsync();
        }
    }
}
