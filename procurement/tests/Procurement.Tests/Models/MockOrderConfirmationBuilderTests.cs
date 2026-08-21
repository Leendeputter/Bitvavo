using System;
using Procurement.Core.Mocking;
using Procurement.Core.Models;
using Xunit;

namespace Procurement.Tests.Models
{
    /// <summary>
    /// MockOrderConfirmationBuilder is what makes "Orderbevestiging verwerken" show anything at all
    /// in mock mode (the only mode available before real supplier credentials exist) — these fixed
    /// (supplierOrderNumber, part) pairs were found by independently reimplementing DeterministicMock's
    /// FNV-1a hash in Python (see the commit that added this test) and searching for seeds that land
    /// in each of the five deviation branches, so the exact expected outputs are pinned against that
    /// independent computation rather than re-deriving them from the same C# code under test.
    /// </summary>
    public class MockOrderConfirmationBuilderTests
    {
        private static SupplierOrderRequestLine OrderedLine() => new SupplierOrderRequestLine
        {
            SupplierPartNumber = "TESTPART-001",
            Quantity = 100,
            UnitPrice = 2.50m
        };

        [Fact]
        public void BuildLine_IsDeterministic_ForTheSameInput()
        {
            var line = OrderedLine();
            var first = MockOrderConfirmationBuilder.BuildLine("DK000103", line);
            var second = MockOrderConfirmationBuilder.BuildLine("DK000103", line);

            Assert.Equal(first.ConfirmedQuantity, second.ConfirmedQuantity);
            Assert.Equal(first.ConfirmedUnitPrice, second.ConfirmedUnitPrice);
        }

        [Fact]
        public void BuildLine_CleanSeed_MatchesOrderExactly()
        {
            var result = MockOrderConfirmationBuilder.BuildLine("DK000000", OrderedLine());

            Assert.Equal(100, result.ConfirmedQuantity);
            Assert.Equal(2.50m, result.ConfirmedUnitPrice);
            AssertLeadDaysInRange(result.EstimatedShipDate, minDays: 2, maxDays: 7);
        }

        [Fact]
        public void BuildLine_PriceDeviationSeed_ConfirmsFivePercentHigher()
        {
            var result = MockOrderConfirmationBuilder.BuildLine("DK000003", OrderedLine());

            Assert.Equal(100, result.ConfirmedQuantity);
            Assert.Equal(2.625m, result.ConfirmedUnitPrice);
            AssertLeadDaysInRange(result.EstimatedShipDate, minDays: 2, maxDays: 7);
        }

        [Fact]
        public void BuildLine_LateDeliverySeed_QuantityAndPriceUnchanged_ShipDateFarOut()
        {
            var result = MockOrderConfirmationBuilder.BuildLine("DK000004", OrderedLine());

            Assert.Equal(100, result.ConfirmedQuantity);
            Assert.Equal(2.50m, result.ConfirmedUnitPrice);
            AssertLeadDaysInRange(result.EstimatedShipDate, minDays: 10, maxDays: 21);
        }

        [Fact]
        public void BuildLine_QuantityDeviationSeed_ConfirmsFewerThanOrdered()
        {
            var result = MockOrderConfirmationBuilder.BuildLine("DK000039", OrderedLine());

            Assert.Equal(91, result.ConfirmedQuantity);
            Assert.Equal(2.50m, result.ConfirmedUnitPrice);
            AssertLeadDaysInRange(result.EstimatedShipDate, minDays: 2, maxDays: 7);
        }

        [Fact]
        public void BuildLine_AllThreeDeviationsSeed_CombinesAllOfThem()
        {
            var result = MockOrderConfirmationBuilder.BuildLine("DK000103", OrderedLine());

            Assert.Equal(95, result.ConfirmedQuantity);
            Assert.Equal(2.625m, result.ConfirmedUnitPrice);
            AssertLeadDaysInRange(result.EstimatedShipDate, minDays: 10, maxDays: 21);
        }

        [Fact]
        public void BuildLine_NeverConfirmsBelowOneUnit_EvenForVerySmallOrders()
        {
            var tinyLine = new SupplierOrderRequestLine { SupplierPartNumber = "X", Quantity = 1, UnitPrice = 1m };

            // Sweep enough distinct order numbers to hit the quantity-deviation branch at least once
            // for a quantity of 1 — Max(1, ...) must hold even when the ordered quantity itself
            // leaves no room to reduce.
            for (var i = 0; i < 200; i++)
            {
                var result = MockOrderConfirmationBuilder.BuildLine($"SWEEP{i}", tinyLine);
                Assert.True(result.ConfirmedQuantity >= 1);
            }
        }

        // The 2/7 vs 10/21 day windows never overlap, so a coarse "> 8 days from now" split reliably
        // tells the two branches apart without pinning the non-deterministic DateTime.UtcNow anchor
        // itself (only the deterministic lead-days offset from it is under test here).
        private static void AssertLeadDaysInRange(DateTime? estimatedShipDate, int minDays, int maxDays)
        {
            Assert.True(estimatedShipDate.HasValue);
            var daysFromNow = (estimatedShipDate.Value - DateTime.UtcNow).TotalDays;
            Assert.InRange(daysFromNow, minDays - 0.01, maxDays + 0.01);
        }
    }
}
