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

        public async Task<SupplierSelection> AddAsync(SupplierSelection selection)
        {
            _context.SupplierSelections.Add(selection);
            await _context.SaveChangesAsync();
            return selection;
        }

        public async Task<SupplierSelection> GetByLineIdAsync(int purchaseRequestLineId)
        {
            return await _context.SupplierSelections
                .Include(s => s.SelectedOffer)
                .Where(s => s.PurchaseRequestLineId == purchaseRequestLineId)
                .OrderByDescending(s => s.SelectedAt)
                .FirstOrDefaultAsync();
        }
    }
}
