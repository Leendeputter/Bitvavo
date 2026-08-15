using System;
using System.Collections.Generic;
using Procurement.Core.Enums;
using Procurement.Core.Mocking;
using Procurement.Core.Models;

namespace Procurement.Suppliers.Mouser
{
    /// <summary>Fixed, deterministic test data for MouserAdapter — same pattern as DigiKeyMockDataProvider/FarnellMockDataProvider.</summary>
    public static class MouserMockDataProvider
    {
        public const string ExampleManufacturer = "Vishay";
        public const string ExampleMpn = "CRCW060310K0FKEA";
        private const string ExampleSupplierPartNumber = "MO-CRCW060310K0FKEA";

        public static string BuildSupplierPartNumber(string manufacturer, string manufacturerPartNumber)
        {
            if (IsExamplePart(manufacturer, manufacturerPartNumber))
                return ExampleSupplierPartNumber;

            var suffix = DeterministicMock.Hash($"{manufacturer}|{manufacturerPartNumber}|mouser") % 1000000;
            return $"MO-{suffix:D6}";
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
                DatasheetUrl = $"https://www.mouser.com/datasheets/{supplierPartNumber}.pdf"
            };
        }

        public static SupplierAvailability BuildAvailability(string supplierPartNumber)
        {
            if (supplierPartNumber == ExampleSupplierPartNumber)
            {
                return new SupplierAvailability
                {
                    SupplierPartNumber = supplierPartNumber,
                    AvailableQuantity = 52000,
                    LeadTimeDays = 2,
                    EstimatedDeliveryDate = DateTime.UtcNow.AddDays(2),
                    OnBackorder = false
                };
            }

            var available = DeterministicMock.NextInt(supplierPartNumber + "|avail", 500, 130000);
            var leadTime = DeterministicMock.NextInt(supplierPartNumber + "|lead", 1, 9);
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
                    UnitPrice = 0.112m,
                    Currency = "EUR",
                    MinimumOrderQuantity = 4000,
                    OrderMultiple = 4000,
                    PriceBreaks = new List<PriceBreak>
                    {
                        new PriceBreak { BreakQuantity = 4000, UnitPrice = 0.112m },
                        new PriceBreak { BreakQuantity = 10000, UnitPrice = 0.104m },
                        new PriceBreak { BreakQuantity = 25000, UnitPrice = 0.097m }
                    }
                };
            }

            var reelSize = DeterministicMock.Pick(supplierPartNumber + "|reel", new[] { 1000, 2000, 3000, 4000, 5000, 10000 });
            var unitPrice = Math.Round((decimal)DeterministicMock.NextDouble(supplierPartNumber + "|price", 0.03, 3.55), 4);

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
                    new PriceBreak { BreakQuantity = reelSize * 2, UnitPrice = Math.Round(unitPrice * 0.95m, 4) }
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
                        PackagingQuantity = 4000,
                        ReelType = Core.Enums.ReelType.ManufacturerOriginal,
                        ReelingFee = 0m,
                        AvailableQuantity = 52000
                    }
                };
            }

            var reelSize = DeterministicMock.Pick(supplierPartNumber + "|reel", new[] { 1000, 2000, 3000, 4000, 5000, 10000 });
            var available = DeterministicMock.NextInt(supplierPartNumber + "|avail", 500, 130000);

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
