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

        // Packaging policy per component type (17 nov 2026).

        private static SupplierOffer BuildVariant(PackagingType packaging, int offered, decimal unitPrice, decimal reelingFee, int needed)
        {
            var landed = LandedCostCalculator.Calculate(offered, unitPrice, reelingFee, 0m, 0m);
            return new SupplierOffer
            {
                SupplierCode = "DIGIKEY",
                SupplierPartNumber = "DK-" + packaging,
                RequestedQuantity = needed,
                OfferedQuantity = offered,
                UnitPrice = unitPrice,
                ReelingFee = reelingFee,
                PackagingType = packaging,
                ReelType = packaging == PackagingType.ReReel ? ReelType.SupplierReReel : (ReelType?)null,
                AvailableQuantity = 100000,
                EstimatedDeliveryDate = DateTime.UtcNow.AddDays(2),
                LandedCost = landed,
                SurplusQuantity = offered - needed,
                ComparisonCost = PackagingRules.CalculateComparisonCost(landed, offered - needed, 0.0025m, 0.5m),
                MatchConfidence = MatchConfidence.Exact
            };
        }

        private static PackagingPolicy SmtPolicy() => new PackagingPolicy
        {
            ComponentCategory = "SMT",
            AllowedPackaging = new List<PackagingType> { PackagingType.OriginalReel, PackagingType.ReReel },
            ExceptionPriceThreshold = 5m,
            ExceptionPackaging = new List<PackagingType> { PackagingType.CutTape, PackagingType.Tray },
            SurplusPenaltyFactor = 0.5m
        };

        private static PurchaseRequestLine SmtLine(int quantity) => new PurchaseRequestLine
        {
            Id = 2,
            RequestedQuantity = quantity,
            RequiredDate = DateTime.UtcNow.AddDays(30)
        };

        [Fact]
        public void SmtPolicy_RejectsCutTape_ForCheapPart()
        {
            var offers = new List<SupplierOffer>
            {
                BuildVariant(PackagingType.CutTape, 300, 0.01m, 0m, 300),
                BuildVariant(PackagingType.ReReel, 300, 0.012m, 7m, 300)
            };

            var result = SupplierSelectionEngine.SelectBestOffer(offers, SmtLine(300), SmtPolicy(), referenceUnitCost: 0.0025m);

            Assert.Equal(PackagingType.ReReel, result.SelectedOffer.PackagingType);
            Assert.Contains(result.RejectedOffers, r => r.Offer.PackagingType == PackagingType.CutTape);
        }

        [Fact]
        public void SmtPolicy_SmallQuantity_ReReelBeatsFullReel()
        {
            // Doc example: 300 pcs — re-reel €10.60 vs full reel €12.50 plus 4700 surplus.
            var offers = new List<SupplierOffer>
            {
                BuildVariant(PackagingType.OriginalReel, 5000, 0.0025m, 0m, 300),
                BuildVariant(PackagingType.ReReel, 300, 0.012m, 7m, 300)
            };

            var result = SupplierSelectionEngine.SelectBestOffer(offers, SmtLine(300), SmtPolicy(), referenceUnitCost: 0.0025m);

            Assert.Equal(PackagingType.ReReel, result.SelectedOffer.PackagingType);
        }

        [Fact]
        public void SmtPolicy_LargeQuantity_FullReelBeatsReReel()
        {
            // Doc example: 4800 pcs — full reel €12.50 (+ small surplus penalty) vs re-reel €26.20.
            var offers = new List<SupplierOffer>
            {
                BuildVariant(PackagingType.OriginalReel, 5000, 0.0025m, 0m, 4800),
                BuildVariant(PackagingType.ReReel, 4800, 0.004m, 7m, 4800)
            };

            var result = SupplierSelectionEngine.SelectBestOffer(offers, SmtLine(4800), SmtPolicy(), referenceUnitCost: 0.0025m);

            Assert.Equal(PackagingType.OriginalReel, result.SelectedOffer.PackagingType);
        }

        [Fact]
        public void SmtPolicy_ExpensivePart_AllowsCutTape()
        {
            var offers = new List<SupplierOffer>
            {
                BuildVariant(PackagingType.CutTape, 10, 8m, 0m, 10),
                BuildVariant(PackagingType.ReReel, 10, 8m, 7m, 10)
            };

            var result = SupplierSelectionEngine.SelectBestOffer(offers, SmtLine(10), SmtPolicy(), referenceUnitCost: 8m);

            Assert.Equal(PackagingType.CutTape, result.SelectedOffer.PackagingType);
        }
    }
}
