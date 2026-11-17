using System.Collections.Generic;
using Procurement.Core.BusinessLogic;
using Procurement.Core.Entities;
using Procurement.Core.Models;
using Xunit;

namespace Procurement.Tests.BusinessLogic
{
    /// <summary>"Extra voor uitval" (user request, 17 nov 2026), with the starting table from the doc.</summary>
    public class AttritionCalculatorTests
    {
        private static readonly List<AttritionRule> Rules = new List<AttritionRule>
        {
            new AttritionRule { AppliesTo = "Resistors SMT", Percentage = 10m, MinimumExtra = 100 },
            new AttritionRule { AppliesTo = "Integrated Circuits SMT", Percentage = 1m, MinimumExtra = 1 },
            new AttritionRule { AppliesTo = "SMT", MaxUnitPrice = 0.10m, Percentage = 10m, MinimumExtra = 50 },
            new AttritionRule { AppliesTo = "SMT", MinUnitPrice = 0.10m, MaxUnitPrice = 1m, Percentage = 5m, MinimumExtra = 10 },
            new AttritionRule { AppliesTo = "SMT", MinUnitPrice = 1m, MaxUnitPrice = 10m, Percentage = 2m, MinimumExtra = 2 },
            new AttritionRule { AppliesTo = "SMT", MinUnitPrice = 10m, Percentage = 0m, MinimumExtra = 0 },
            new AttritionRule { AppliesTo = "THT", Percentage = 0m, MinimumExtra = 0 }
        };

        private static ComponentClassification Smt(string category) => new ComponentClassification { ComponentType = "SMT", Category = category };

        [Fact]
        public void CategoryRule_BeatsTypeRule()
        {
            // 300 resistors: max(30, 100) = 100 extra (category rule), not the SMT band's 50.
            Assert.Equal(100, AttritionCalculator.CalculateExtra(300, Rules, Smt("Resistors SMT"), 0.0025m));
        }

        [Fact]
        public void TypeRule_PriceBand()
        {
            Assert.Equal(50, AttritionCalculator.CalculateExtra(300, Rules, Smt("Diodes SMT"), 0.05m));
            Assert.Equal(15, AttritionCalculator.CalculateExtra(300, Rules, Smt("Diodes SMT"), 0.50m));
            Assert.Equal(6, AttritionCalculator.CalculateExtra(300, Rules, Smt("Diodes SMT"), 2m));
            Assert.Equal(0, AttritionCalculator.CalculateExtra(300, Rules, Smt("Diodes SMT"), 25m));
        }

        [Fact]
        public void BandUpperBoundIsExclusive()
        {
            // €0.10 falls in the €0.10–€1 band (5%, min 10), not the "under €0.10" band.
            Assert.Equal(10, AttritionCalculator.CalculateExtra(100, Rules, Smt("Diodes SMT"), 0.10m));
        }

        [Fact]
        public void PercentageRoundsUp()
        {
            // 1% of 250 = 2.5 → 3, above the minimum of 1.
            Assert.Equal(3, AttritionCalculator.CalculateExtra(250, Rules, Smt("Integrated Circuits SMT"), 3m));
        }

        [Fact]
        public void UnknownUnitCost_OnlyRulesWithoutPriceBand()
        {
            Assert.Equal(100, AttritionCalculator.CalculateExtra(300, Rules, Smt("Resistors SMT"), null));
            Assert.Equal(0, AttritionCalculator.CalculateExtra(300, Rules, Smt("Diodes SMT"), null));
        }

        [Fact]
        public void ThtAndUnknown_NoExtra()
        {
            Assert.Equal(0, AttritionCalculator.CalculateExtra(300, Rules, new ComponentClassification { ComponentType = "THT" }, 0.01m));
            Assert.Equal(0, AttritionCalculator.CalculateExtra(300, Rules, null, 0.01m));
        }
    }
}
