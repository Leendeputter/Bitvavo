using System.Data.Entity;
using System.Data.Entity.Infrastructure.Annotations;
using System.Data.Entity.ModelConfiguration.Conventions;
using Procurement.Core.Entities;
using Procurement.Data.Configurations;

namespace Procurement.Data
{
    /// <summary>
    /// EF6 Code First context for the procurement prototype. Connection string name/key is
    /// "ProcurementDbContext" (see App.config in Procurement.UI); LocalDB by default for
    /// development, SQL Server for anything beyond that.
    /// </summary>
    public class ProcurementDbContext : DbContext
    {
        public ProcurementDbContext() : base("name=ProcurementDbContext")
        {
        }

        public ProcurementDbContext(string nameOrConnectionString) : base(nameOrConnectionString)
        {
        }

        public DbSet<PurchaseRequest> PurchaseRequests { get; set; }
        public DbSet<PurchaseRequestLine> PurchaseRequestLines { get; set; }
        public DbSet<SupplierProductMapping> SupplierProductMappings { get; set; }
        public DbSet<SupplierProductCache> SupplierProducts { get; set; }
        public DbSet<SupplierOffer> SupplierOffers { get; set; }
        public DbSet<SupplierOfferPackagingOption> SupplierOfferPackagingOptions { get; set; }
        public DbSet<SupplierSelection> SupplierSelections { get; set; }
        public DbSet<ApprovalRequest> ApprovalRequests { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; }
        public DbSet<SupplierOrder> SupplierOrders { get; set; }
        public DbSet<SupplierOrderLine> SupplierOrderLines { get; set; }
        public DbSet<ProcurementEvent> ProcurementEvents { get; set; }
        public DbSet<SupplierPreference> SupplierPreferences { get; set; }
        public DbSet<PackagingPolicy> PackagingPolicies { get; set; }
        public DbSet<ApprovalPolicy> ApprovalPolicies { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierCapabilityRecord> SupplierCapabilityRecords { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // A prototype's tables are heavily cross-referenced (line -> offers -> selection ->
            // PO -> supplier order); letting every FK cascade would hit SQL Server's "multiple
            // cascade paths" restriction. Delete behavior is handled explicitly in repositories.
            modelBuilder.Conventions.Remove<OneToManyCascadeDeleteConvention>();
            modelBuilder.Conventions.Remove<ManyToManyCascadeDeleteConvention>();

            // Global convention for money fields; individual configs can override where needed.
            modelBuilder.Properties<decimal>().Configure(c => c.HasPrecision(18, 4));

            modelBuilder.Configurations.Add(new PurchaseRequestConfiguration());
            modelBuilder.Configurations.Add(new PurchaseRequestLineConfiguration());
            modelBuilder.Configurations.Add(new SupplierProductMappingConfiguration());
            modelBuilder.Configurations.Add(new SupplierProductCacheConfiguration());
            modelBuilder.Configurations.Add(new SupplierOfferConfiguration());
            modelBuilder.Configurations.Add(new SupplierOfferPackagingOptionConfiguration());
            modelBuilder.Configurations.Add(new SupplierSelectionConfiguration());
            modelBuilder.Configurations.Add(new ApprovalRequestConfiguration());
            modelBuilder.Configurations.Add(new PurchaseOrderConfiguration());
            modelBuilder.Configurations.Add(new PurchaseOrderLineConfiguration());
            modelBuilder.Configurations.Add(new SupplierOrderConfiguration());
            modelBuilder.Configurations.Add(new SupplierOrderLineConfiguration());
            modelBuilder.Configurations.Add(new SupplierConfiguration());
            modelBuilder.Configurations.Add(new SupplierCapabilityRecordConfiguration());
            modelBuilder.Configurations.Add(new ProcurementEventConfiguration());
            modelBuilder.Configurations.Add(new SupplierPreferenceConfiguration());
            modelBuilder.Configurations.Add(new PackagingPolicyConfiguration());
            modelBuilder.Configurations.Add(new ApprovalPolicyConfiguration());

            base.OnModelCreating(modelBuilder);
        }
    }
}
