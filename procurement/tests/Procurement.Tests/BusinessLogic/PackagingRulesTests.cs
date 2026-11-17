using System.Collections.Generic;
using System.Linq;
using Procurement.Core.BusinessLogic;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Procurement.Core.Models;
using Xunit;

namespace Procurement.Tests.BusinessLogic
{
    /// <summary>Packaging policy per component type/category (user request, 17 nov 2026).</summary>
    public class PackagingRulesTests
    {
        private static PackagingPolicy Policy(string name, decimal? threshold, PackagingType[] allowed, PackagingType[] exception) => new PackagingPolicy
        {
            ComponentCategory = name,
            AllowedPackaging = allowed.ToList(),
            ExceptionPriceThreshold = threshold,
            ExceptionPackaging = exception.ToList()
        };

        private static readonly List<PackagingPolicy> Policies = new List<PackagingPolicy>
        {
            Policy("Default", null, new[] { PackagingType.OriginalReel, PackagingType.CutTape }, new PackagingType[0]),
            Policy("SMT", 5m, new[] { PackagingType.OriginalReel, PackagingType.ReReel }, new[] { PackagingType.CutTape, PackagingType.Tray }),
            Policy("Integrated Circuits SMT", 2m, new[] { PackagingType.OriginalReel, PackagingType.ReReel }, new[] { PackagingType.CutTape, PackagingType.Tray }),
            Policy("THT", 5m, new[] { PackagingType.Tube, PackagingType.Tray, PackagingType.CutTape }, new[] { PackagingType.Bulk })
        };

        [Fact]
        public void ResolvePolicy_CategoryBeatsComponentType()
        {
            var policy = PackagingRules.ResolvePolicy(Policies, new ComponentClassification { ComponentType = "SMT", Category = "Integrated Circuits SMT" });
            Assert.Equal("Integrated Circuits SMT", policy.ComponentCategory);
        }

        [Fact]
        public void ResolvePolicy_UnknownCategory_FallsBackToComponentType()
        {
            var policy = PackagingRules.ResolvePolicy(Policies, new ComponentClassification { ComponentType = "SMT", Category = "Resistors SMT" });
            Assert.Equal("SMT", policy.ComponentCategory);
        }

        [Fact]
        public void ResolvePolicy_NoClassification_FallsBackToDefault()
        {
            Assert.Equal("Default", PackagingRules.ResolvePolicy(Policies, null).ComponentCategory);
        }

        [Fact]
        public void ResolvePolicy_IgnoresCaseAndNonBreakingSpaces()
        {
            var policy = PackagingRules.ResolvePolicy(Policies, new ComponentClassification { Category = "integrated circuits  SMT" });
            Assert.Equal("Integrated Circuits SMT", policy.ComponentCategory);
        }

        [Fact]
        public void Smt_BelowThreshold_OnlyReels()
        {
            var allowed = PackagingRules.GetAllowedPackaging(Policies[1], 0.01m);
            Assert.True(PackagingRules.IsAllowed(PackagingType.OriginalReel, allowed));
            Assert.True(PackagingRules.IsAllowed(PackagingType.ReReel, allowed));
            Assert.False(PackagingRules.IsAllowed(PackagingType.CutTape, allowed));
            Assert.False(PackagingRules.IsAllowed(PackagingType.Any, allowed));
        }

        [Fact]
        public void Smt_AtThreshold_CutTapeAndTrayToo()
        {
            var allowed = PackagingRules.GetAllowedPackaging(Policies[1], 5m);
            Assert.True(PackagingRules.IsAllowed(PackagingType.CutTape, allowed));
            Assert.True(PackagingRules.IsAllowed(PackagingType.Tray, allowed));
        }

        [Fact]
        public void Tht_BulkOnlyAboveThreshold()
        {
            Assert.False(PackagingRules.IsAllowed(PackagingType.Bulk, PackagingRules.GetAllowedPackaging(Policies[3], 1m)));
            Assert.True(PackagingRules.IsAllowed(PackagingType.Bulk, PackagingRules.GetAllowedPackaging(Policies[3], 6m)));
        }

        [Fact]
        public void Default_AllowsUnknownPackaging()
        {
            Assert.True(PackagingRules.IsAllowed(PackagingType.Any, PackagingRules.GetAllowedPackaging(Policies[0], null)));
        }

        [Fact]
        public void NoPolicyOrEmptyList_NoRestriction()
        {
            Assert.Null(PackagingRules.GetAllowedPackaging(null, 1m));
            Assert.Null(PackagingRules.GetAllowedPackaging(new PackagingPolicy { ComponentCategory = "SMT" }, 1m));
            Assert.True(PackagingRules.IsAllowed(PackagingType.Bulk, null));
        }

        [Fact]
        public void ComparisonCost_CountsSurplusAtFactorTimesUnitCost()
        {
            // Doc example: 300 needed, full reel of 5000 at €0.0025 = €12.50, 4700 surplus.
            var cost = PackagingRules.CalculateComparisonCost(12.50m, 4700, 0.0025m, 0.5m);
            Assert.Equal(12.50m + 0.5m * 4700 * 0.0025m, cost);
        }

        [Fact]
        public void ComparisonCost_ZeroFactor_IsLandedCost()
        {
            Assert.Equal(12.50m, PackagingRules.CalculateComparisonCost(12.50m, 4700, 0.0025m, 0m));
        }
    }
}
