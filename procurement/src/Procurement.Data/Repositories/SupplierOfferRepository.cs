using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;

namespace Procurement.Data.Repositories
{
    public class SupplierOfferRepository
    {
        private readonly ProcurementDbContext _context;

        public SupplierOfferRepository(ProcurementDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<SupplierOffer>> GetByLineIdAsync(int purchaseRequestLineId)
        {
            return await _context.SupplierOffers
                .Include(o => o.PackagingOptions)
                .Where(o => o.PurchaseRequestLineId == purchaseRequestLineId)
                .OrderBy(o => o.LandedCost)
                .ToListAsync();
        }

        public async Task AddRangeAsync(IEnumerable<SupplierOffer> offers)
        {
            _context.SupplierOffers.AddRange(offers);
            await _context.SaveChangesAsync();
        }

        public async Task<SupplierOffer> GetByIdAsync(int id)
        {
            return await _context.SupplierOffers
                .Include(o => o.PackagingOptions)
                .FirstOrDefaultAsync(o => o.Id == id);
        }
    }
}
