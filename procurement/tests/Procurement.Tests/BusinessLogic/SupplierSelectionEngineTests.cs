using System;
using System.Collections.Generic;
using Procurement.Core.BusinessLogic;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Xunit;

namespace Procurement.Tests.BusinessLogic
{
    /// <summary>
    /// Reproduces the worked example from spec §7 verbatim:
    ///
    /// Gevraagd: 12.000 stuks, reel vereist, originele reel voorkeur
    /// DigiKey:    reel = 5.000  → 15.000 stuks × €0,11
    /// Farnell:    reel = 2.500  → 12.500 stuks × €0,115
    /// Supplier C: reel = 4.000  → 12.000 stuks × €0,105 (re-reel)
    ///
    /// Als ORIGINAL_REEL_REQUIRED = true  → Supplier C afgewezen
    /// Als EITHER_REEL = true             → Supplier C blijft in de vergelijking
    /// </summary>
    public class SupplierSelectionEngineTests
    {
        private static PurchaseRequestLine BuildLine(bool reelRequirement) => new PurchaseRequestLine
        {
            Id = 1,
            Manufacturer = "Vishay",
            ManufacturerPartNumber = "CRCW060310K0FKEA",
            RequestedQuantity = 12000,
            RequiredDate = DateTime.UtcNow.AddDays(30),
            ReelRequirement = reelRequirement,
            PackagingRequirement = PackagingRequirement.OriginalReel
        };

        private static SupplierOffer BuildOffer(string supplierCode, int reelSize, decimal unitPrice, ReelType reelType)
        {
            var orderedQuantity = QuantityCalculator.CalculateOrderedQuantity(12000, reelSize, reelSize);
            var landedCost = LandedCostCalculator.Calculate(orderedQuantity, unitPrice, reelingFee: 0m, shippingCost: 0m, tax: 0m);

            return new SupplierOffer
            {
                PurchaseRequestLineId = 1,
                SupplierCode = supplierCode,
                SupplierPartNumber = supplierCode + "-PN",
                Manufacturer = "Vishay",
                ManufacturerPartNumber = "CRCW060310K0FKEA",
                RequestedQuantity = 12000,
                OfferedQuantity = orderedQuantity,
                UnitPrice = unitPrice,
                Currency = "EUR",
                TotalPrice = orderedQuantity * unitPrice,
                MinimumOrderQuantity = reelSize,
                OrderMultiple = reelSize,
                PackagingType = reelType == ReelType.ManufacturerOriginal ? PackagingType.OriginalReel : PackagingType.ReReel,
                PackagingQuantity = reelSize,
                ReelType = reelType,
                AvailableQuantity = orderedQuantity + 1000,
                LeadTimeDays = 5,
                EstimatedDeliveryDate = DateTime.UtcNow.AddDays(5),
                LandedCost = landedCost,
                MatchConfidence = MatchConfidence.Exact,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            };
        }

        private static List<SupplierOffer> BuildThreeSupplierOffers()
        {
            return new List<SupplierOffer>
            {
                BuildOffer("DIGIKEY", 5000, 0.11m, ReelType.ManufacturerOriginal),
                BuildOffer("FARNELL", 2500, 0.115m, ReelType.ManufacturerOriginal),
                BuildOffer("SUPPLIERC", 4000, 0.105m, ReelType.SupplierReReel)
            };
        }

        [Fact]
        public void QuantityAndLandedCost_MatchWorkedExample()
        {
            var offers = BuildThreeSupplierOffers();

            var digiKey = offers.Find(o => o.SupplierCode == "DIGIKEY");
            var farnell = offers.Find(o => o.SupplierCode == "FARNELL");
            var supplierC = offers.Find(o => o.SupplierCode == "SUPPLIERC");

            Assert.Equal(15000, digiKey.OfferedQuantity);
            Assert.Equal(1650.00m, digiKey.LandedCost);

            Assert.Equal(12500, farnell.OfferedQuantity);
            Assert.Equal(1437.50m, farnell.LandedCost);

            Assert.Equal(12000, supplierC.OfferedQuantity);
            Assert.Equal(1260.00m, supplierC.LandedCost);
        }

        [Fact]
        public void OriginalReelRequired_RejectsReReelSupplier_AndPicksLowestCostAmongTheRest()
        {
            var line = BuildLine(reelRequirement: true);
            var policy = new PackagingPolicy { OriginalReelRequired = true };
            var offers = BuildThreeSupplierOffers();

            var result = SupplierSelectionEngine.SelectBestOffer(offers, line, policy);

            Assert.NotNull(result.SelectedOffer);
            Assert.Equal("FARNELL", result.SelectedOffer.SupplierCode);
            Assert.Contains(result.RejectedOffers, r => r.Offer.SupplierCode == "SUPPLIERC");
        }

        [Fact]
        public void EitherReelAllowed_KeepsReReelSupplier_AndPicksLowestLandedCostOverall()
        {
            var line = BuildLine(reelRequirement: true);
            var policy = new PackagingPolicy { OriginalReelRequired = false };
            var offers = BuildThreeSupplierOffers();

            var result = SupplierSelectionEngine.SelectBestOffer(offers, line, policy);

            Assert.NotNull(result.SelectedOffer);
            Assert.Equal("SUPPLIERC", result.SelectedOffer.SupplierCode);
            Assert.DoesNotContain(result.RejectedOffers, r => r.Offer.SupplierCode == "SUPPLIERC");
        }

        [Fact]
        public void InsufficientStock_IsRejected()
        {
            var line = BuildLine(reelRequirement: false);
            var offers = BuildThreeSupplierOffers();
            offers[0].AvailableQuantity = 100; // DigiKey needs 15000

            var result = SupplierSelectionEngine.SelectBestOffer(offers, line, policy: null);

            Assert.NotEqual("DIGIKEY", result.SelectedOffer?.SupplierCode);
            Assert.Contains(result.RejectedOffers, r => r.Offer.SupplierCode == "DIGIKEY");
        }

        [Fact]
        public void NoValidOffers_ReturnsNullSelection()
        {
            var line = BuildLine(reelRequirement: false);
            var offers = BuildThreeSupplierOffers();
            foreach (var offer in offers) offer.MatchConfidence = MatchConfidence.Low;

            var result = SupplierSelectionEngine.SelectBestOffer(offers, line, policy: null);

            Assert.Null(result.SelectedOffer);
        }
    }
}
