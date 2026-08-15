using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Infrastructure.Annotations;
using System.Data.Entity.ModelConfiguration;
using Procurement.Core.Entities;

namespace Procurement.Data.Configurations
{
    public class PurchaseRequestConfiguration : EntityTypeConfiguration<PurchaseRequest>
    {
        public PurchaseRequestConfiguration()
        {
            ToTable("Procurement_PurchaseRequest");
            HasKey(pr => pr.Id);

            Property(pr => pr.ErpRequestNumber).HasMaxLength(50);
            Property(pr => pr.Warehouse).HasMaxLength(50);
            Property(pr => pr.Project).HasMaxLength(100);
            Property(pr => pr.MaxOrderStatus).HasMaxLength(10);

            // Every query filters by CompanyId (spec §15 follow-up: data is now scoped per MAX company).
            Property(pr => pr.CompanyId)
                .HasColumnAnnotation(IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_PurchaseRequest_CompanyId")));

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
            ToTable("Procurement_PurchaseRequestLine");
            HasKey(l => l.Id);

            Property(l => l.ErpArticleId).HasMaxLength(50);
            Property(l => l.Manufacturer).HasMaxLength(100);
            Property(l => l.ManufacturerPartNumber).HasMaxLength(100);
            Property(l => l.Description).HasMaxLength(500);
            Property(l => l.PreferredSuppliersCsv).HasMaxLength(200).HasColumnName("PreferredSuppliers");
            Property(l => l.PartType).HasMaxLength(10);
            Property(l => l.Revision).HasMaxLength(20);
            Property(l => l.Customer).HasMaxLength(50);
            Property(l => l.StockId).HasMaxLength(50);
            Property(l => l.Desc1).HasMaxLength(250);
            Property(l => l.Desc2).HasMaxLength(250);
            Property(l => l.MaxLineNumber).HasMaxLength(10);
            Property(l => l.MaxDeliveryNumber).HasMaxLength(10);
        }
    }
}
