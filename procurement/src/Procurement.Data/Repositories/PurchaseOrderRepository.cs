using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;

namespace Procurement.Data.Repositories
{
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

        public Task<IReadOnlyList<PurchaseOrder>> GetByPurchaseRequestIdAsync(int purchaseRequestId) => _context.RunGuardedAsync(async () =>
        {
            IReadOnlyList<PurchaseOrder> result = await _context.PurchaseOrders
                .Include(po => po.Lines)
                .Where(po => po.PurchaseRequestId == purchaseRequestId)
                .ToListAsync();
            return result;
        });

        public Task<PurchaseOrder> GetByIdAsync(int id) => _context.RunGuardedAsync(() =>
            _context.PurchaseOrders
                .Include(po => po.Lines)
                .FirstOrDefaultAsync(po => po.Id == id));

        public Task UpdateStatusByErpPoNumberAsync(string erpPoNumber, Procurement.Core.Enums.PurchaseOrderStatus status) => _context.RunGuardedAsync(async () =>
        {
            var order = await _context.PurchaseOrders.FirstOrDefaultAsync(po => po.ErpPoNumber == erpPoNumber);
            if (order == null) return;
            order.Status = status;
            await _context.SaveChangesAsync();
        });
    }
}
