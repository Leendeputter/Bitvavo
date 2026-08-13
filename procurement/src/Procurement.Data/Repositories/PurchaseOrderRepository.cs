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

        public async Task<PurchaseOrder> AddAsync(PurchaseOrder order)
        {
            _context.PurchaseOrders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task<PurchaseOrder> GetByErpPoNumberAsync(string erpPoNumber)
        {
            return await _context.PurchaseOrders
                .Include(po => po.Lines)
                .FirstOrDefaultAsync(po => po.ErpPoNumber == erpPoNumber);
        }

        public async Task<IReadOnlyList<PurchaseOrder>> GetByPurchaseRequestIdAsync(int purchaseRequestId)
        {
            return await _context.PurchaseOrders
                .Include(po => po.Lines)
                .Where(po => po.PurchaseRequestId == purchaseRequestId)
                .ToListAsync();
        }

        public async Task<PurchaseOrder> GetByIdAsync(int id)
        {
            return await _context.PurchaseOrders
                .Include(po => po.Lines)
                .FirstOrDefaultAsync(po => po.Id == id);
        }

        public async Task UpdateStatusByErpPoNumberAsync(string erpPoNumber, Procurement.Core.Enums.PurchaseOrderStatus status)
        {
            var order = await _context.PurchaseOrders.FirstOrDefaultAsync(po => po.ErpPoNumber == erpPoNumber);
            if (order == null) return;
            order.Status = status;
            await _context.SaveChangesAsync();
        }
    }
}
