using System.Text.Json;
using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Domain.Models;
using BitvavoBot.Exchange.Bitvavo.Dto;

namespace BitvavoBot.Exchange.Bitvavo;

/// <summary>
/// Live implementation of <see cref="IExchangeClient"/> against the real Bitvavo API. Ticker
/// updates arrive over the WebSocket feed; if that connection drops, a REST polling loop takes
/// over automatically (functional spec 3.1) until the socket reconnects.
/// </summary>
public sealed class BitvavoExchangeClient : IExchangeClient, IAsyncDisposable
{
    private readonly BitvavoRestClient _rest;
    private readonly BitvavoWebSocketClient _webSocket;
    private readonly int _pollingIntervalSeconds;
    private readonly HashSet<string> _subscribedMarkets = new();
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;

    public TradingMode Mode => TradingMode.Live;
    public ConnectionStatus ConnectionStatus { get; private set; } = ConnectionStatus.Disconnected;

    public event EventHandler<ConnectionStatus>? ConnectionStatusChanged;
    public event EventHandler<Ticker>? TickerUpdated;
    public event EventHandler<OrderBookSnapshot>? OrderBookUpdated;
    public event EventHandler<Domain.Interfaces.OrderResult>? OrderUpdated;

    public BitvavoExchangeClient(BitvavoRestClient restClient, BitvavoWebSocketClient webSocketClient, int pollingIntervalSeconds = 5)
    {
        _rest = restClient;
        _webSocket = webSocketClient;
        _pollingIntervalSeconds = pollingIntervalSeconds;
        _webSocket.ConnectionStatusChanged += OnWebSocketStatusChanged;
        _webSocket.MessageReceived += OnWebSocketMessage;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _webSocket.StartAsync(cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        StopPolling();
        await _webSocket.StopAsync();
    }

    public async Task<IReadOnlyList<MarketInfo>> GetMarketsAsync(CancellationToken cancellationToken = default)
    {
        var markets = await _rest.GetMarketsAsync(cancellationToken);
        return markets.Select(MapMarket).ToList();
    }

    public async Task<Ticker> GetTickerAsync(string market, CancellationToken cancellationToken = default)
    {
        var dto = await _rest.GetTicker24hAsync(market, cancellationToken);
        return MapTicker(dto);
    }

    public async Task<IReadOnlyList<Ticker>> GetAllTickersAsync(CancellationToken cancellationToken = default)
    {
        var dtos = await _rest.GetAllTicker24hAsync(cancellationToken);
        return dtos.Select(MapTicker).ToList();
    }

    public async Task<OrderBookSnapshot> GetOrderBookAsync(string market, int depth = 25, CancellationToken cancellationToken = default)
    {
        var dto = await _rest.GetOrderBookAsync(market, depth, cancellationToken);
        return MapOrderBook(dto);
    }

    public async Task<FeeSchedule> GetFeeScheduleAsync(string market, CancellationToken cancellationToken = default)
    {
        var dto = await _rest.GetAccountAsync(cancellationToken);
        return new FeeSchedule(
            decimal.Parse(dto.Fees.Maker, System.Globalization.CultureInfo.InvariantCulture) * 100m,
            decimal.Parse(dto.Fees.Taker, System.Globalization.CultureInfo.InvariantCulture) * 100m);
    }

    public async Task SubscribeTickerAsync(string market, CancellationToken cancellationToken = default)
    {
        _subscribedMarkets.Add(market);
        await _webSocket.SubscribeTickerAsync(market, cancellationToken);
    }

    public async Task UnsubscribeTickerAsync(string market, CancellationToken cancellationToken = default)
    {
        _subscribedMarkets.Remove(market);
        await _webSocket.UnsubscribeTickerAsync(market, cancellationToken);
    }

    public async Task<IReadOnlyList<AssetBalance>> GetBalanceAsync(CancellationToken cancellationToken = default)
    {
        var balances = await _rest.GetBalanceAsync(cancellationToken);
        return balances.Select(b => new AssetBalance(
            b.Symbol,
            decimal.Parse(b.Available, System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse(b.InOrder, System.Globalization.CultureInfo.InvariantCulture))).ToList();
    }

    public async Task<Domain.Interfaces.OrderResult> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default)
    {
        var body = new PlaceOrderBody
        {
            Market = request.Market,
            Side = request.Side == OrderSide.Buy ? "buy" : "sell",
            OrderType = request.Type == OrderType.Limit ? "limit" : "market",
            Amount = request.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Price = request.LimitPrice?.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        var dto = await _rest.PlaceOrderAsync(body, cancellationToken);
        return MapOrderResult(dto);
    }

    public async Task<Domain.Interfaces.OrderResult> CancelOrderAsync(string market, string externalOrderId, CancellationToken cancellationToken = default)
    {
        var dto = await _rest.CancelOrderAsync(market, externalOrderId, cancellationToken);
        return MapOrderResult(dto);
    }

    public async Task<IReadOnlyList<Domain.Interfaces.OrderResult>> CancelAllOrdersAsync(string? market = null, CancellationToken cancellationToken = default)
    {
        var dtos = await _rest.CancelAllOrdersAsync(market, cancellationToken);
        return dtos.Select(MapOrderResult).ToList();
    }

    public async Task<IReadOnlyList<Domain.Interfaces.OrderResult>> GetOpenOrdersAsync(string? market = null, CancellationToken cancellationToken = default)
    {
        var dtos = await _rest.GetOpenOrdersAsync(market, cancellationToken);
        return dtos.Select(MapOrderResult).ToList();
    }

    private void OnWebSocketStatusChanged(object? sender, ConnectionStatus status)
    {
        ConnectionStatus = status;
        ConnectionStatusChanged?.Invoke(this, status);

        if (status is ConnectionStatus.Reconnecting or ConnectionStatus.FallbackPolling)
        {
            StartPolling();
        }
        else if (status == ConnectionStatus.Connected)
        {
            StopPolling();
        }
    }

    private void OnWebSocketMessage(object? sender, JsonElement message)
    {
        if (!message.TryGetProperty("event", out var eventProp)) return;
        var eventName = eventProp.GetString();

        switch (eventName)
        {
            case "ticker24h" when message.TryGetProperty("data", out var tickerData):
                foreach (var item in tickerData.EnumerateArray())
                {
                    var dto = JsonSerializer.Deserialize<Ticker24hDto>(item.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    if (dto is not null) TickerUpdated?.Invoke(this, MapTicker(dto));
                }
                break;
            case "book" when message.TryGetProperty("market", out var marketProp):
                // Incremental book diffs are out of scope for v1; full snapshots are fetched via REST when needed.
                break;
        }
    }

    private void StartPolling()
    {
        if (_pollingTask is not null) return;
        _pollingCts = new CancellationTokenSource();
        _pollingTask = Task.Run(() => PollingLoopAsync(_pollingCts.Token));
    }

    private void StopPolling()
    {
        _pollingCts?.Cancel();
        _pollingTask = null;
    }

    private async Task PollingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                foreach (var market in _subscribedMarkets.ToArray())
                {
                    var ticker = await GetTickerAsync(market, cancellationToken);
                    TickerUpdated?.Invoke(this, ticker);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Swallow: a single failed poll iteration must not crash the fallback loop or surface to the UI.
            }

            try { await Task.Delay(TimeSpan.FromSeconds(_pollingIntervalSeconds), cancellationToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private static MarketInfo MapMarket(MarketDto dto) => new(
        dto.Market,
        dto.Base,
        dto.Quote,
        dto.PricePrecision,
        8,
        dto.MinOrderInBaseAsset is not null ? decimal.Parse(dto.MinOrderInBaseAsset, System.Globalization.CultureInfo.InvariantCulture) : 0m,
        dto.MinOrderInQuoteAsset is not null ? decimal.Parse(dto.MinOrderInQuoteAsset, System.Globalization.CultureInfo.InvariantCulture) : 0m,
        string.Equals(dto.Status, "trading", StringComparison.OrdinalIgnoreCase));

    private static Ticker MapTicker(Ticker24hDto dto)
    {
        var last = ParseOrZero(dto.Last);
        var open = ParseOrZero(dto.Open);
        var changePct = open == 0m ? 0m : (last - open) / open * 100m;

        return new Ticker(
            dto.Market,
            ParseOrZero(dto.Bid),
            ParseOrZero(dto.Ask),
            last,
            ParseOrZero(dto.Volume),
            changePct,
            ParseOrZero(dto.High),
            ParseOrZero(dto.Low),
            dto.Timestamp > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(dto.Timestamp) : DateTimeOffset.UtcNow);
    }

    private static OrderBookSnapshot MapOrderBook(OrderBookDto dto) => new(
        dto.Market,
        dto.Bids.Select(b => new OrderBookLevel(ParseOrZero(b.ElementAtOrDefault(0)), ParseOrZero(b.ElementAtOrDefault(1)))).ToList(),
        dto.Asks.Select(a => new OrderBookLevel(ParseOrZero(a.ElementAtOrDefault(0)), ParseOrZero(a.ElementAtOrDefault(1)))).ToList(),
        DateTimeOffset.UtcNow);

    private static Domain.Interfaces.OrderResult MapOrderResult(OrderDto dto) => new(
        dto.OrderId,
        MapStatus(dto.Status),
        ParseOrZero(dto.FilledAmount),
        dto.Price is not null ? ParseOrZero(dto.Price) : null,
        ParseOrZero(dto.FeePaid));

    private static OrderStatus MapStatus(string status) => status switch
    {
        "new" => OrderStatus.New,
        "open" => OrderStatus.Open,
        "partiallyFilled" => OrderStatus.PartiallyFilled,
        "filled" => OrderStatus.Filled,
        "canceled" or "cancelled" => OrderStatus.Cancelled,
        "rejected" => OrderStatus.Rejected,
        "expired" => OrderStatus.Expired,
        _ => OrderStatus.New
    };

    private static decimal ParseOrZero(string? value) =>
        value is not null && decimal.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : 0m;

    public async ValueTask DisposeAsync()
    {
        StopPolling();
        _webSocket.ConnectionStatusChanged -= OnWebSocketStatusChanged;
        _webSocket.MessageReceived -= OnWebSocketMessage;
        await _webSocket.DisposeAsync();
    }
}
