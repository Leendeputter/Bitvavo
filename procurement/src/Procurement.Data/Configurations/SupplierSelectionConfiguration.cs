using System.Data.Entity.ModelConfiguration;
using Procurement.Core.Entities;

namespace Procurement.Data.Configurations
{
    public class SupplierSelectionConfiguration : EntityTypeConfiguration<SupplierSelection>
    {
        public SupplierSelectionConfiguration()
        {
            ToTable("Procurement_SupplierSelection");
            HasKey(s => s.Id);

            Property(s => s.ReasonSummary).HasMaxLength(2000);

            HasRequired(s => s.PurchaseRequestLine)
                .WithMany()
                .HasForeignKey(s => s.PurchaseRequestLineId)
                .WillCascadeOnDelete(false);

            HasRequired(s => s.SelectedOffer)
                .WithMany()
                .HasForeignKey(s => s.SelectedOfferId)
                .WillCascadeOnDelete(false);
        }
    }
}
