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

        /// <summary>Updates the editable fields (currently just VendorId) of an existing supplier — used by the Instellingen "Suppliers" tab. Suppliers themselves are only ever created by the migration seed, not from the UI.</summary>
        public Task UpdateVendorIdAsync(int id, string vendorId) => _context.RunGuardedAsync(async () =>
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return;
            supplier.VendorId = vendorId;
            await _context.SaveChangesAsync();
        });
    }
}
