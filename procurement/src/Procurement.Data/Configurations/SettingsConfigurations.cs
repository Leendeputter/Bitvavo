using System.Data.Entity.ModelConfiguration;
using Procurement.Core.Entities;

namespace Procurement.Data.Configurations
{
    public class ProcurementEventConfiguration : EntityTypeConfiguration<ProcurementEvent>
    {
        public ProcurementEventConfiguration()
        {
            ToTable("ProcurementEvent");
            HasKey(e => e.Id);

            Property(e => e.EntityType).HasMaxLength(50);
            Property(e => e.EntityId).HasMaxLength(50);
            Property(e => e.EventType).HasMaxLength(50);
            Property(e => e.UserOrSystem).HasMaxLength(100);
            Property(e => e.SupplierCode).HasMaxLength(20);
            Property(e => e.Status).HasMaxLength(20);
            Property(e => e.RequestPayload).HasColumnType("nvarchar(max)");
            Property(e => e.ResponsePayload).HasColumnType("nvarchar(max)");
            Property(e => e.Error).HasColumnType("nvarchar(max)");
        }
    }

    public class SupplierPreferenceConfiguration : EntityTypeConfiguration<SupplierPreference>
    {
        public SupplierPreferenceConfiguration()
        {
            ToTable("SupplierPreference");
            HasKey(p => p.Id);
            Property(p => p.SupplierCode).HasMaxLength(20).IsRequired();
        }
    }

    public class PackagingPolicyConfiguration : EntityTypeConfiguration<PackagingPolicy>
    {
        public PackagingPolicyConfiguration()
        {
            ToTable("PackagingPolicy");
            HasKey(p => p.Id);
            Property(p => p.ComponentCategory).HasMaxLength(100).IsRequired();
            Property(p => p.AllowedPackagingCsv).HasMaxLength(200).HasColumnName("AllowedPackaging");
        }
    }

    public class ApprovalPolicyConfiguration : EntityTypeConfiguration<ApprovalPolicy>
    {
        public ApprovalPolicyConfiguration()
        {
            ToTable("ApprovalPolicy");
            HasKey(p => p.Id);
        }
    }
}
