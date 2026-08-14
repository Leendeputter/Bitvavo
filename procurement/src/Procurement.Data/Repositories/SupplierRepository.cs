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

        /// <summary>Updates VendorId/IsSandbox/UseMockData — the fields editable directly in the Instellingen "Suppliers" grid. Suppliers themselves are only ever created by the seed, not from the UI.</summary>
        public Task UpdateSettingsAsync(int id, string vendorId, bool isSandbox, bool useMockData) => _context.RunGuardedAsync(async () =>
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return;
            supplier.VendorId = vendorId;
            supplier.IsSandbox = isSandbox;
            supplier.UseMockData = useMockData;
            await _context.SaveChangesAsync();
        });

        /// <summary>
        /// Updates API credentials via the separate "Credentials bewerken" dialog (never the main
        /// grid, which never shows a decrypted secret). Each parameter is write-only and optional —
        /// a null/empty value leaves that credential unchanged, since the dialog never displays an
        /// existing stored value back (there'd be nothing meaningful to show without decrypting it
        /// onto the screen, which defeats the point) — so "leave the box empty" is how you keep the
        /// current value, and typing something new is the only way to change or (by design) not
        /// clear one; clearing a credential isn't supported from the UI yet.
        /// </summary>
        public Task UpdateCredentialsAsync(int id, string clientId, string clientSecret, string apiKey) => _context.RunGuardedAsync(async () =>
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return;
            if (!string.IsNullOrEmpty(clientId)) supplier.ClientId = clientId;
            if (!string.IsNullOrEmpty(clientSecret)) supplier.ClientSecret = clientSecret;
            if (!string.IsNullOrEmpty(apiKey)) supplier.ApiKey = apiKey;
            await _context.SaveChangesAsync();
        });
    }
}
