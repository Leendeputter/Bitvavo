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

        /// <summary>"ErpArticleId|SupplierCode" of every existing mapping, for a bulk "already mapped?" check.</summary>
        public Task<HashSet<string>> GetExistingKeysAsync() => _context.RunGuardedAsync(async () =>
        {
            var keys = await _context.SupplierProductMappings
                .Select(m => new { m.ErpArticleId, m.SupplierCode })
                .ToListAsync();
            return new HashSet<string>(keys.Select(k => Key(k.ErpArticleId, k.SupplierCode)), System.StringComparer.OrdinalIgnoreCase);
        });

        public static string Key(string erpArticleId, string supplierCode) => erpArticleId + "|" + supplierCode;

        /// <summary>Adds several mappings with a single SaveChanges.</summary>
        public Task AddRangeAsync(IReadOnlyCollection<SupplierProductMapping> mappings) => _context.RunGuardedAsync(async () =>
        {
            if (mappings.Count == 0) return;
            _context.SupplierProductMappings.AddRange(mappings);
            await _context.SaveChangesAsync();
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

        /// <summary>Another order code typed in Supplier Mapping (24 nov 2026); a code chosen by hand counts as Verified.</summary>
        public Task UpdatePartNumberAsync(int id, string supplierPartNumber) => _context.RunGuardedAsync(async () =>
        {
            var mapping = await _context.SupplierProductMappings.FindAsync(id);
            if (mapping == null) return;
            mapping.SupplierPartNumber = supplierPartNumber;
            mapping.MatchConfidence = MatchConfidence.Verified;
            await _context.SaveChangesAsync();
        });

        /// <summary>
        /// Overwrites an existing mapping's found-product fields in place — used by
        /// ProcurementEngine.ResolveOneMappingAsync to self-heal a mapping that was previously cached
        /// with a blank SupplierPartNumber (which blocks every future offer for that article/supplier
        /// — see EnsureKnownPart/"SupplierPartNumber is required" downstream) once a fresh search
        /// actually finds a usable part number. Updates in place (never AddAsync here) because
        /// (ErpArticleId, SupplierCode) has a unique index — a second insert for the same pair would
        /// violate it.
        /// </summary>
        public Task UpdateSupplierPartAsync(int id, string supplierPartNumber, string manufacturer, string manufacturerPartNumber, MatchConfidence confidence) => _context.RunGuardedAsync(async () =>
        {
            var mapping = await _context.SupplierProductMappings.FindAsync(id);
            if (mapping == null) return;
            mapping.SupplierPartNumber = supplierPartNumber;
            mapping.Manufacturer = manufacturer;
            mapping.ManufacturerPartNumber = manufacturerPartNumber;
            mapping.MatchConfidence = confidence;
            await _context.SaveChangesAsync();
        });
    }
}
