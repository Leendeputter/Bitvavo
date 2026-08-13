using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;

namespace Procurement.Data.Repositories
{
    public class SupplierSelectionRepository
    {
        private readonly ProcurementDbContext _context;

        public SupplierSelectionRepository(ProcurementDbContext context)
        {
            _context = context;
        }

        public Task<SupplierSelection> AddAsync(SupplierSelection selection) => _context.RunGuardedAsync(async () =>
        {
            _context.SupplierSelections.Add(selection);
            await _context.SaveChangesAsync();
            return selection;
        });

        public Task<SupplierSelection> GetByLineIdAsync(int purchaseRequestLineId) => _context.RunGuardedAsync(() =>
            _context.SupplierSelections
                .Include(s => s.SelectedOffer)
                .Where(s => s.PurchaseRequestLineId == purchaseRequestLineId)
                .OrderByDescending(s => s.SelectedAt)
                .FirstOrDefaultAsync());
    }
}
