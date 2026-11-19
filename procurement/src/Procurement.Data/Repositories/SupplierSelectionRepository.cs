using System.Collections.Generic;
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

        /// <summary>
        /// The latest selection per line for many lines at once (19 nov 2026: the requests grid did one
        /// query per row, which made Query slow with many orders). Batches of 1000 ids per query.
        /// </summary>
        public Task<IReadOnlyDictionary<int, SupplierSelection>> GetLatestByLineIdsAsync(IReadOnlyCollection<int> lineIds) => _context.RunGuardedAsync(async () =>
        {
            var result = new Dictionary<int, SupplierSelection>();
            var ids = lineIds.Distinct().ToList();
            for (var offset = 0; offset < ids.Count; offset += 1000)
            {
                var batch = ids.Skip(offset).Take(1000).ToList();
                var selections = await _context.SupplierSelections
                    .Include(s => s.SelectedOffer)
                    .Where(s => batch.Contains(s.PurchaseRequestLineId))
                    .ToListAsync();
                foreach (var group in selections.GroupBy(s => s.PurchaseRequestLineId))
                    result[group.Key] = group.OrderByDescending(s => s.SelectedAt).First();
            }
            return (IReadOnlyDictionary<int, SupplierSelection>)result;
        });

        /// <summary>Clears a line back to "no offer selected" (ProcurementEngine.DeselectOfferAsync) — removes every SupplierSelection row for it, not just the latest, since none of this app's own logic needs a historical trail here (the audit log already has one, separately).</summary>
        public Task DeleteByLineIdAsync(int purchaseRequestLineId) => _context.RunGuardedAsync(async () =>
        {
            var selections = await _context.SupplierSelections
                .Where(s => s.PurchaseRequestLineId == purchaseRequestLineId)
                .ToListAsync();
            _context.SupplierSelections.RemoveRange(selections);
            await _context.SaveChangesAsync();
        });
    }
}
