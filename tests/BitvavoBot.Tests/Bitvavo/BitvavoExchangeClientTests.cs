using System.Net;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Models;
using BitvavoBot.Exchange.Bitvavo;
using BitvavoBot.Tests.Fakes;
using Xunit;

namespace BitvavoBot.Tests.Bitvavo;

public sealed class BitvavoExchangeClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string> _respond;
        public StubHandler(Func<HttpRequestMessage, string> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_respond(request)) });
    }

    private static BitvavoWebSocketClient MakeIdleWebSocketClient() =>
        new(new BitvavoOptions { WebSocketUrl = "wss://ws.bitvavo.com/v2/" });

    /// <summary>
    /// Regression test: BitvavoExchangeClient.PlaceOrderAsync previously never touched the local
    /// database at all, so Live orders/trades/positions were never recorded — Open Orders and
    /// Posities (Live tab) would always be empty regardless of what actually happened on Bitvavo.
    /// A market order (always filled synchronously in Bitvavo's response) must now be persisted
    /// as an Order, a Trade, and update the local Position immediately.
    /// </summary>
    [Fact]
    public async Task PlaceOrderAsync_MarketOrderFilledImmediately_PersistsOrderTradeAndPosition()
    {
        const string placeOrderResponse = """
            {"orderId":"live-order-1","market":"BTC-EUR","status":"filled","side":"buy","orderType":"market",
             "amount":"1","filledAmount":"1","price":"100","feePaid":"0.25","feeCurrency":"EUR"}
            """;
        var handler = new StubHandler(_ => placeOrderResponse);
        var rest = new BitvavoRestClient(new HttpClient(handler), new BitvavoOptions { RestBaseUrl = "https://api.bitvavo.com/v2" });

        var orders = new InMemoryOrderRepository();
        var trades = new InMemoryTradeRepository { Orders = orders.Orders };
        var positions = new InMemoryPositionRepository();

        await using var webSocket = MakeIdleWebSocketClient();
        await using var client = new BitvavoExchangeClient(rest, webSocket, orders, trades, positions);

        var request = new PlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Market, 1m, null, "LiveProfile", null);
        var result = await client.PlaceOrderAsync(request);

        Assert.Equal(OrderStatus.Filled, result.Status);

        var savedOrder = Assert.Single(orders.Orders);
        Assert.Equal(TradingMode.Live, savedOrder.Mode);
        Assert.Equal(OrderStatus.Filled, savedOrder.Status);

        var savedTrade = Assert.Single(trades.Trades);
        Assert.Equal(TradingMode.Live, savedTrade.Mode);
        Assert.Equal(100m, savedTrade.Price);

        var position = await positions.GetOpenPositionAsync(TradingMode.Live, "BTC");
        Assert.NotNull(position);
        Assert.Equal(1m, position!.Amount);
        Assert.Equal(100m, position.AverageEntryPrice);
    }

    /// <summary>
    /// Regression test: a resting limit order that fills later has no push notification wired up
    /// in this app, so ReconcileOpenOrdersAsync (called once per BotRunner tick) must be the thing
    /// that discovers it and records the trade/position update.
    /// </summary>
    [Fact]
    public async Task ReconcileOpenOrdersAsync_DiscoversLimitOrderFilledSinceItWasPlaced()
    {
        const string placeOrderResponse = """
            {"orderId":"live-order-2","market":"BTC-EUR","status":"open","side":"buy","orderType":"limit",
             "amount":"1","filledAmount":"0","price":"90"}
            """;
        const string getOrderFilledResponse = """
            {"orderId":"live-order-2","market":"BTC-EUR","status":"filled","side":"buy","orderType":"limit",
             "amount":"1","filledAmount":"1","price":"90","feePaid":"0.15","feeCurrency":"EUR"}
            """;

        var callCount = 0;
        var handler = new StubHandler(request =>
        {
            callCount++;
            return request.Method == HttpMethod.Post ? placeOrderResponse : getOrderFilledResponse;
        });
        var rest = new BitvavoRestClient(new HttpClient(handler), new BitvavoOptions { RestBaseUrl = "https://api.bitvavo.com/v2" });

        var orders = new InMemoryOrderRepository();
        var trades = new InMemoryTradeRepository { Orders = orders.Orders };
        var positions = new InMemoryPositionRepository();

        await using var webSocket = MakeIdleWebSocketClient();
        await using var client = new BitvavoExchangeClient(rest, webSocket, orders, trades, positions);

        var request = new PlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Limit, 1m, 90m, "LiveProfile", null);
        var placed = await client.PlaceOrderAsync(request);
        Assert.Equal(OrderStatus.Open, placed.Status);
        Assert.Empty(trades.Trades);

        await client.ReconcileOpenOrdersAsync();

        var savedOrder = Assert.Single(orders.Orders);
        Assert.Equal(OrderStatus.Filled, savedOrder.Status);
        Assert.Single(trades.Trades);

        var position = await positions.GetOpenPositionAsync(TradingMode.Live, "BTC");
        Assert.NotNull(position);
        Assert.Equal(1m, position!.Amount);
        Assert.True(callCount >= 2);
    }
}
