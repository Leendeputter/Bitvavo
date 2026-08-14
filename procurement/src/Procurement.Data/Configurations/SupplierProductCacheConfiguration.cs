using System.Data.Entity.ModelConfiguration;
using Procurement.Core.Entities;

namespace Procurement.Data.Configurations
{
    public class SupplierProductCacheConfiguration : EntityTypeConfiguration<SupplierProductCache>
    {
        public SupplierProductCacheConfiguration()
        {
            ToTable("Procurement_SupplierProduct");
            HasKey(p => p.Id);

            Property(p => p.SupplierCode).HasMaxLength(20).IsRequired();
            Property(p => p.SupplierPartNumber).HasMaxLength(100).IsRequired();
            Property(p => p.Manufacturer).HasMaxLength(100);
            Property(p => p.ManufacturerPartNumber).HasMaxLength(100);
            Property(p => p.Description).HasMaxLength(500);
            Property(p => p.DatasheetUrl).HasMaxLength(500);
        }
    }
}
