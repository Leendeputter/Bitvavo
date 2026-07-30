using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Models;
using BitvavoBot.Exchange.Paper;
using BitvavoBot.Tests.Fakes;
using Xunit;

namespace BitvavoBot.Tests.Paper;

public sealed class PaperExchangeClientTests
{
    private static Ticker MakeTicker(string market, decimal bid, decimal ask, decimal last = 0m) =>
        new(market, bid, ask, last == 0m ? (bid + ask) / 2m : last, 0m, 0m, ask, bid, DateTimeOffset.UtcNow);

    private (PaperExchangeClient Client, FakeMarketDataFeed Feed, InMemoryOrderRepository Orders, InMemoryTradeRepository Trades, InMemoryPositionRepository Positions, InMemoryPapertradingProfileRepository Profiles, InMemoryLogRepository Logs) CreateSut(decimal startingBalance = 10_000m)
    {
        var feed = new FakeMarketDataFeed();
        var orders = new InMemoryOrderRepository();
        var trades = new InMemoryTradeRepository { Orders = orders.Orders };
        var positions = new InMemoryPositionRepository();
        var profiles = new InMemoryPapertradingProfileRepository();
        var logs = new InMemoryLogRepository();
        profiles.Profiles.Add(new Domain.Entities.PapertradingProfile
        {
            Id = 1,
            Name = "TestProfile",
            QuoteCurrency = "EUR",
            StartingBalance = startingBalance,
            CurrentBalance = startingBalance
        });

        var client = new PaperExchangeClient(feed, orders, trades, positions, profiles, logs, "TestProfile");
        return (client, feed, orders, trades, positions, profiles, logs);
    }

    [Fact]
    public async Task MarketBuyOrder_FillsImmediatelyAtAskPrice()
    {
        var (client, feed, _, _, positions, _, _) = CreateSut();
        feed.SetTicker(MakeTicker("BTC-EUR", bid: 99m, ask: 100m));

        var request = new PlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Market, 1m, null, "Profile1", null);
        var result = await client.PlaceOrderAsync(request);

        Assert.Equal(OrderStatus.Filled, result.Status);
        Assert.Equal(100m, result.AverageFillPrice);
        Assert.Equal(1m, result.FilledAmount);

