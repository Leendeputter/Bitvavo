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

            // Base64(IV + AES-256 ciphertext) is comfortably longer than the plaintext it replaces —
            // 500 leaves headroom for any credential length SecretProtector produces.
            Property(s => s.ClientIdEncrypted).HasMaxLength(500);
            Property(s => s.ClientSecretEncrypted).HasMaxLength(500);
            Property(s => s.ApiKeyEncrypted).HasMaxLength(500);

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
