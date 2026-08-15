using System;
using System.Collections.Generic;
using Procurement.Core.Enums;
using Procurement.Core.Mocking;
using Procurement.Core.Models;

namespace Procurement.Suppliers.TME
{
    /// <summary>Fixed, deterministic test data for TmeAdapter — same pattern as DigiKeyMockDataProvider/FarnellMockDataProvider.</summary>
    public static class TmeMockDataProvider
    {
        public const string ExampleManufacturer = "Vishay";
        public const string ExampleMpn = "CRCW060310K0FKEA";
        private const string ExampleSupplierPartNumber = "TME-CRCW060310K0FKEA";

        public static string BuildSupplierPartNumber(string manufacturer, string manufacturerPartNumber)
        {
            if (IsExamplePart(manufacturer, manufacturerPartNumber))
                return ExampleSupplierPartNumber;

            var suffix = DeterministicMock.Hash($"{manufacturer}|{manufacturerPartNumber}|tme") % 1000000;
            return $"TME-{suffix:D6}";
        }

        private static bool IsExamplePart(string manufacturer, string mpn) =>
            string.Equals(manufacturer, ExampleManufacturer, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(mpn, ExampleMpn, StringComparison.OrdinalIgnoreCase);

        public static SupplierProduct BuildProduct(string manufacturer, string manufacturerPartNumber)
        {
            var supplierPartNumber = BuildSupplierPartNumber(manufacturer, manufacturerPartNumber);
            var confidence = string.IsNullOrWhiteSpace(manufacturer) || string.IsNullOrWhiteSpace(manufacturerPartNumber)
                ? MatchConfidence.Medium
                : MatchConfidence.Exact;

            return new SupplierProduct
            {
                SupplierPartNumber = supplierPartNumber,
                Manufacturer = manufacturer,
                ManufacturerPartNumber = manufacturerPartNumber,
                Description = $"{manufacturer} {manufacturerPartNumber}".Trim(),
                MatchConfidence = confidence,
                DatasheetUrl = $"https://www.tme.eu/datasheets/{supplierPartNumber}.pdf"
            };
        }

        public static SupplierAvailability BuildAvailability(string supplierPartNumber)
        {
            if (supplierPartNumber == ExampleSupplierPartNumber)
            {
                return new SupplierAvailability
                {
                    SupplierPartNumber = supplierPartNumber,
                    AvailableQuantity = 36000,
                    LeadTimeDays = 5,
                    EstimatedDeliveryDate = DateTime.UtcNow.AddDays(5),
                    OnBackorder = false
                };
            }

            var available = DeterministicMock.NextInt(supplierPartNumber + "|avail", 500, 90000);
            var leadTime = DeterministicMock.NextInt(supplierPartNumber + "|lead", 3, 14);
            return new SupplierAvailability
            {
                SupplierPartNumber = supplierPartNumber,
                AvailableQuantity = available,
                LeadTimeDays = leadTime,
                EstimatedDeliveryDate = DateTime.UtcNow.AddDays(leadTime),
                OnBackorder = available < 100
            };
        }

        public static SupplierPricing BuildPricing(string supplierPartNumber, int quantity)
        {
            if (supplierPartNumber == ExampleSupplierPartNumber)
            {
                return new SupplierPricing
                {
                    SupplierPartNumber = supplierPartNumber,
                    RequestedQuantity = quantity,
                    UnitPrice = 0.118m,
                    Currency = "EUR",
                    MinimumOrderQuantity = 3000,
                    OrderMultiple = 3000,
                    PriceBreaks = new List<PriceBreak>
                    {
                        new PriceBreak { BreakQuantity = 3000, UnitPrice = 0.118m },
                        new PriceBreak { BreakQuantity = 10000, UnitPrice = 0.109m },
                        new PriceBreak { BreakQuantity = 25000, UnitPrice = 0.101m }
                    }
                };
            }

            var reelSize = DeterministicMock.Pick(supplierPartNumber + "|reel", new[] { 1000, 1500, 3000, 4000, 6000 });
            var unitPrice = Math.Round((decimal)DeterministicMock.NextDouble(supplierPartNumber + "|price", 0.03, 3.60), 4);

            return new SupplierPricing
            {
                SupplierPartNumber = supplierPartNumber,
                RequestedQuantity = quantity,
                UnitPrice = unitPrice,
                Currency = "EUR",
                MinimumOrderQuantity = reelSize,
                OrderMultiple = reelSize,
                PriceBreaks = new List<PriceBreak>
                {
                    new PriceBreak { BreakQuantity = reelSize, UnitPrice = unitPrice },
                    new PriceBreak { BreakQuantity = reelSize * 2, UnitPrice = Math.Round(unitPrice * 0.96m, 4) }
                }
            };
        }

        public static IReadOnlyList<SupplierPackagingOption> BuildPackagingOptions(string supplierPartNumber, int quantity)
        {
            if (supplierPartNumber == ExampleSupplierPartNumber)
            {
                return new List<SupplierPackagingOption>
                {
                    new SupplierPackagingOption
                    {
                        SupplierPartNumber = supplierPartNumber,
                        PackagingType = PackagingType.OriginalReel,
                        PackagingQuantity = 3000,
                        ReelType = Core.Enums.ReelType.ManufacturerOriginal,
                        ReelingFee = 0m,
                        AvailableQuantity = 36000
                    }
                };
            }

            var reelSize = DeterministicMock.Pick(supplierPartNumber + "|reel", new[] { 1000, 1500, 3000, 4000, 6000 });
            var available = DeterministicMock.NextInt(supplierPartNumber + "|avail", 500, 90000);

            return new List<SupplierPackagingOption>
            {
                new SupplierPackagingOption
                {
                    SupplierPartNumber = supplierPartNumber,
                    PackagingType = PackagingType.OriginalReel,
                    PackagingQuantity = reelSize,
                    ReelType = Core.Enums.ReelType.ManufacturerOriginal,
                    ReelingFee = 0m,
                    AvailableQuantity = available
                },
                new SupplierPackagingOption
                {
                    SupplierPartNumber = supplierPartNumber,
                    PackagingType = PackagingType.CutTape,
                    PackagingQuantity = 1,
                    ReelType = null,
                    ReelingFee = 0m,
                    AvailableQuantity = Math.Min(available, 500)
                }
            };
        }
    }
}
