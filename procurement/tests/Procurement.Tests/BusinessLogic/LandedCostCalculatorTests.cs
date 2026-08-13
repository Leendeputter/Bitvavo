using Procurement.Core.BusinessLogic;
using Xunit;

namespace Procurement.Tests.BusinessLogic
{
    public class LandedCostCalculatorTests
    {
        [Fact]
        public void SumsProductCostReelingShippingAndTax()
        {
            var result = LandedCostCalculator.Calculate(
                orderedQuantity: 1000, unitPrice: 0.10m, reelingFee: 25m, shippingCost: 15m, tax: 5m);

            Assert.Equal(145m, result); // 1000*0.10 + 25 + 15 + 5
        }

        [Fact]
        public void NoFees_EqualsProductCostOnly()
        {
            var result = LandedCostCalculator.Calculate(15000, 0.11m, 0m, 0m, 0m);
            Assert.Equal(1650.00m, result);
        }
    }
}
