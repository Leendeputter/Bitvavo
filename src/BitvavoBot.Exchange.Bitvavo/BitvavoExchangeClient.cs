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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly BitvavoRestClient _rest;
    private readonly BitvavoWebSocketClient _webSocket;
    private readonly IOrderRepository _orderRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IPositionRepository _positionRepository;
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

    public BitvavoExchangeClient(
        BitvavoRestClient restClient, BitvavoWebSocketClient webSocketClient,
        IOrderRepository orderRepository, ITradeRepository tradeRepository, IPositionRepository positionRepository,
        int pollingIntervalSeconds = 5)
    {
        _rest = restClient;
        _webSocket = webSocketClient;
        _orderRepository = orderRepository;
        _tradeRepository = tradeRepository;
        _positionRepository = positionRepository;
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

        var now = DateTimeOffset.UtcNow;
        var order = new Order
        {
            ExternalId = dto.OrderId,
            Mode = TradingMode.Live,
            Market = request.Market,
            Side = request.Side,
            Type = request.Type,
            Status = MapStatus(dto.Status),
            Price = request.LimitPrice,
            Amount = request.Amount,
            FilledAmount = ParseOrZero(dto.FilledAmount),
            FeeCurrency = "EUR",
            BotProfileName = request.BotProfileName,
            StrategyName = request.StrategyName,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _orderRepository.AddAsync(order, cancellationToken);

        if (order.Status == OrderStatus.Filled)
        {
            await RecordFillAsync(order, ParseOrZero(dto.Price), ParseOrZero(dto.FeePaid), cancellationToken);
        }

        return MapOrderResult(dto);
    }

    public async Task<Domain.Interfaces.OrderResult> CancelOrderAsync(string market, string externalOrderId, CancellationToken cancellationToken = default)
    {
        var dto = await _rest.CancelOrderAsync(market, externalOrderId, cancellationToken);
        await MarkCancelledAsync(externalOrderId, cancellationToken);
        return MapOrderResult(dto);
    }

    public async Task<IReadOnlyList<Domain.Interfaces.OrderResult>> CancelAllOrdersAsync(string? market = null, CancellationToken cancellationToken = default)
    {
        var dtos = await _rest.CancelAllOrdersAsync(market, cancellationToken);
        foreach (var dto in dtos)
        {
            await MarkCancelledAsync(dto.OrderId, cancellationToken);
        }
        return dtos.Select(MapOrderResult).ToList();
    }

    public async Task<IReadOnlyList<Domain.Interfaces.OrderResult>> GetOpenOrdersAsync(string? market = null, CancellationToken cancellationToken = default)
    {
        var dtos = await _rest.GetOpenOrdersAsync(market, cancellationToken);
        return dtos.Select(MapOrderResult).ToList();
    }

    /// <summary>
    /// Bitvavo has no authenticated push channel wired up in this app for order/fill updates, so a
    /// resting limit order's eventual fill is only ever discovered by asking Bitvavo directly.
    /// Call this periodically (BotRunner does, once per scrape tick) for every locally open Live
    /// order to detect and record fills that happened since the order was placed — without this,
    /// Live positions/trades only ever reflect orders that filled instantly (market orders, or a
    /// limit order that happened to already be crossed at placement time).
    /// </summary>
    public async Task ReconcileOpenOrdersAsync(CancellationToken cancellationToken = default)
    {
        var openOrders = await _orderRepository.GetOpenOrdersAsync(TradingMode.Live, cancellationToken: cancellationToken);
        foreach (var order in openOrders)
        {
            OrderDto dto;
            try
            {
                dto = await _rest.GetOrderAsync(order.Market, order.ExternalId, cancellationToken);
            }
            catch (BitvavoApiException)
            {
                continue;
            }

            var status = MapStatus(dto.Status);
            if (status == order.Status) continue;

            order.Status = status;
            order.FilledAmount = ParseOrZero(dto.FilledAmount);
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await _orderRepository.UpdateAsync(order, cancellationToken);

            if (status == OrderStatus.Filled)
            {
                await RecordFillAsync(order, ParseOrZero(dto.Price), ParseOrZero(dto.FeePaid), cancellationToken);
            }
            else if (status == OrderStatus.Cancelled)
            {
                order.CancelledAt = order.UpdatedAt;
                await _orderRepository.UpdateAsync(order, cancellationToken);
            }
        }
    }

    private async Task MarkCancelledAsync(string externalOrderId, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByExternalIdAsync(TradingMode.Live, externalOrderId, cancellationToken);
        if (order is null) return;

        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTimeOffset.UtcNow;
        order.UpdatedAt = order.CancelledAt.Value;
        await _orderRepository.UpdateAsync(order, cancellationToken);
    }

    private async Task RecordFillAsync(Order order, decimal fillPrice, decimal fee, CancellationToken cancellationToken)
    {
        order.AverageFillPrice = fillPrice;
        order.FeePaid = fee;
        order.FilledAt = DateTimeOffset.UtcNow;
        order.UpdatedAt = order.FilledAt.Value;
        await _orderRepository.UpdateAsync(order, cancellationToken);

        var asset = order.Market.Split('-')[0];
        decimal? realizedPnl = null;

        if (order.Side == OrderSide.Buy)
        {
            var position = await _positionRepository.GetOpenPositionAsync(TradingMode.Live, asset, cancellationToken: cancellationToken)
                ?? new Position { Mode = TradingMode.Live, Market = order.Market, Asset = asset, OpenedAt = order.FilledAt.Value };

            position.ApplyBuy(order.Amount, fillPrice);
            await _positionRepository.UpsertAsync(position, cancellationToken);
        }
        else
        {
            var position = await _positionRepository.GetOpenPositionAsync(TradingMode.Live, asset, cancellationToken: cancellationToken);
            if (position is not null)
            {
                realizedPnl = (fillPrice - position.AverageEntryPrice) * order.Amount - fee;
                position.ApplySell(order.Amount);
                await _positionRepository.UpsertAsync(position, cancellationToken);
            }
        }

        await _tradeRepository.AddAsync(new Trade
        {
            OrderId = order.Id,
            Mode = TradingMode.Live,
            Market = order.Market,
            Side = order.Side,
            Price = fillPrice,
            Amount = order.Amount,
            Fee = fee,
            FeeCurrency = "EUR",
            ProfitLoss = realizedPnl,
            Timestamp = order.FilledAt.Value
        }, cancellationToken);
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
        // A single malformed/unexpected ticker payload must never escape this handler: an
        // exception here propagates out of the WebSocket receive loop, which tears down and
        // reconnects the *entire* connection. If the same bad message reappears after every
        // reconnect, TickerUpdated stops firing for every market indefinitely — which silently
        // starves Papertrading limit orders of the price updates they need to ever fill.
        try
        {
            if (!message.TryGetProperty("event", out var eventProp)) return;
            var eventName = eventProp.GetString();

            switch (eventName)
            {
                case "ticker24h" when message.TryGetProperty("data", out var tickerData):
                    foreach (var item in tickerData.EnumerateArray())
                    {
                        try
                        {
                            var dto = JsonSerializer.Deserialize<Ticker24hDto>(item.GetRawText(), JsonOptions);
                            if (dto is not null) TickerUpdated?.Invoke(this, MapTicker(dto));
                        }
                        catch (JsonException)
                        {
                            // Skip this one ticker item; the rest of the batch (and the connection) is still good.
                        }
                    }
                    break;
                case "book" when message.TryGetProperty("market", out var marketProp):
                    // Incremental book diffs are out of scope for v1; full snapshots are fetched via REST when needed.
                    break;
            }
        }
        catch (Exception)
        {
            // Never let a message-parsing failure kill the socket — worst case we drop one update.
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
