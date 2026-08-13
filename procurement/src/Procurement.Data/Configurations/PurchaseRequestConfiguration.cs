using System.Data.Entity.ModelConfiguration;
using Procurement.Core.Entities;

namespace Procurement.Data.Configurations
{
    public class PurchaseRequestConfiguration : EntityTypeConfiguration<PurchaseRequest>
    {
        public PurchaseRequestConfiguration()
        {
            ToTable("PurchaseRequest");
            HasKey(pr => pr.Id);

            Property(pr => pr.ErpRequestNumber).HasMaxLength(50);
            Property(pr => pr.Warehouse).HasMaxLength(50);
            Property(pr => pr.Project).HasMaxLength(100);

            HasMany(pr => pr.Lines)
                .WithRequired(l => l.PurchaseRequest)
                .HasForeignKey(l => l.PurchaseRequestId)
                .WillCascadeOnDelete(false);
        }
    }

    public class PurchaseRequestLineConfiguration : EntityTypeConfiguration<PurchaseRequestLine>
    {
        public PurchaseRequestLineConfiguration()
        {
            ToTable("PurchaseRequestLine");
            HasKey(l => l.Id);

            Property(l => l.ErpArticleId).HasMaxLength(50);
            Property(l => l.Manufacturer).HasMaxLength(100);
            Property(l => l.ManufacturerPartNumber).HasMaxLength(100);
            Property(l => l.Description).HasMaxLength(500);
            Property(l => l.PreferredSuppliersCsv).HasMaxLength(200).HasColumnName("PreferredSuppliers");
        }
    }
}
