using System.Data.Entity.ModelConfiguration;
using Procurement.Core.Entities;

namespace Procurement.Data.Configurations
{
    public class SupplierOfferConfiguration : EntityTypeConfiguration<SupplierOffer>
    {
        public SupplierOfferConfiguration()
        {
            ToTable("Procurement_SupplierOffer");
            HasKey(o => o.Id);

            Property(o => o.SupplierCode).HasMaxLength(20).IsRequired();
            Property(o => o.SupplierPartNumber).HasMaxLength(100).IsRequired();
            Property(o => o.Manufacturer).HasMaxLength(100);
            Property(o => o.ManufacturerPartNumber).HasMaxLength(100);
            Property(o => o.Currency).HasMaxLength(3);

            HasRequired(o => o.PurchaseRequestLine)
                .WithMany()
                .HasForeignKey(o => o.PurchaseRequestLineId)
                .WillCascadeOnDelete(false);

            HasMany(o => o.PackagingOptions)
                .WithRequired(p => p.SupplierOffer)
                .HasForeignKey(p => p.SupplierOfferId)
                .WillCascadeOnDelete(true);
        }
    }
}
