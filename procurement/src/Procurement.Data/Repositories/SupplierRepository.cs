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
        /// Updates the account/shipping-contact fields a real Ordering API call needs (see
        /// Supplier.cs) via Instellingen's "Verzendgegevens bewerken" dialog. Unlike
        /// the supplier credentials (Beheer > App Secrets) this isn't write-only: none of these are secrets, so the dialog
        /// pre-fills the current values and every parameter here always overwrites (an intentionally
        /// cleared field arrives as "", not null, and is saved as such — there's no "leave unchanged"
        /// convention to preserve here the way there is for credentials).
        /// </summary>
        public Task UpdateOrderingContactAsync(
            int id, string accountId, string contactName, string contactEmail, string contactTelephone,
            string addressLine1, string addressLine2, string city, string province, string postalCode, string countryCode) =>
            _context.RunGuardedAsync(async () =>
            {
                var supplier = await _context.Suppliers.FindAsync(id);
                if (supplier == null) return;
                supplier.AccountId = accountId;
                supplier.ContactName = contactName;
                supplier.ContactEmail = contactEmail;
                supplier.ContactTelephone = contactTelephone;
                supplier.AddressLine1 = addressLine1;
                supplier.AddressLine2 = addressLine2;
                supplier.City = city;
                supplier.Province = province;
                supplier.PostalCode = postalCode;
                supplier.CountryCode = countryCode;
                await _context.SaveChangesAsync();
            });
    }
}
