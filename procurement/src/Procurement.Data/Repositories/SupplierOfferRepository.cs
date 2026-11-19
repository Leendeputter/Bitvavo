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
            // Decided approvals go too (19 nov 2026: after changing the quantity of an approved line the
            // old offers stayed next to the new ones, because their approval still pointed at them).
            // Nothing reads a decided approval; the decision itself stays in the Audit Log
            // (APPROVAL_GRANTED / APPROVAL_REJECTED). Only offers still chosen on a line are kept.
            var approvals = await _context.ApprovalRequests
                .Where(a => a.PurchaseRequestLineId == purchaseRequestLineId
                            || _context.SupplierOffers.Any(o => o.Id == a.ProposedOfferId && o.PurchaseRequestLineId == purchaseRequestLineId))
                .ToListAsync();
            if (approvals.Count > 0)
            {
                _context.ApprovalRequests.RemoveRange(approvals);
                await _context.SaveChangesAsync();
            }

            var offers = await _context.SupplierOffers
                .Include(o => o.PackagingOptions)
                .Where(o => o.PurchaseRequestLineId == purchaseRequestLineId
                            && !_context.SupplierSelections.Any(s => s.SelectedOfferId == o.Id))
                .ToListAsync();
            if (offers.Count == 0) return 0;

            foreach (var offer in offers)
                _context.SupplierOfferPackagingOptions.RemoveRange(offer.PackagingOptions.ToList());
            _context.SupplierOffers.RemoveRange(offers);
            await _context.SaveChangesAsync();
            return offers.Count;
        });

        /// <summary>Every copy of every offer of one bundle (sourcing per article, 18 nov 2026).</summary>
        public Task<IReadOnlyList<SupplierOffer>> GetByBundleKeyAsync(string bundleKey) => _context.RunGuardedAsync(async () =>
        {
            IReadOnlyList<SupplierOffer> result = await _context.SupplierOffers
                .Where(o => o.BundleKey == bundleKey)
                .ToListAsync();
            return result;
        });

        public Task<SupplierOffer> GetByIdAsync(int id) => _context.RunGuardedAsync(() =>
            _context.SupplierOffers
                .Include(o => o.PackagingOptions)
                .FirstOrDefaultAsync(o => o.Id == id));
    }
}
