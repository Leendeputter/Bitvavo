using System.Data.Entity.Migrations;
using System.Linq;
using Procurement.Core.Entities;
using Procurement.Core.Enums;

namespace Procurement.Data.Migrations
{
    /// <summary>
    /// EF6 automatic migrations — no design-time "Add-Migration" scaffolding is available for
    /// this prototype, so the schema evolves automatically on startup
    /// (Database.SetInitializer(new MigrateDatabaseToLatestVersion&lt;...&gt;()) in
    /// Procurement.UI's Program.cs) and seeds the configurable business rules from spec §3.5/§5.
    /// </summary>
    public sealed class Configuration : DbMigrationsConfiguration<ProcurementDbContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = true;
        }

        protected override void Seed(ProcurementDbContext context)
        {
            SeedSuppliers(context);
            SeedSupplierPreferences(context);
            SeedPackagingPolicies(context);
            SeedApprovalPolicy(context);
        }

        private static void SeedSuppliers(ProcurementDbContext context)
        {
            // VendorId = MAX Part_Vendor.VENID_07 for this supplier, used to translate MAX's
            // vendor-part cross-reference into SupplierProductMapping rows (see MaxErpConnector).
            // Only backfilled if empty, so a manual correction via Instellingen (§8.6) is never
            // silently overwritten by this seed running again on a later startup.
            SeedSupplier(context, "DIGIKEY", "DigiKey Electronics", "10194", new[]
            {
                (Capability: "ProductSearch", Status: CapabilityStatus.Supported),
                (Capability: "Pricing", Status: CapabilityStatus.Supported),
                (Capability: "Availability", Status: CapabilityStatus.Supported),
                (Capability: "Packaging", Status: CapabilityStatus.Supported),
                (Capability: "Ordering", Status: CapabilityStatus.Supported),
                (Capability: "OrderStatus", Status: CapabilityStatus.Supported),
                (Capability: "Shipment", Status: CapabilityStatus.ManualProcess),
                (Capability: "Tracking", Status: CapabilityStatus.ManualProcess),
                (Capability: "Invoice", Status: CapabilityStatus.ManualProcess),
            });

            SeedSupplier(context, "FARNELL", "Farnell / element14", "0349", new[]
            {
                (Capability: "ProductSearch", Status: CapabilityStatus.Supported),
                (Capability: "Pricing", Status: CapabilityStatus.Supported),
                (Capability: "Availability", Status: CapabilityStatus.Supported),
                (Capability: "Packaging", Status: CapabilityStatus.Supported),
                (Capability: "Ordering", Status: CapabilityStatus.Supported),
                (Capability: "OrderStatus", Status: CapabilityStatus.Supported),
                (Capability: "Shipment", Status: CapabilityStatus.ManualProcess),
                (Capability: "Tracking", Status: CapabilityStatus.ManualProcess),
                (Capability: "Invoice", Status: CapabilityStatus.ManualProcess),
            });
        }

        private static void SeedSupplier(
            ProcurementDbContext context,
            string code,
            string name,
            string vendorId,
            (string Capability, CapabilityStatus Status)[] capabilities)
        {
            var supplier = context.Suppliers.FirstOrDefault(s => s.SupplierCode == code);
            if (supplier == null)
            {
                supplier = new Supplier
                {
                    SupplierCode = code,
                    Name = name,
                    VendorId = vendorId,
                    IsSandbox = true,
                    UseMockData = true
                };
                context.Suppliers.Add(supplier);
                context.SaveChanges();
            }
            else if (string.IsNullOrEmpty(supplier.VendorId))
            {
                supplier.VendorId = vendorId;
                context.SaveChanges();
            }

            foreach (var (capability, status) in capabilities)
            {
                var existing = context.SupplierCapabilityRecords
                    .FirstOrDefault(c => c.SupplierId == supplier.Id && c.Capability == capability);
                if (existing == null)
                {
                    context.SupplierCapabilityRecords.Add(new SupplierCapabilityRecord
                    {
                        SupplierId = supplier.Id,
                        Capability = capability,
                        Status = status
                    });
                }
                else
                {
                    existing.Status = status;
                }
            }

            context.SaveChanges();
        }

        private static void SeedSupplierPreferences(ProcurementDbContext context)
        {
            if (!context.SupplierPreferences.Any(p => p.SupplierCode == "DIGIKEY"))
            {
                context.SupplierPreferences.Add(new SupplierPreference
                {
                    SupplierCode = "DIGIKEY",
                    Priority = 1,
                    Active = true,
                    AllowedForAutoOrder = true
                });
            }

            if (!context.SupplierPreferences.Any(p => p.SupplierCode == "FARNELL"))
            {
                context.SupplierPreferences.Add(new SupplierPreference
                {
                    SupplierCode = "FARNELL",
                    Priority = 2,
                    Active = true,
                    AllowedForAutoOrder = true
                });
            }

            context.SaveChanges();
        }

        private static void SeedPackagingPolicies(ProcurementDbContext context)
        {
            if (!context.PackagingPolicies.Any(p => p.ComponentCategory == "Default"))
            {
                var policy = new PackagingPolicy
                {
                    ComponentCategory = "Default",
                    MinimumQuantity = 0,
                    PreferredPackaging = PackagingType.EitherReel,
                    OriginalReelRequired = false
                };
                policy.AllowedPackaging = new System.Collections.Generic.List<PackagingType>
                {
                    PackagingType.OriginalReel,
                    PackagingType.ReReel,
                    PackagingType.EitherReel,
                    PackagingType.CutTape,
                    PackagingType.Tray,
                    PackagingType.Tube
                };
                context.PackagingPolicies.Add(policy);
            }

            context.SaveChanges();
        }

        private static void SeedApprovalPolicy(ProcurementDbContext context)
        {
            if (!context.ApprovalPolicies.Any())
            {
                context.ApprovalPolicies.Add(new ApprovalPolicy
                {
                    MaxOrderValueForAutoApproval = 500m,
                    MaxPriceVariancePercentage = 10m,
                    AllowExternalSupplier = false,
                    AllowNonOriginalPackaging = true,
                    AllowAlternativePart = false
                });
                context.SaveChanges();
            }
        }
    }
}
