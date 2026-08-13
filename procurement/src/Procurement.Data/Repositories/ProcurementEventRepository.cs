using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;

namespace Procurement.Data.Repositories
{
    /// <summary>Read side of the audit trail for the Audit-log viewer (spec §8.7).</summary>
    public class ProcurementEventRepository
    {
        private readonly ProcurementDbContext _context;

        public ProcurementEventRepository(ProcurementDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<ProcurementEvent>> SearchAsync(
            string entityType = null,
            string eventType = null,
            string supplierCode = null,
            DateTime? from = null,
            DateTime? to = null)
        {
            var query = _context.ProcurementEvents.AsQueryable();

            if (!string.IsNullOrWhiteSpace(entityType))
                query = query.Where(e => e.EntityType == entityType);
            if (!string.IsNullOrWhiteSpace(eventType))
                query = query.Where(e => e.EventType == eventType);
            if (!string.IsNullOrWhiteSpace(supplierCode))
                query = query.Where(e => e.SupplierCode == supplierCode);
            if (from.HasValue)
                query = query.Where(e => e.Timestamp >= from.Value);
            if (to.HasValue)
                query = query.Where(e => e.Timestamp <= to.Value);

            return await query.OrderByDescending(e => e.Timestamp).Take(1000).ToListAsync();
        }
    }
}
