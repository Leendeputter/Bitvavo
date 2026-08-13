using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;

namespace Procurement.Data.Repositories
{
    public class SupplierRepository
    {
        private readonly ProcurementDbContext _context;

        public SupplierRepository(ProcurementDbContext context)
        {
            _context = context;
        }

        public Task<IReadOnlyList<Supplier>> GetAllAsync() => _context.RunGuardedAsync(async () =>
        {
            IReadOnlyList<Supplier> result = await _context.Suppliers
                .Include(s => s.Capabilities)
                .ToListAsync();
            return result;
        });

        public Task<Supplier> GetByCodeAsync(string supplierCode) => _context.RunGuardedAsync(() =>
            _context.Suppliers
                .Include(s => s.Capabilities)
                .FirstOrDefaultAsync(s => s.SupplierCode == supplierCode));
    }
}
