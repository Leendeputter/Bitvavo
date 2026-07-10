using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Models;
using BitvavoBot.Trading.Strategies;
using Xunit;

namespace BitvavoBot.Tests.Strategies;

public sealed class StrategyTests
{
    private static Ticker MakeTicker(decimal last) => new("BTC-EUR", last, last, last, 0m, 0m, last, last, DateTimeOffset.UtcNow);

    [Fact]
    public void GridTrading_SameInputSequence_ProducesIdenticalSignals()
    {
        var prices = new[] { 100m, 99m, 98m, 97m, 96m, 101m, 102m };

        IReadOnlyList<StrategySignal> Run()
        {
            var strategy = new GridTradingStrategy();
            strategy.Configure("""{"gridSpacingPercentage":1,"gridCount":3}""");
            var signals = new List<StrategySignal>();
            foreach (var price in prices)
            {
                signals.AddRange(strategy.OnMarketData(new MarketDataEvent("BTC-EUR", MakeTicker(price))));
            }
            return signals;
        }

        var run1 = Run();
        var run2 = Run();

        Assert.Equal(run1.Count, run2.Count);
        Assert.True(run1.Count > 0);
        for (var i = 0; i < run1.Count; i++)
        {
            Assert.Equal(run1[i].Action, run2[i].Action);
            Assert.Equal(run1[i].SuggestedPrice, run2[i].SuggestedPrice);
        }
    }

    [Fact]
    public void GridTrading_BuyLevel_TriggersOnlyOnce()
    {
        var strategy = new GridTradingStrategy();
        strategy.Configure("""{"gridSpacingPercentage":1,"gridCount":2}""");

        strategy.OnMarketData(new MarketDataEvent("BTC-EUR", MakeTicker(100m))); // anchors base price
        var firstDrop = strategy.OnMarketData(new MarketDataEvent("BTC-EUR", MakeTicker(98m)));
        var stillLow = strategy.OnMarketData(new MarketDataEvent("BTC-EUR", MakeTicker(97.5m)));

        Assert.Contains(firstDrop, s => s.Action == SignalAction.Buy);
        Assert.DoesNotContain(stillLow, s => s.Action == SignalAction.Buy);
    }

    [Fact]
    public void MovingAverage_GoldenCross_EmitsBuySignal()
    {
        var strategy = new MovingAverageStrategy();
        strategy.Configure("""{"fastPeriod":2,"slowPeriod":4}""");

        // Declining then sharply rising closes should produce a golden cross (fast crossing above slow).
        var closes = new decimal[] { 100, 99, 98, 97, 96, 110, 120, 130 };
        var signals = new List<StrategySignal>();
        foreach (var close in closes)
        {
            var bar = new PriceBar(DateTimeOffset.UtcNow, close, close, close, close, 0m);
            signals.AddRange(strategy.OnMarketData(new MarketDataEvent("BTC-EUR", MakeTicker(close), bar)));
        }

        Assert.Contains(signals, s => s.Action == SignalAction.Buy);
    }

    [Fact]
    public void MeanReversion_RsiBelowBuyLevel_EmitsBuySignalOnce()
    {
        var strategy = new MeanReversionStrategy();
        strategy.Configure("""{"rsiPeriod":3,"rsiBuyLevel":30,"rsiSellLevel":70}""");

        // A sustained decline should push RSI below 30.
        var closes = new decimal[] { 100, 95, 90, 85, 80, 75, 70 };
        var signals = new List<StrategySignal>();
        foreach (var close in closes)
        {
            var bar = new PriceBar(DateTimeOffset.UtcNow, close, close, close, close, 0m);
            signals.AddRange(strategy.OnMarketData(new MarketDataEvent("BTC-EUR", MakeTicker(close), bar)));
        }

        var buySignals = signals.Where(s => s.Action == SignalAction.Buy).ToList();
        Assert.Single(buySignals);
    }

    [Fact]
    public void MarketMaking_QuotesSymmetricallyAroundMidPrice()
    {
        var strategy = new MarketMakingStrategy();
        strategy.Configure("""{"spreadPercentage":2,"orderSize":1,"levels":1}""");

        var ticker = new Ticker("BTC-EUR", 99m, 101m, 100m, 0m, 0m, 101m, 99m, DateTimeOffset.UtcNow);
        var signals = strategy.OnMarketData(new MarketDataEvent("BTC-EUR", ticker));

        var buy = Assert.Single(signals, s => s.Action == SignalAction.Buy);
        var sell = Assert.Single(signals, s => s.Action == SignalAction.Sell);
        var mid = (99m + 101m) / 2m;
        Assert.True(buy.SuggestedPrice < mid);
        Assert.True(sell.SuggestedPrice > mid);
    }
}
