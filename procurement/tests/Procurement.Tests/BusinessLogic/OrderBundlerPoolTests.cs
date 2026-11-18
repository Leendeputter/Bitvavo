using System.Collections.Generic;
using System.Linq;
using Procurement.Core.BusinessLogic;
using Xunit;

namespace Procurement.Tests.BusinessLogic
{
    /// <summary>Spreading reels + rest over the lines of a bundle (sourcing per article, 18 nov 2026).</summary>
    public class OrderBundlerPoolTests
    {
        [Fact]
        public void SingleLine_GetsBothPoolsExactly()
        {
            var result = OrderBundler.AllocateAcrossPools(new List<int> { 1260 }, new List<int> { 1200, 60 });

            Assert.Equal(new[] { 1200, 60 }, result[0]);
        }

        [Fact]
        public void Lines_FillFirstPoolThenRest_EarliestFirst()
        {
            // 945 + 3100 + 2600 = 6645 → 6000 on reels, 645 on re-reel.
            var result = OrderBundler.AllocateAcrossPools(new List<int> { 945, 3100, 2600 }, new List<int> { 6000, 645 });

            Assert.Equal(new[] { 945, 0 }, result[0]);
            Assert.Equal(new[] { 3100, 0 }, result[1]);
            Assert.Equal(new[] { 1955, 645 }, result[2]);
            Assert.Equal(6645, result.Sum(r => r.Sum()));
        }

        [Fact]
        public void Surplus_GoesToEarliestLine()
        {
            // Rest rounded up to its MOQ: pools hold 10 more than needed.
            var result = OrderBundler.AllocateAcrossPools(new List<int> { 100, 200 }, new List<int> { 250, 60 });

            Assert.Equal(110, result[0].Sum());
            Assert.Equal(200, result[1].Sum());
            Assert.Equal(new[] { 110, 0 }, result[0]);
            Assert.Equal(new[] { 140, 60 }, result[1]);
        }
    }
}
