using System;
using System.Collections.Generic;
using Procurement.Core.Enums;
using Procurement.Core.Mocking;
using Procurement.Core.Models;

namespace Procurement.Suppliers.DigiKey
{
    /// <summary>
    /// Fixed, realistic test data for DigiKeyAdapter. One exact part reproduces the worked
    /// example from spec §7 (12.000 pcs, reel 5.000 @ €0,11); anything else gets deterministic
    /// pseudo-realistic values derived from the manufacturer+MPN, so the workflow is fully
    /// demoable for arbitrary test input, not just the one example part.
    /// </summary>
    public static class DigiKeyMockDataProvider
    {
        public const string ExampleManufacturer = "Vishay";
        public const string ExampleMpn = "CRCW060310K0FKEA";
        private const string ExampleSupplierPartNumber = "DK-CRCW060310K0FKEA";

        public static string BuildSupplierPartNumber(string manufacturer, string manufacturerPartNumber)
        {
            if (IsExamplePart(manufacturer, manufacturerPartNumber))
                return ExampleSupplierPartNumber;

            var suffix = DeterministicMock.Hash($"{manufacturer}|{manufacturerPartNumber}") % 1000000;
            return $"DK-{suffix:D6}";
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
                DatasheetUrl = $"https://www.digikey.com/datasheets/{supplierPartNumber}.pdf"
            };
        }

        public static SupplierAvailability BuildAvailability(string supplierPartNumber)
        {
            if (supplierPartNumber == ExampleSupplierPartNumber)
            {
                return new SupplierAvailability
                {
                    SupplierPartNumber = supplierPartNumber,
                    AvailableQuantity = 48000,
                    LeadTimeDays = 3,
                    EstimatedDeliveryDate = DateTime.UtcNow.AddDays(3),
                    OnBackorder = false
                };
            }

            var available = DeterministicMock.NextInt(supplierPartNumber + "|avail", 500, 120000);
            var leadTime = DeterministicMock.NextInt(supplierPartNumber + "|lead", 2, 10);
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
                    UnitPrice = 0.11m,
                    Currency = "EUR",
                    MinimumOrderQuantity = 5000,
                    OrderMultiple = 5000,
                    PriceBreaks = new List<PriceBreak>
                    {
                        new PriceBreak { BreakQuantity = 5000, UnitPrice = 0.11m },
                        new PriceBreak { BreakQuantity = 10000, UnitPrice = 0.105m },
                        new PriceBreak { BreakQuantity = 25000, UnitPrice = 0.095m }
                    }
                };
            }

            var reelSize = DeterministicMock.Pick(supplierPartNumber + "|reel", new[] { 1000, 2000, 2500, 4000, 5000, 10000 });
            var unitPrice = Math.Round((decimal)DeterministicMock.NextDouble(supplierPartNumber + "|price", 0.03, 3.50), 4);

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
                        PackagingQuantity = 5000,
                        ReelType = Core.Enums.ReelType.ManufacturerOriginal,
                        ReelingFee = 0m,
                        AvailableQuantity = 48000
                    }
                };
            }

            var reelSize = DeterministicMock.Pick(supplierPartNumber + "|reel", new[] { 1000, 2000, 2500, 4000, 5000, 10000 });
            var available = DeterministicMock.NextInt(supplierPartNumber + "|avail", 500, 120000);

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
