using BitvavoBot.Domain.Calculations;
using BitvavoBot.Domain.Enums;
using Xunit;

namespace BitvavoBot.Tests.Calculations;

public sealed class TradingCalculationsTests
{
    [Fact]
    public void BuyLimitPrice_MatchesSpecExample()
    {
        // bid = €100, koopmarge = 1% -> kooplimiet = €99,00
        var result = TradingCalculations.BuyLimitPrice(100m, 1m);
        Assert.Equal(99.00m, result);
    }

    [Fact]
    public void SellLimitPrice_MatchesSpecExample()
    {
        // aankoop = €100, verkoopmarge = 2% -> verkooplimiet = €102,00
        var result = TradingCalculations.SellLimitPrice(100m, 2m);
        Assert.Equal(102.00m, result);
    }

    [Theory]
    [InlineData(8, 10, 80)]
    [InlineData(0, 10, 0)]
    [InlineData(5, 0, 0)]
    public void OpenOrdersPercentage_ComputesCorrectly(int open, int total, decimal expected)
    {
        var result = TradingCalculations.OpenOrdersPercentage(open, total);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void StopLossPrice_ReducesByPercentage()
    {
        var result = TradingCalculations.StopLossPrice(200m, 5m);
        Assert.Equal(190.00m, result);
    }

    [Fact]
    public void TakeProfitPrice_IncreasesByPercentage()
    {
        var result = TradingCalculations.TakeProfitPrice(200m, 10m);
        Assert.Equal(220.00m, result);
    }

    [Fact]
    public void RoundPriceToTickSize_TruncatesWithoutRoundingUp()
    {
        var result = TradingCalculations.RoundPriceToTickSize(1.23456789m, 2);
        Assert.Equal(1.23m, result);
    }

    [Fact]
    public void RoundAmountToStepSize_TruncatesDownward()
    {
        var result = TradingCalculations.RoundAmountToStepSize(0.123456789m, 4);
        Assert.Equal(0.1234m, result);
    }

    [Fact]
    public void ResolveTradeAmount_FixedAmount_IgnoresBalance()
    {
        var result = TradingCalculations.ResolveTradeAmount(TradeAmountMode.FixedAmount, 50m, 10_000m);
        Assert.Equal(50m, result);
    }

    [Fact]
    public void ResolveTradeAmount_Percentage_ScalesWithBalance()
    {
        var result = TradingCalculations.ResolveTradeAmount(TradeAmountMode.PercentageOfBalance, 5m, 1000m);
        Assert.Equal(50m, result);
    }

    [Fact]
    public void InternalPrecision_RetainsAtLeastEightDecimals()
    {
        // Functional spec 4.3: internal math must not lose precision before the final tick-size rounding.
        var buyPrice = TradingCalculations.BuyLimitPrice(0.00000123m, 0.5m);
        Assert.Equal(0.00000123m * 0.995m, buyPrice);
    }
}
