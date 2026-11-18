using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;
using Procurement.Core.Enums;

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

        /// <summary>
        /// Before a line is sourced again (user, 18 nov 2026: "Ik heb nu 6 offers staan in plaats van 3
        /// omdat de vorige ook blijven staan"): removes its still-pending approval requests (the new
        /// sourcing makes a new proposal) and every old offer no selection or decided approval points
        /// at. Returns the number of offers removed.
        /// </summary>
        public Task<int> DeleteForResourcingAsync(int purchaseRequestLineId) => _context.RunGuardedAsync(async () =>
        {
            var pendingApprovals = await _context.ApprovalRequests
                .Where(a => a.PurchaseRequestLineId == purchaseRequestLineId && a.Decision == ApprovalDecision.Pending)
                .ToListAsync();
            if (pendingApprovals.Count > 0)
            {
                _context.ApprovalRequests.RemoveRange(pendingApprovals);
                await _context.SaveChangesAsync();
            }

            var offers = await _context.SupplierOffers
                .Include(o => o.PackagingOptions)
                .Where(o => o.PurchaseRequestLineId == purchaseRequestLineId
                            && !_context.SupplierSelections.Any(s => s.SelectedOfferId == o.Id)
                            && !_context.ApprovalRequests.Any(a => a.ProposedOfferId == o.Id))
                .ToListAsync();
            if (offers.Count == 0) return 0;

            foreach (var offer in offers)
                _context.SupplierOfferPackagingOptions.RemoveRange(offer.PackagingOptions.ToList());
            _context.SupplierOffers.RemoveRange(offers);
            await _context.SaveChangesAsync();
            return offers.Count;
        });

        public Task<SupplierOffer> GetByIdAsync(int id) => _context.RunGuardedAsync(() =>
            _context.SupplierOffers
                .Include(o => o.PackagingOptions)
                .FirstOrDefaultAsync(o => o.Id == id));
    }
}
