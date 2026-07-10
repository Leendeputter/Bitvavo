using BitvavoBot.Domain.Entities;
using Xunit;

namespace BitvavoBot.Tests.Calculations;

public sealed class PositionTests
{
    [Fact]
    public void ApplyBuy_RecalculatesWeightedAveragePrice()
    {
        var position = new Position();

        position.ApplyBuy(1m, 100m);
        Assert.Equal(1m, position.Amount);
        Assert.Equal(100m, position.AverageEntryPrice);

        position.ApplyBuy(1m, 200m);
        Assert.Equal(2m, position.Amount);
        Assert.Equal(150m, position.AverageEntryPrice);
    }

    [Fact]
    public void ApplySell_ClosesPositionWhenFullyReduced()
    {
        var position = new Position();
        position.ApplyBuy(2m, 100m);

        position.ApplySell(2m);

        Assert.Equal(0m, position.Amount);
        Assert.False(position.IsOpen);
        Assert.NotNull(position.ClosedAt);
    }

    [Fact]
    public void ApplySell_PartialReduction_KeepsPositionOpenWithSameAveragePrice()
    {
        var position = new Position();
        position.ApplyBuy(4m, 100m);

        position.ApplySell(1m);

        Assert.Equal(3m, position.Amount);
        Assert.Equal(100m, position.AverageEntryPrice);
        Assert.True(position.IsOpen);
    }
}
