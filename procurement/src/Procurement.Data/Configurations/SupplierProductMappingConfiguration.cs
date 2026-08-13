using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Infrastructure.Annotations;
using System.Data.Entity.ModelConfiguration;
using Procurement.Core.Entities;

namespace Procurement.Data.Configurations
{
    public class SupplierProductMappingConfiguration : EntityTypeConfiguration<SupplierProductMapping>
    {
        public SupplierProductMappingConfiguration()
        {
            ToTable("SupplierProductMapping");
            HasKey(m => m.Id);

            Property(m => m.ErpArticleId).HasMaxLength(50);
            Property(m => m.SupplierCode).HasMaxLength(20).IsRequired();
            Property(m => m.SupplierPartNumber).HasMaxLength(100).IsRequired();
            Property(m => m.Manufacturer).HasMaxLength(100);
            Property(m => m.ManufacturerPartNumber).HasMaxLength(100);

            // One mapping per (ErpArticleId, SupplierCode) — repeated lookups reuse the same row.
            Property(m => m.ErpArticleId)
                .HasColumnAnnotation(IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_SupplierProductMapping_Article_Supplier", 1) { IsUnique = true }));
            Property(m => m.SupplierCode)
                .HasColumnAnnotation(IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_SupplierProductMapping_Article_Supplier", 2) { IsUnique = true }));
        }
    }
}
