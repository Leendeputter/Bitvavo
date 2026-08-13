using System.Data.Entity.ModelConfiguration;
using Procurement.Core.Entities;

namespace Procurement.Data.Configurations
{
    public class PurchaseOrderConfiguration : EntityTypeConfiguration<PurchaseOrder>
    {
        public PurchaseOrderConfiguration()
        {
            ToTable("PurchaseOrder");
            HasKey(po => po.Id);

            Property(po => po.ErpPoNumber).HasMaxLength(50).IsRequired();

            HasRequired(po => po.PurchaseRequest)
                .WithMany()
                .HasForeignKey(po => po.PurchaseRequestId)
                .WillCascadeOnDelete(false);

            HasMany(po => po.Lines)
                .WithRequired(l => l.PurchaseOrder)
                .HasForeignKey(l => l.PurchaseOrderId)
                .WillCascadeOnDelete(true);
        }
    }

    public class PurchaseOrderLineConfiguration : EntityTypeConfiguration<PurchaseOrderLine>
    {
        public PurchaseOrderLineConfiguration()
        {
            ToTable("PurchaseOrderLine");
            HasKey(l => l.Id);

            Property(l => l.SupplierCode).HasMaxLength(20);
            Property(l => l.SupplierPartNumber).HasMaxLength(100);
        }
    }
}
