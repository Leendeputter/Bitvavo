using System.Collections.Generic;
using System.Linq;
using Procurement.Core.BusinessLogic;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Procurement.Core.Models;
using Xunit;

namespace Procurement.Tests.BusinessLogic
{
    /// <summary>Full reels + rest from another variant (user request, 18 nov 2026).</summary>
    public class CombinationOfferBuilderTests
    {
        private static readonly PackagingPolicy Smt = new PackagingPolicy
        {
            ComponentCategory = "SMT",
            AllowedPackaging = new List<PackagingType> { PackagingType.OriginalReel, PackagingType.ReReel },
            ExceptionPriceThreshold = 5m,
            ExceptionPackaging = new List<PackagingType> { PackagingType.CutTape },
            SurplusPenaltyFactor = 0.5m
        };

        private static SupplierPackagingOption Reel() => new SupplierPackagingOption
        {
            SupplierPartNumber = "609-3696-1-ND",
            PackagingType = PackagingType.OriginalReel,
            PackagingQuantity = 600,
            MinimumOrderQuantity = 600,
            AvailableQuantity = 12449,
            PriceBreaks = new List<PriceBreak> { new PriceBreak { BreakQuantity = 600, UnitPrice = 0.70m } }
        };

        private static SupplierPackagingOption Variant(string partNumber, PackagingType type, decimal fee) => new SupplierPackagingOption
        {
            SupplierPartNumber = partNumber,
            PackagingType = type,
            MinimumOrderQuantity = 1,
            ReelingFee = fee,
            AvailableQuantity = 12449,
            PriceBreaks = new List<PriceBreak>
            {
                new PriceBreak { BreakQuantity = 1, UnitPrice = 1.50m },
                new PriceBreak { BreakQuantity = 50, UnitPrice = 1.00m },
                new PriceBreak { BreakQuantity = 100, UnitPrice = 0.90m }
            }
        };

        private static SupplierOffer ReelOffer() => new SupplierOffer
        {
            PurchaseRequestLineId = 1,
            SupplierCode = "DIGIKEY",
            SupplierPartNumber = "609-3696-1-ND",
            RequestedQuantity = 1200,
            AttritionQuantity = 60,
            OfferedQuantity = 1800,
            UnitPrice = 0.70m,
            AvailableQuantity = 12449,
            PackagingType = PackagingType.OriginalReel,
            PackagingQuantity = 600,
            ReelType = ReelType.ManufacturerOriginal
        };

        private static List<SupplierPackagingOption> Options(SupplierPackagingOption reel) => new List<SupplierPackagingOption>
        {
            reel,
            Variant("609-3696-2-ND", PackagingType.CutTape, 0m),
            Variant("609-3696-6-ND", PackagingType.ReReel, 5.50m)
        };

        [Fact]
        public void TwoFullReelsPlusRestOnReReel()
        {
            var reel = Reel();
            var offer = CombinationOfferBuilder.TryBuild(ReelOffer(), reel, Options(reel), 1260, Smt, false, 0.80m);

            Assert.NotNull(offer);
            Assert.True(offer.IsCombination);
            Assert.Equal(1200, offer.MainQuantity);
            Assert.Equal(0.70m, offer.MainUnitPrice);
            Assert.Equal("609-3696-6-ND", offer.RemainderSupplierPartNumber);
            Assert.Equal(PackagingType.ReReel, offer.RemainderPackagingType);
            Assert.Equal(60, offer.RemainderQuantity);
            Assert.Equal(1.00m, offer.RemainderUnitPrice);
            Assert.Equal(1260, offer.OfferedQuantity);
            Assert.Equal(0, offer.SurplusQuantity);
            Assert.Equal(900m, offer.TotalPrice);
            Assert.Equal(905.50m, offer.LandedCost);
            Assert.Equal(905.50m, offer.ComparisonCost);
            Assert.Equal("2× 600 OriginalReel + 60 ReReel", offer.Composition);
        }

        [Fact]
        public void CutTapeAllowedAboveThreshold_IsUsedWhenCheaper()
        {
            var reel = Reel();
            var offer = CombinationOfferBuilder.TryBuild(ReelOffer(), reel, Options(reel), 1260, Smt, false, 6m);

            Assert.Equal("609-3696-2-ND", offer.RemainderSupplierPartNumber);
            Assert.Equal(0m, offer.ReelingFee);
        }

        [Fact]
        public void OriginalReelRequired_NoCombination()
        {
            var reel = Reel();
            Assert.Null(CombinationOfferBuilder.TryBuild(ReelOffer(), reel, Options(reel), 1260, Smt, true, 0.80m));
        }

        [Theory]
        [InlineData(500)]   // less than one reel
        [InlineData(1200)]  // exactly two reels, nothing left over
        public void NoRestOrNoFullReel_NoCombination(int needed)
        {
            var reel = Reel();
            Assert.Null(CombinationOfferBuilder.TryBuild(ReelOffer(), reel, Options(reel), needed, Smt, false, 0.80m));
        }

        [Fact]
        public void NoAllowedRestVariant_NoCombination()
        {
            var reel = Reel();
            var options = Options(reel).Where(o => o.PackagingType != PackagingType.ReReel).ToList();
            Assert.Null(CombinationOfferBuilder.TryBuild(ReelOffer(), reel, options, 1260, Smt, false, 0.80m));
        }

        [Fact]
        public void NotEnoughStockForRest_NoCombination()
        {
            var reel = Reel();
            var options = Options(reel);
            options.Where(o => o.PackagingType != PackagingType.OriginalReel).ToList().ForEach(o => o.AvailableQuantity = 10);
            Assert.Null(CombinationOfferBuilder.TryBuild(ReelOffer(), reel, options, 1260, Smt, false, 0.80m));
        }
    }
}
