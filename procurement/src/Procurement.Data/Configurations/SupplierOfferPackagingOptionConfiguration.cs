using System.Data.Entity.ModelConfiguration;
using Procurement.Core.Entities;

namespace Procurement.Data.Configurations
{
    public class SupplierOfferPackagingOptionConfiguration : EntityTypeConfiguration<SupplierOfferPackagingOption>
    {
        public SupplierOfferPackagingOptionConfiguration()
        {
            ToTable("SupplierOfferPackaging");
            HasKey(p => p.Id);
        }
    }
}
