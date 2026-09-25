using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Infrastructure.Annotations;
using System.Data.Entity.ModelConfiguration;
using Procurement.Core.Entities;

namespace Procurement.Data.Configurations
{
    public class SupplierConfiguration : EntityTypeConfiguration<Supplier>
    {
        public SupplierConfiguration()
        {
            ToTable("Procurement_Supplier");
            HasKey(s => s.Id);

            Property(s => s.SupplierCode).HasMaxLength(20).IsRequired()
                .HasColumnAnnotation(IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_Supplier_SupplierCode") { IsUnique = true }));
            Property(s => s.Name).HasMaxLength(200);
            Property(s => s.VendorId).HasMaxLength(20);

            Property(s => s.AccountId).HasMaxLength(50);
            Property(s => s.ContactName).HasMaxLength(200);
            Property(s => s.ContactEmail).HasMaxLength(200);
            Property(s => s.ContactTelephone).HasMaxLength(50);
            Property(s => s.AddressLine1).HasMaxLength(100);
            Property(s => s.AddressLine2).HasMaxLength(100);
            Property(s => s.City).HasMaxLength(100);
            Property(s => s.Province).HasMaxLength(50);
            Property(s => s.PostalCode).HasMaxLength(20);
            // DigiKey's own Address schema caps this at 2 (ISO country code) — kept generic-length
            // for other suppliers, but 2 already covers the confirmed case.
            Property(s => s.CountryCode).HasMaxLength(2);

            // Base64(IV + AES-256 ciphertext) is comfortably longer than the plaintext it replaces —
            // 500 leaves headroom for any credential length SecretProtector produces.
            Property(s => s.ClientIdEncrypted).HasMaxLength(500);
            Property(s => s.ClientSecretEncrypted).HasMaxLength(500);
            Property(s => s.ApiKeyEncrypted).HasMaxLength(500);
            Property(s => s.RefreshTokenEncrypted).HasMaxLength(1000);

            HasMany(s => s.Capabilities)
                .WithRequired(c => c.Supplier)
                .HasForeignKey(c => c.SupplierId)
                .WillCascadeOnDelete(true);
        }
    }

    public class SupplierCapabilityRecordConfiguration : EntityTypeConfiguration<SupplierCapabilityRecord>
    {
        public SupplierCapabilityRecordConfiguration()
        {
            ToTable("Procurement_SupplierCapability");
            HasKey(c => c.Id);

            Property(c => c.Capability).HasMaxLength(50).IsRequired();
        }
    }
}
