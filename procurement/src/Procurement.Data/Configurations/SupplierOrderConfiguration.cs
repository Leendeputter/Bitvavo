using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Infrastructure.Annotations;
using System.Data.Entity.ModelConfiguration;
using Procurement.Core.Entities;

namespace Procurement.Data.Configurations
{
    public class SupplierOrderConfiguration : EntityTypeConfiguration<SupplierOrder>
    {
        public SupplierOrderConfiguration()
        {
            ToTable("Procurement_SupplierOrder");
            HasKey(o => o.Id);

            Property(o => o.ErpPoNumber).HasMaxLength(50).IsRequired();
            Property(o => o.SupplierCode).HasMaxLength(20).IsRequired();
            Property(o => o.SupplierOrderNumber).HasMaxLength(100);
            Property(o => o.IdempotencyKey).HasMaxLength(100).IsRequired();
            Property(o => o.Currency).HasMaxLength(3);

            HasRequired(o => o.ErpPo)
                .WithMany()
                .HasForeignKey(o => o.ErpPoId)
                .WillCascadeOnDelete(false);

            HasMany(o => o.Lines)
                .WithRequired(l => l.SupplierOrder)
                .HasForeignKey(l => l.SupplierOrderId)
                .WillCascadeOnDelete(true);

            // Spec §10: ErpPoNumber + SupplierCode + OrderVersion must be unique — a retried
            // submission after a timeout re-uses the same idempotency key instead of duplicating.
            Property(o => o.ErpPoNumber)
                .HasColumnAnnotation(IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_SupplierOrder_Po_Supplier_Version", 1) { IsUnique = true }));
            Property(o => o.SupplierCode)
                .HasColumnAnnotation(IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_SupplierOrder_Po_Supplier_Version", 2) { IsUnique = true }));
            Property(o => o.OrderVersion)
                .HasColumnAnnotation(IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_SupplierOrder_Po_Supplier_Version", 3) { IsUnique = true }));

            Property(o => o.IdempotencyKey)
                .HasColumnAnnotation(IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_SupplierOrder_IdempotencyKey") { IsUnique = true }));
        }
    }

    public class SupplierOrderLineConfiguration : EntityTypeConfiguration<SupplierOrderLine>
    {
        public SupplierOrderLineConfiguration()
        {
            ToTable("Procurement_SupplierOrderLine");
            HasKey(l => l.Id);

            Property(l => l.SupplierPartNumber).HasMaxLength(100);
        }
    }
}
