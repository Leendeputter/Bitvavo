using System.Data.Entity.ModelConfiguration;
using Procurement.Core.Entities;

namespace Procurement.Data.Configurations
{
    public class ApprovalRequestConfiguration : EntityTypeConfiguration<ApprovalRequest>
    {
        public ApprovalRequestConfiguration()
        {
            ToTable("Procurement_ApprovalRequest");
            HasKey(a => a.Id);

            Property(a => a.Reasons).HasMaxLength(2000);
            Property(a => a.Comment).HasMaxLength(2000);
            Property(a => a.DecidedBy).HasMaxLength(100);

            HasRequired(a => a.PurchaseRequestLine)
                .WithMany()
                .HasForeignKey(a => a.PurchaseRequestLineId)
                .WillCascadeOnDelete(false);

            HasRequired(a => a.ProposedOffer)
                .WithMany()
                .HasForeignKey(a => a.ProposedOfferId)
                .WillCascadeOnDelete(false);
        }
    }
}
