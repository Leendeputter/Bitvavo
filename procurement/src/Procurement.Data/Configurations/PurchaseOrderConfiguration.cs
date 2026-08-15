using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Infrastructure.Annotations;
using System.Data.Entity.ModelConfiguration;
using Procurement.Core.Entities;

namespace Procurement.Data.Configurations
{
    public class PurchaseOrderConfiguration : EntityTypeConfiguration<PurchaseOrder>
    {
        public PurchaseOrderConfiguration()
        {
            ToTable("Procurement_PurchaseOrder");
            HasKey(po => po.Id);

            Property(po => po.ErpPoNumber).HasMaxLength(50).IsRequired();
            Property(po => po.SupplierCode).HasMaxLength(20).IsRequired();
            Property(po => po.SupplierOrderNumber).HasMaxLength(100);
            Property(po => po.IdempotencyKey).HasMaxLength(100).IsRequired();
            Property(po => po.Currency).HasMaxLength(3);

            HasMany(po => po.Lines)
                .WithRequired(l => l.PurchaseOrder)
                .HasForeignKey(l => l.PurchaseOrderId)
                .WillCascadeOnDelete(true);

            // Spec §10, now on the merged entity: a retried submission after a timeout re-uses the
            // same idempotency key instead of duplicating. Always set (see PurchaseOrder.IdempotencyKey),
            // so this can be a straightforward unique index, unlike ErpPoNumber which one supplier's
            // adapter capabilities aside is otherwise the natural key but isn't indexed separately here.
            Property(po => po.IdempotencyKey)
                .HasColumnAnnotation(IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_PurchaseOrder_IdempotencyKey") { IsUnique = true }));
        }
    }

    public class PurchaseOrderLineConfiguration : EntityTypeConfiguration<PurchaseOrderLine>
    {
        public PurchaseOrderLineConfiguration()
        {
            ToTable("Procurement_PurchaseOrderLine");
            HasKey(l => l.Id);

            Property(l => l.SupplierPartNumber).HasMaxLength(100);
            Property(l => l.MaxLineNumber).HasMaxLength(10);
            Property(l => l.MaxDeliveryNumber).HasMaxLength(10);

            HasMany(l => l.Deliveries)
                .WithRequired(d => d.PurchaseOrderLine)
                .HasForeignKey(d => d.PurchaseOrderLineId)
                .WillCascadeOnDelete(true);
        }
    }

    public class PurchaseOrderDeliveryConfiguration : EntityTypeConfiguration<PurchaseOrderDelivery>
    {
        public PurchaseOrderDeliveryConfiguration()
        {
            ToTable("Procurement_PurchaseOrderDelivery");
            HasKey(d => d.Id);

            Property(d => d.TrackingNumber).HasMaxLength(100);
        }
    }
}
