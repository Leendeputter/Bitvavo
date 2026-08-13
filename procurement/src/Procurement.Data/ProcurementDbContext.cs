using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Annotations;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Data.Entity.Validation;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Procurement.Core.Entities;
using Procurement.Data.Configurations;

namespace Procurement.Data
{
    /// <summary>
    /// EF6 Code First context for the procurement prototype. The real connection string is
    /// resolved at login time (Windows identity + MAX company selection -> the "Unitron"
    /// database) and passed explicitly via the <see cref="ProcurementDbContext(string)"/>
    /// constructor from Procurement.UI's CompositionRoot — see that constructor's remarks for
    /// why the parameterless constructor still exists and what it's for.
    ///
    /// This app keeps a single long-lived instance for the whole run (see CompositionRoot), which
    /// is simple but not thread/reentrancy-safe on its own: EF6 throws NotSupportedException if a
    /// second async operation starts on a context before the first completes — easy to trigger in
    /// WinForms since a user can interact with the UI (firing another event handler) while an
    /// earlier await is still pending. <see cref="RunGuardedAsync{T}"/> serializes all access to
    /// this context so overlapping calls queue up instead of crashing.
    /// </summary>
    public class ProcurementDbContext : DbContext
    {
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        // EF6's migration tooling (DbMigrator) constructs a context via this parameterless
        // constructor purely to compute the current Code First model — for diffing against
        // migration history — regardless of Configuration.TargetDatabase (which only supplies
        // the connection actually used for the real database work in Program.cs). This instance
        // is never opened/queried for real, so the connection string just needs to be
        // syntactically valid, not a working one — a "name=X" config lookup throws immediately
        // during construction if that name doesn't exist, which is exactly what broke here once
        // the static App.config connection string was removed in favor of a runtime-resolved one.
        public ProcurementDbContext() : base(DesignTimeOnlyConnectionString)
        {
        }

        public ProcurementDbContext(string nameOrConnectionString) : base(nameOrConnectionString)
        {
        }

        private const string DesignTimeOnlyConnectionString =
            "Data Source=.;Initial Catalog=Procurement_DesignTimeOnly;Integrated Security=True";

        public async Task<T> RunGuardedAsync<T>(Func<Task<T>> operation)
        {
            await _gate.WaitAsync();
            try
            {
                return await operation();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task RunGuardedAsync(Func<Task> operation)
        {
            await _gate.WaitAsync();
            try
            {
                await operation();
            }
            finally
            {
                _gate.Release();
            }
        }

        // DbEntityValidationException.Message is always the same generic "Validation failed..."
        // text — the actual per-property reason lives in EntityValidationErrors, which nothing
        // prints by default. Overriding SaveChangesAsync here (the method every repository
        // ultimately calls) means every validation failure, current and future, surfaces its
        // real cause everywhere in the app automatically, without repeating try/catch in each
        // of the ~20 call sites across the repositories.
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await base.SaveChangesAsync(cancellationToken);
            }
            catch (DbEntityValidationException ex)
            {
                throw new DbEntityValidationException(BuildValidationErrorMessage(ex), ex.EntityValidationErrors);
            }
        }

        private static string BuildValidationErrorMessage(DbEntityValidationException ex)
        {
            var sb = new StringBuilder("Validatie mislukt:");
            foreach (var entityError in ex.EntityValidationErrors)
            {
                var entityName = entityError.Entry.Entity.GetType().Name;
                foreach (var error in entityError.ValidationErrors)
                {
                    sb.Append($"\n- {entityName}.{error.PropertyName}: {error.ErrorMessage}");
                }
            }
            return sb.ToString();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _gate.Dispose();
            }
            base.Dispose(disposing);
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
