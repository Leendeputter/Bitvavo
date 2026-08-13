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

        public Task<IReadOnlyList<SupplierOffer>> GetByLineIdAsync(int purchaseRequestLineId) => _context.RunGuardedAsync(async () =>
        {
            IReadOnlyList<SupplierOffer> result = await _context.SupplierOffers
                .Include(o => o.PackagingOptions)
                .Where(o => o.PurchaseRequestLineId == purchaseRequestLineId)
                .OrderBy(o => o.LandedCost)
                .ToListAsync();
            return result;
        });

        public Task AddRangeAsync(IEnumerable<SupplierOffer> offers) => _context.RunGuardedAsync(async () =>
        {
            _context.SupplierOffers.AddRange(offers);
            await _context.SaveChangesAsync();
        });

        public Task<SupplierOffer> GetByIdAsync(int id) => _context.RunGuardedAsync(() =>
            _context.SupplierOffers
                .Include(o => o.PackagingOptions)
                .FirstOrDefaultAsync(o => o.Id == id));
    }
}
