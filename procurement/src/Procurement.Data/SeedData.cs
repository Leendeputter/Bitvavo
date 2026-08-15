using System.Collections.Generic;
using System.Linq;
using Procurement.Core.Entities;
using Procurement.Core.Enums;

namespace Procurement.Data
{
    /// <summary>
    /// Seeds the configurable business-rule tables from spec §3.5/§5 (suppliers, capabilities,
    /// preferences, packaging/approval policy defaults). Called directly from Program.cs after the
    /// schema-existence check — this used to be EF6 migrations' Seed() callback, but this project's
    /// PackageReference-style .csproj doesn't support the classic "Add-Migration"/"Update-Database"
    /// PowerShell tooling (that mechanism relies on NuGet's install.ps1/init.ps1 scripts, which
    /// PackageReference projects never run — see README's "Database" section), so migrations
    /// scaffolding was dropped entirely in favor of hand-reviewed SQL scripts. Every method here is
    /// idempotent (checks before inserting), so calling this on every startup is safe.
    /// </summary>
    public static class SeedData
    {
        public static void EnsureSeeded(ProcurementDbContext context)
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

            // Same capability matrix for every distributor below (spec §5) — only the confirmed
            // API depth behind each one differs (see CompositionRoot / README "Overige
            // distributeurs"). VendorId is left null for all of them: nobody has confirmed the
            // MAX Part_Vendor.VENID_07 code for these yet, fill in via Instellingen (§8.6) once
            // known — SeedSupplier only backfills when empty, so this is safe to leave blank here.
            var standardCapabilities = new[]
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
            };

            // Publiek gedocumenteerde, zelfbedienings-API — zelfde vertrouwensniveau als DigiKey/Farnell.
            SeedSupplier(context, "MOUSER", "Mouser Electronics", null, standardCapabilities);
            SeedSupplier(context, "TME", "TME (Transfer Multisort Elektronik)", null, standardCapabilities);

            // Grote distributeurs zonder bevestigde publieke API — vermoedelijk alleen na een
            // aparte account-/EDI-overeenkomst, contract nog niet bevestigd (zie README).
            SeedSupplier(context, "ARROW", "Arrow Electronics", null, standardCapabilities);
            SeedSupplier(context, "RUTRONIK", "Rutronik", null, standardCapabilities);
            SeedSupplier(context, "AVNET_SILICA", "Avnet / Silica", null, standardCapabilities);
            SeedSupplier(context, "KARL_KRUSE", "Karl Kruse", null, standardCapabilities);
            SeedSupplier(context, "RS_COMPONENTS", "RS Components", null, standardCapabilities);
            SeedSupplier(context, "DISTRELEC", "Distrelec / Elfa Distrelec", null, standardCapabilities);
            SeedSupplier(context, "CONRAD", "Conrad Business Supplies", null, standardCapabilities);
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

            // Seeded inactief: geen VendorId/Part_Vendor-koppeling opgezet en (op Mouser/TME na)
            // geen bevestigde API, dus deze mogen niet zomaar meedoen aan sourcing/auto-order
            // totdat iemand ze bewust activeert via Instellingen -> Supplier preferences.
            var priority = 3;
            foreach (var code in new[]
                     {
                         "MOUSER", "TME", "ARROW", "RUTRONIK", "AVNET_SILICA",
                         "KARL_KRUSE", "RS_COMPONENTS", "DISTRELEC", "CONRAD"
                     })
            {
                if (!context.SupplierPreferences.Any(p => p.SupplierCode == code))
                {
                    context.SupplierPreferences.Add(new SupplierPreference
                    {
                        SupplierCode = code,
                        Priority = priority,
                        Active = false,
                        AllowedForAutoOrder = false
                    });
                }
                priority++;
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
                policy.AllowedPackaging = new List<PackagingType>
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
