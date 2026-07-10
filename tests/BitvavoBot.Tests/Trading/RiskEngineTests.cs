using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Tests.Fakes;
using BitvavoBot.Trading;
using Xunit;

namespace BitvavoBot.Tests.Trading;

public sealed class RiskEngineTests
{
    private static BotProfile MakeProfile(Action<BotProfile>? configure = null)
    {
        var profile = new BotProfile
        {
            Id = 1,
            Name = "TestProfile",
            Mode = TradingMode.Live,
            MaxTradesPerDay = 0,
            MaxLossPerDay = 0,
            MaxInvestment = 0,
            MaxConcurrentOrders = 0,
            MinOpenOrdersPercentage = 80m
        };
        configure?.Invoke(profile);
        return profile;
    }

    [Fact]
    public async Task CheckBeforePlacingOrder_BlocksWhenMaxConcurrentOrdersReached()
    {
        var orders = new InMemoryOrderRepository();
        var trades = new InMemoryTradeRepository { Orders = orders.Orders };
        var positions = new InMemoryPositionRepository();
        var engine = new RiskEngine(orders, trades, positions);
        var profile = MakeProfile(p => p.MaxConcurrentOrders = 1);

        await orders.AddAsync(new Order { Mode = TradingMode.Live, BotProfileName = profile.Name, Status = OrderStatus.Open, Market = "BTC-EUR" });

        var result = await engine.CheckBeforePlacingOrderAsync(profile, TradingMode.Live, candidateOrderQuoteValue: 10m);

        Assert.False(result.CanPlaceOrder);
        Assert.Equal(RiskBlockReason.MaxConcurrentOrdersReached, result.Reason);
    }

    [Fact]
    public async Task CheckBeforePlacingOrder_BlocksWhenMaxInvestmentWouldBeExceeded()
    {
        var orders = new InMemoryOrderRepository();
        var trades = new InMemoryTradeRepository { Orders = orders.Orders };
        var positions = new InMemoryPositionRepository();
        var engine = new RiskEngine(orders, trades, positions);
        var profile = MakeProfile(p => p.MaxInvestment = 100m);

        var result = await engine.CheckBeforePlacingOrderAsync(profile, TradingMode.Live, candidateOrderQuoteValue: 150m);

        Assert.False(result.CanPlaceOrder);
        Assert.Equal(RiskBlockReason.MaxInvestmentReached, result.Reason);
    }

    [Fact]
    public async Task CheckBeforePlacingOrder_AllowsWhenWithinAllLimits()
    {
        var orders = new InMemoryOrderRepository();
        var trades = new InMemoryTradeRepository { Orders = orders.Orders };
        var positions = new InMemoryPositionRepository();
        var engine = new RiskEngine(orders, trades, positions);
        var profile = MakeProfile();

        var result = await engine.CheckBeforePlacingOrderAsync(profile, TradingMode.Live, candidateOrderQuoteValue: 50m);

        Assert.True(result.CanPlaceOrder);
        Assert.Equal(RiskBlockReason.None, result.Reason);
    }

    [Fact]
    public async Task IsDailyLossLimitExceeded_TrueWhenTodaysRealizedLossExceedsLimit()
    {
        var orders = new InMemoryOrderRepository();
        var trades = new InMemoryTradeRepository { Orders = orders.Orders };
        var positions = new InMemoryPositionRepository();
        var engine = new RiskEngine(orders, trades, positions);
        var profile = MakeProfile(p => p.MaxLossPerDay = 50m);

        var order = await orders.AddAsync(new Order { Mode = TradingMode.Live, BotProfileName = profile.Name, Market = "BTC-EUR", Status = OrderStatus.Filled });
        await trades.AddAsync(new Trade { Mode = TradingMode.Live, OrderId = order.Id, ProfitLoss = -60m, Timestamp = DateTimeOffset.UtcNow });

        var exceeded = await engine.IsDailyLossLimitExceededAsync(profile, TradingMode.Live);

        Assert.True(exceeded);
    }

    [Fact]
    public async Task EvaluateOpenOrders_FlagsBelowThresholdAndSuggestsReplenish()
    {
        var orders = new InMemoryOrderRepository();
        var trades = new InMemoryTradeRepository { Orders = orders.Orders };
        var positions = new InMemoryPositionRepository();
        var engine = new RiskEngine(orders, trades, positions);
        var profile = MakeProfile(p => { p.MinOpenOrdersPercentage = 80m; p.AutoReplenishOpenOrders = true; p.MaxConcurrentOrders = 10; });

        // 1 open out of 5 total placed = 20%, below the 80% threshold.
        await orders.AddAsync(new Order { Mode = TradingMode.Live, BotProfileName = profile.Name, Status = OrderStatus.Open, Market = "BTC-EUR" });
        for (var i = 0; i < 4; i++)
        {
            await orders.AddAsync(new Order { Mode = TradingMode.Live, BotProfileName = profile.Name, Status = OrderStatus.Filled, Market = "BTC-EUR" });
        }

        var health = await engine.EvaluateOpenOrdersAsync(profile, TradingMode.Live);

        Assert.Equal(20m, health.OpenPercentage);
        Assert.True(health.BelowThreshold);
        Assert.True(health.ShouldAutoReplenish);
    }
}
