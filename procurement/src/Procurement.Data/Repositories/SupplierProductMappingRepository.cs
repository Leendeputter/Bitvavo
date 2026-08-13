using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;
using Procurement.Core.Enums;

namespace Procurement.Data.Repositories
{
    public class SupplierProductMappingRepository
    {
        private readonly ProcurementDbContext _context;

        public SupplierProductMappingRepository(ProcurementDbContext context)
        {
            _context = context;
        }

        public Task<SupplierProductMapping> FindAsync(string erpArticleId, string supplierCode)
        {
            if (string.IsNullOrEmpty(erpArticleId)) return Task.FromResult<SupplierProductMapping>(null);

            return _context.RunGuardedAsync(() =>
                _context.SupplierProductMappings
                    .FirstOrDefaultAsync(m => m.ErpArticleId == erpArticleId && m.SupplierCode == supplierCode));
        }

        public Task<IReadOnlyList<SupplierProductMapping>> GetAllAsync(MatchConfidence? filter = null) => _context.RunGuardedAsync(async () =>
        {
            var query = _context.SupplierProductMappings.AsQueryable();
            if (filter.HasValue)
                query = query.Where(m => m.MatchConfidence == filter.Value);

            IReadOnlyList<SupplierProductMapping> result = await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
            return result;
        });

        public Task<SupplierProductMapping> AddAsync(SupplierProductMapping mapping) => _context.RunGuardedAsync(async () =>
        {
            _context.SupplierProductMappings.Add(mapping);
            await _context.SaveChangesAsync();
            return mapping;
        });

        public Task UpdateConfidenceAsync(int id, MatchConfidence confidence) => _context.RunGuardedAsync(async () =>
        {
            var mapping = await _context.SupplierProductMappings.FindAsync(id);
            if (mapping == null) return;
            mapping.MatchConfidence = confidence;
            await _context.SaveChangesAsync();
        });
    }
}