        var position = await positions.GetOpenPositionAsync(TradingMode.Paper, "BTC", "TestProfile");
        Assert.NotNull(position);
        Assert.Equal(1m, position!.Amount);
        Assert.Equal(100m, position.AverageEntryPrice);
    }

    [Fact]
    public async Task MarketBuyOrder_NeverReachesTheRealExchange_OnlySimulatesLocally()
    {
        // No real Bitvavo dependency exists in this test at all: the client only talks to the
        // fake market data feed and in-memory repositories, proving Paper mode cannot leak to Live.
        var (client, feed, orders, _, _, _, _) = CreateSut();
        feed.SetTicker(MakeTicker("BTC-EUR", bid: 99m, ask: 100m));

        await client.PlaceOrderAsync(new PlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Market, 1m, null, "Profile1", null));

        var order = Assert.Single(orders.Orders);
        Assert.Equal(TradingMode.Paper, order.Mode);
        Assert.StartsWith("PAPER-", order.ExternalId);
    }

    [Fact]
    public async Task LimitBuyOrder_StaysOpenUntilAskReachesLimit()
    {
        var (client, feed, orders, _, _, _, _) = CreateSut();
        feed.SetTicker(MakeTicker("BTC-EUR", bid: 100m, ask: 101m));

        var request = new PlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Limit, 1m, 95m, "Profile1", null);
        var result = await client.PlaceOrderAsync(request);
        Assert.Equal(OrderStatus.Open, result.Status);

        // Price has not reached the limit yet: still open.
        feed.SetTicker(MakeTicker("BTC-EUR", bid: 96m, ask: 97m));
        var stillOpen = await client.GetOpenOrdersAsync("BTC-EUR");
        Assert.Single(stillOpen);

        // Ask drops to/through the limit: order fills at the limit price.
        feed.SetTicker(MakeTicker("BTC-EUR", bid: 94m, ask: 95m));
        var openAfterFill = await client.GetOpenOrdersAsync("BTC-EUR");
        Assert.Empty(openAfterFill);

        var filledOrder = Assert.Single(orders.Orders);
        Assert.Equal(OrderStatus.Filled, filledOrder.Status);
        Assert.Equal(95m, filledOrder.AverageFillPrice);
    }

    [Fact]
    public async Task LimitSellOrder_FillsWhenBidReachesOrPassesLimit()
    {
        var (client, feed, orders, _, _, _, _) = CreateSut();
        feed.SetTicker(MakeTicker("BTC-EUR", bid: 100m, ask: 101m));
        await client.PlaceOrderAsync(new PlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Market, 1m, null, "Profile1", null));

        var sellRequest = new PlaceOrderRequest("BTC-EUR", OrderSide.Sell, OrderType.Limit, 1m, 110m, "Profile1", null);
        await client.PlaceOrderAsync(sellRequest);

        feed.SetTicker(MakeTicker("BTC-EUR", bid: 105m, ask: 106m));
        Assert.Single(await client.GetOpenOrdersAsync("BTC-EUR"));

        feed.SetTicker(MakeTicker("BTC-EUR", bid: 111m, ask: 112m));
        Assert.Empty(await client.GetOpenOrdersAsync("BTC-EUR"));

        var sellOrder = orders.Orders.Single(o => o.Side == OrderSide.Sell);
        Assert.Equal(OrderStatus.Filled, sellOrder.Status);
        Assert.Equal(110m, sellOrder.AverageFillPrice);
    }

    [Fact]
    public async Task Fees_AreDeductedFromBalanceOnFill()
    {
        var (client, feed, _, _, _, profiles, _) = CreateSut(startingBalance: 10_000m);
        feed.FeeSchedule = new FeeSchedule(MakerFeePercentage: 0m, TakerFeePercentage: 1m);
        feed.SetTicker(MakeTicker("BTC-EUR", bid: 99m, ask: 100m));

        await client.PlaceOrderAsync(new PlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Market, 1m, null, "Profile1", null));

        var profile = await profiles.GetByNameAsync("TestProfile");
        // 100 (cost) + 1% taker fee (1.00) = 101 spent.
        Assert.Equal(10_000m - 101m, profile!.CurrentBalance);
    }

    [Fact]
    public async Task CancelOrder_RemovesFromOpenOrdersWithoutFilling()
    {
        var (client, feed, orders, _, _, _, _) = CreateSut();
        feed.SetTicker(MakeTicker("BTC-EUR", bid: 100m, ask: 101m));

        var placed = await client.PlaceOrderAsync(new PlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Limit, 1m, 50m, "Profile1", null));
        await client.CancelOrderAsync("BTC-EUR", placed.ExternalId);

        Assert.Empty(await client.GetOpenOrdersAsync("BTC-EUR"));
        var order = orders.Orders.Single();
        Assert.Equal(OrderStatus.Cancelled, order.Status);

        // Even if the price later crosses the (cancelled) limit, it must not fill.
        feed.SetTicker(MakeTicker("BTC-EUR", bid: 10m, ask: 11m));
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    /// <summary>
    /// Regression test: placing an order and it later actually filling are distinct, separately
    /// logged events — previously only "order placed" was logged anywhere, so a user had no way
    /// to tell from the Logging screen whether a pending limit order had ever actually filled.
    /// </summary>
    [Fact]
    public async Task LimitOrderFill_IsLoggedDistinctlyFromPlacement()
    {
        var (client, feed, _, _, _, _, logs) = CreateSut();
        feed.SetTicker(MakeTicker("BTC-EUR", bid: 100m, ask: 101m));

        await client.PlaceOrderAsync(new PlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Limit, 1m, 95m, "Profile1", null));
        Assert.DoesNotContain(logs.Entries, e => e.Message.Contains("FILLED"));

        feed.SetTicker(MakeTicker("BTC-EUR", bid: 94m, ask: 95m));

        Assert.Contains(logs.Entries, e => e.Message.Contains("FILLED"));
    }

    [Fact]
    public async Task CancelOrder_IsLogged()
    {
        var (client, feed, _, _, _, _, logs) = CreateSut();
        feed.SetTicker(MakeTicker("BTC-EUR", bid: 100m, ask: 101m));

        var placed = await client.PlaceOrderAsync(new PlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Limit, 1m, 50m, "Profile1", null));
        await client.CancelOrderAsync("BTC-EUR", placed.ExternalId);

        Assert.Contains(logs.Entries, e => e.Message.Contains("cancelled"));
    }

    [Fact]
    public async Task SellingAfterMultipleBuys_RealizesPnLAgainstWeightedAveragePrice()
    {
        var (client, feed, _, trades, _, _, _) = CreateSut();
        feed.FeeSchedule = new FeeSchedule(0m, 0m);

        feed.SetTicker(MakeTicker("BTC-EUR", bid: 99m, ask: 100m));
        await client.PlaceOrderAsync(new PlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Market, 1m, null, "Profile1", null));

        feed.SetTicker(MakeTicker("BTC-EUR", bid: 199m, ask: 200m));
        await client.PlaceOrderAsync(new PlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Market, 1m, null, "Profile1", null));
        // Weighted average entry price is now 150.

        feed.SetTicker(MakeTicker("BTC-EUR", bid: 180m, ask: 181m));
        await client.PlaceOrderAsync(new PlaceOrderRequest("BTC-EUR", OrderSide.Sell, OrderType.Market, 2m, null, "Profile1", null));

        var sellTrade = trades.Trades.Single(t => t.Side == OrderSide.Sell);
        // (180 - 150) * 2 = 60 profit.
        Assert.Equal(60m, sellTrade.ProfitLoss);
    }
}
