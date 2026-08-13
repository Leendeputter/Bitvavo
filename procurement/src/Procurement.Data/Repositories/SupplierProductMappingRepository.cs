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

        public async Task<SupplierProductMapping> FindAsync(string erpArticleId, string supplierCode)
        {
            if (string.IsNullOrEmpty(erpArticleId)) return null;

            return await _context.SupplierProductMappings
                .FirstOrDefaultAsync(m => m.ErpArticleId == erpArticleId && m.SupplierCode == supplierCode);
        }

        public async Task<IReadOnlyList<SupplierProductMapping>> GetAllAsync(MatchConfidence? filter = null)
        {
            var query = _context.SupplierProductMappings.AsQueryable();
            if (filter.HasValue)
                query = query.Where(m => m.MatchConfidence == filter.Value);

            return await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
        }

        public async Task<SupplierProductMapping> AddAsync(SupplierProductMapping mapping)
        {
            _context.SupplierProductMappings.Add(mapping);
            await _context.SaveChangesAsync();
            return mapping;
        }

        public async Task UpdateConfidenceAsync(int id, MatchConfidence confidence)
        {
            var mapping = await _context.SupplierProductMappings.FindAsync(id);
            if (mapping == null) return;
            mapping.MatchConfidence = confidence;
            await _context.SaveChangesAsync();
        }
    }
}
