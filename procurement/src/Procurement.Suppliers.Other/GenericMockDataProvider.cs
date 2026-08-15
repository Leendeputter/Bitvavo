using System;
using System.Collections.Generic;
using Procurement.Core.Enums;
using Procurement.Core.Mocking;
using Procurement.Core.Models;

namespace Procurement.Suppliers.Other
{
    /// <summary>
    /// Deterministic mock data generator shared by every GenericMockSupplierAdapter instance,
    /// parameterized by supplier code so DigiKeyMockDataProvider/FarnellMockDataProvider's pattern
    /// doesn't need to be copy-pasted seven times for suppliers that don't have a real HTTP layer
    /// yet anyway. Unlike DigiKey/Farnell/Mouser/TME there is no "known example part" reproducing
    /// spec §7 exactly — every distributor here returns purely deterministic pseudo-realistic
    /// offers for any input, which is enough for sourcing/comparison demos across all of them.
    /// </summary>
    internal static class GenericMockDataProvider
    {
        public static string BuildSupplierPartNumber(string supplierCode, string manufacturer, string manufacturerPartNumber)
        {
            var prefix = SupplierPartPrefix(supplierCode);
            var suffix = DeterministicMock.Hash($"{manufacturer}|{manufacturerPartNumber}|{supplierCode}") % 1000000;
            return $"{prefix}-{suffix:D6}";
        }

        private static string SupplierPartPrefix(string supplierCode) =>
            supplierCode.Length >= 3 ? supplierCode.Substring(0, 3) : supplierCode;

        public static SupplierProduct BuildProduct(string supplierCode, string manufacturer, string manufacturerPartNumber, string datasheetHost)
        {
            var supplierPartNumber = BuildSupplierPartNumber(supplierCode, manufacturer, manufacturerPartNumber);
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
                DatasheetUrl = $"https://{datasheetHost}/datasheets/{supplierPartNumber}.pdf"
            };
        }

        public static SupplierAvailability BuildAvailability(string supplierPartNumber)
        {
            var available = DeterministicMock.NextInt(supplierPartNumber + "|avail", 200, 100000);
            var leadTime = DeterministicMock.NextInt(supplierPartNumber + "|lead", 2, 15);
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
            var reelSize = DeterministicMock.Pick(supplierPartNumber + "|reel", new[] { 1000, 1500, 2000, 2500, 4000, 5000, 10000 });
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
                    new PriceBreak { BreakQuantity = reelSize * 2, UnitPrice = Math.Round(unitPrice * 0.95m, 4) }
                }
            };
        }

        public static IReadOnlyList<SupplierPackagingOption> BuildPackagingOptions(string supplierPartNumber, int quantity)
        {
            var reelSize = DeterministicMock.Pick(supplierPartNumber + "|reel", new[] { 1000, 1500, 2000, 2500, 4000, 5000, 10000 });
            var available = DeterministicMock.NextInt(supplierPartNumber + "|avail", 200, 100000);

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
