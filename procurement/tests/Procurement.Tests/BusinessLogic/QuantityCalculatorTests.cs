using System;
using Procurement.Core.BusinessLogic;
using Xunit;

namespace Procurement.Tests.BusinessLogic
{
    public class QuantityCalculatorTests
    {
        [Theory]
        [InlineData(12000, 5000, 5000, 15000)]  // spec §7: DigiKey
        [InlineData(12000, 2500, 2500, 12500)]  // spec §7: Farnell
        [InlineData(12000, 4000, 4000, 12000)]  // spec §7: Supplier C — already an exact multiple
        [InlineData(1, 1, 1, 1)]
        [InlineData(100, 1, 1, 100)]             // no packaging multiple constraint
        public void RoundsUpToOrderMultiple_RespectingMoq(int requested, int orderMultiple, int moq, int expected)
        {
            var result = QuantityCalculator.CalculateOrderedQuantity(requested, orderMultiple, moq);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void MoqHigherThanRequested_RoundsUpToMoq()
        {
            var result = QuantityCalculator.CalculateOrderedQuantity(50, 100, 500);
            Assert.Equal(500, result);
        }

        [Fact]
        public void ZeroOrNegativeOrderMultiple_TreatedAsOne()
        {
            var result = QuantityCalculator.CalculateOrderedQuantity(7, 0, 0);
            Assert.Equal(7, result);
        }

        [Fact]
        public void NonPositiveRequestedQuantity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => QuantityCalculator.CalculateOrderedQuantity(0, 10, 10));
        }
    }
}
