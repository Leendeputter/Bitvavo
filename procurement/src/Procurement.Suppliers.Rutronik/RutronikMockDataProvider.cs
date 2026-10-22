using System;
using System.Collections.Generic;
using Procurement.Core.Enums;
using Procurement.Core.Mocking;
using Procurement.Core.Models;

namespace Procurement.Suppliers.Rutronik
{
    /// <summary>Fixed, deterministic test data for RutronikAdapter — same pattern as TmeMockDataProvider/MouserMockDataProvider.</summary>
    public static class RutronikMockDataProvider
    {
        public const string ExampleManufacturer = "Infineon";
        public const string ExampleMpn = "XMC4500-F100K1024 AC";
        private const string ExampleSupplierPartNumber = "ICMCU22246";

        public static string BuildSupplierPartNumber(string manufacturer, string manufacturerPartNumber)
        {
            if (IsExamplePart(manufacturer, manufacturerPartNumber))
                return ExampleSupplierPartNumber;

            var suffix = DeterministicMock.Hash($"{manufacturer}|{manufacturerPartNumber}|rutronik") % 1000000;
            return $"IC{suffix:D6}";
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
                DatasheetUrl = $"https://www.rutronik24.com/produkt/{supplierPartNumber}.html"
            };
        }

        public static SupplierAvailability BuildAvailability(string supplierPartNumber)
        {
            if (supplierPartNumber == ExampleSupplierPartNumber)
            {
                return new SupplierAvailability
                {
                    SupplierPartNumber = supplierPartNumber,
                    AvailableQuantity = 18500,
                    LeadTimeDays = 7 * 7,
                    EstimatedDeliveryDate = DateTime.UtcNow.AddDays(7 * 7),
                    OnBackorder = false
                };
            }

            var available = DeterministicMock.NextInt(supplierPartNumber + "|avail", 200, 60000);
            var leadTimeWeeks = DeterministicMock.NextInt(supplierPartNumber + "|lead", 1, 12);
            var leadTimeDays = leadTimeWeeks * 7;
            return new SupplierAvailability
            {
                SupplierPartNumber = supplierPartNumber,
                AvailableQuantity = available,
                LeadTimeDays = leadTimeDays,
                EstimatedDeliveryDate = DateTime.UtcNow.AddDays(leadTimeDays),
                OnBackorder = available < 50
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
                    UnitPrice = 4.12m,
                    Currency = "EUR",
                    MinimumOrderQuantity = 1,
                    OrderMultiple = 1,
                    PriceBreaks = new List<PriceBreak>
                    {
                        new PriceBreak { BreakQuantity = 1, UnitPrice = 4.12m },
                        new PriceBreak { BreakQuantity = 10, UnitPrice = 3.75m },
                        new PriceBreak { BreakQuantity = 100, UnitPrice = 3.20m }
                    }
                };
            }

            var puSize = DeterministicMock.Pick(supplierPartNumber + "|pu", new[] { 1, 10, 100, 1000 });
            var unitPrice = Math.Round((decimal)DeterministicMock.NextDouble(supplierPartNumber + "|price", 0.02, 5.00), 4);

            return new SupplierPricing
            {
                SupplierPartNumber = supplierPartNumber,
                RequestedQuantity = quantity,
                UnitPrice = unitPrice,
                Currency = "EUR",
                MinimumOrderQuantity = puSize,
                OrderMultiple = puSize,
                PriceBreaks = new List<PriceBreak>
                {
                    new PriceBreak { BreakQuantity = puSize, UnitPrice = unitPrice },
                    new PriceBreak { BreakQuantity = puSize * 10, UnitPrice = Math.Round(unitPrice * 0.9m, 4) }
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
                        PackagingType = PackagingType.Tray,
                        PackagingQuantity = 1,
                        ReelType = null,
                        ReelingFee = 0m,
                        AvailableQuantity = 18500
                    }
                };
            }

            var packaging = DeterministicMock.Pick(supplierPartNumber + "|packaging",
                new[] { PackagingType.OriginalReel, PackagingType.Tray, PackagingType.Tube, PackagingType.Any });
            var available = DeterministicMock.NextInt(supplierPartNumber + "|avail", 200, 60000);
            var puSize = DeterministicMock.Pick(supplierPartNumber + "|pu", new[] { 1, 10, 100, 1000 });

            return new List<SupplierPackagingOption>
            {
                new SupplierPackagingOption
                {
                    SupplierPartNumber = supplierPartNumber,
                    PackagingType = packaging,
                    PackagingQuantity = puSize,
                    ReelType = packaging == PackagingType.OriginalReel ? Core.Enums.ReelType.ManufacturerOriginal : (Core.Enums.ReelType?)null,
                    ReelingFee = 0m,
                    AvailableQuantity = available
                }
            };
        }
    }
}
