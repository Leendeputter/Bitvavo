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

        public async Task<IReadOnlyList<Supplier>> GetAllAsync()
        {
            return await _context.Suppliers
                .Include(s => s.Capabilities)
                .ToListAsync();
        }

        public async Task<Supplier> GetByCodeAsync(string supplierCode)
        {
            return await _context.Suppliers
                .Include(s => s.Capabilities)
                .FirstOrDefaultAsync(s => s.SupplierCode == supplierCode);
        }
    }
}
