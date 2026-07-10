using System.Collections.Concurrent;
using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Domain.Models;

namespace BitvavoBot.Exchange.Paper;

/// <summary>
/// Simulated exchange client for one Papertrading profile (virtual account). Implements exactly
/// the same <see cref="IExchangeClient"/> contract as <c>BitvavoExchangeClient</c> so the Trading
/// Engine and Risk Engine cannot tell the difference (functional spec 8.6/9) — only order
/// execution is simulated locally, reading prices from the shared, always-live
/// <see cref="IMarketDataFeed"/>. No call in this class ever reaches the real Bitvavo API.
/// </summary>
public sealed class PaperExchangeClient : IExchangeClient, IAsyncDisposable
{
    private readonly IMarketDataFeed _marketData;
    private readonly IOrderRepository _orderRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly IPapertradingProfileRepository _profileRepository;
    private readonly string _papertradingProfileName;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, Order>> _pendingByMarket = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public TradingMode Mode => TradingMode.Paper;
    public ConnectionStatus ConnectionStatus => _marketData.ConnectionStatus;

    public event EventHandler<ConnectionStatus>? ConnectionStatusChanged
    {
        add => _marketData.ConnectionStatusChanged += value;
        remove => _marketData.ConnectionStatusChanged -= value;
    }
    public event EventHandler<Ticker>? TickerUpdated
    {
        add => _marketData.TickerUpdated += value;
        remove => _marketData.TickerUpdated -= value;
    }
    public event EventHandler<OrderBookSnapshot>? OrderBookUpdated
    {
        add => _marketData.OrderBookUpdated += value;
        remove => _marketData.OrderBookUpdated -= value;
    }
    public event EventHandler<OrderResult>? OrderUpdated;

    public PaperExchangeClient(
        IMarketDataFeed marketData,
        IOrderRepository orderRepository,
        ITradeRepository tradeRepository,
        IPositionRepository positionRepository,
        IPapertradingProfileRepository profileRepository,
        string papertradingProfileName)
    {
        _marketData = marketData;
        _orderRepository = orderRepository;
        _tradeRepository = tradeRepository;
        _positionRepository = positionRepository;
        _profileRepository = profileRepository;
        _papertradingProfileName = papertradingProfileName;
        _marketData.TickerUpdated += OnTickerUpdated;
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default) => _marketData.ConnectAsync(cancellationToken);
    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<MarketInfo>> GetMarketsAsync(CancellationToken cancellationToken = default) =>
        _marketData.GetMarketsAsync(cancellationToken);

    public Task<Ticker> GetTickerAsync(string market, CancellationToken cancellationToken = default) =>
        _marketData.GetTickerAsync(market, cancellationToken);

    public Task<IReadOnlyList<Ticker>> GetAllTickersAsync(CancellationToken cancellationToken = default) =>
        _marketData.GetAllTickersAsync(cancellationToken);

    public Task<OrderBookSnapshot> GetOrderBookAsync(string market, int depth = 25, CancellationToken cancellationToken = default) =>
        _marketData.GetOrderBookAsync(market, depth, cancellationToken);

    public Task<FeeSchedule> GetFeeScheduleAsync(string market, CancellationToken cancellationToken = default) =>
        _marketData.GetFeeScheduleAsync(market, cancellationToken);

    public Task SubscribeTickerAsync(string market, CancellationToken cancellationToken = default) =>
        _marketData.SubscribeTickerAsync(market, cancellationToken);

    public Task UnsubscribeTickerAsync(string market, CancellationToken cancellationToken = default) =>
        _marketData.UnsubscribeTickerAsync(market, cancellationToken);

    public async Task<IReadOnlyList<AssetBalance>> GetBalanceAsync(CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        var positions = await _positionRepository.GetOpenPositionsAsync(TradingMode.Paper, _papertradingProfileName, cancellationToken);

        var reservedForBuys = _pendingByMarket.Values
            .SelectMany(d => d.Values)
            .Where(o => o.Side == OrderSide.Buy)
            .Sum(o => (o.Amount - o.FilledAmount) * (o.Price ?? 0m));

        var balances = new List<AssetBalance>
        {
            new(profile.QuoteCurrency, profile.CurrentBalance - reservedForBuys, reservedForBuys)
        };

        foreach (var position in positions)
        {
            var reservedForSells = _pendingByMarket.Values
                .SelectMany(d => d.Values)
                .Where(o => o.Side == OrderSide.Sell && o.Market.StartsWith(position.Asset + "-", StringComparison.OrdinalIgnoreCase))
                .Sum(o => o.Amount - o.FilledAmount);

            balances.Add(new AssetBalance(position.Asset, position.Amount - reservedForSells, reservedForSells));
        }

        return balances;
    }

    public async Task<OrderResult> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var ticker = await _marketData.GetTickerAsync(request.Market, cancellationToken);
            var now = DateTimeOffset.UtcNow;

            var order = new Order
            {
                ExternalId = $"PAPER-{Guid.NewGuid():N}",
                Mode = TradingMode.Paper,
                Market = request.Market,
                Side = request.Side,
                Type = request.Type,
                Status = OrderStatus.Open,
                Price = request.LimitPrice,
                Amount = request.Amount,
                FilledAmount = 0m,
                FeeCurrency = "EUR",
                BotProfileName = request.BotProfileName,
                StrategyName = request.StrategyName,
                PapertradingProfileName = _papertradingProfileName,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _orderRepository.AddAsync(order, cancellationToken);

            if (request.Type == OrderType.Market)
            {
                var fillPrice = request.Side == OrderSide.Buy ? ticker.Ask : ticker.Bid;
                await FillOrderAsync(order, fillPrice, isMaker: false, cancellationToken);
            }
            else
            {
                await _marketData.SubscribeTickerAsync(request.Market, cancellationToken);
                _pendingByMarket.GetOrAdd(request.Market, _ => new ConcurrentDictionary<long, Order>())[order.Id] = order;
                if (IsCrossed(order, ticker))
                {
                    await FillOrderAsync(order, order.Price!.Value, isMaker: true, cancellationToken);
                }
            }

            return new OrderResult(order.ExternalId, order.Status, order.FilledAmount, order.AverageFillPrice, order.FeePaid);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<OrderResult> CancelOrderAsync(string market, string externalOrderId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var order = await _orderRepository.GetByExternalIdAsync(TradingMode.Paper, externalOrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Papertrading order {externalOrderId} not found.");

            await CancelPendingOrderAsync(order, cancellationToken);
            return new OrderResult(order.ExternalId, order.Status, order.FilledAmount, order.AverageFillPrice, order.FeePaid);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<OrderResult>> CancelAllOrdersAsync(string? market = null, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var results = new List<OrderResult>();
            var markets = market is null ? _pendingByMarket.Keys.ToArray() : new[] { market };

            foreach (var m in markets)
            {
                if (!_pendingByMarket.TryGetValue(m, out var orders)) continue;
                foreach (var order in orders.Values.ToArray())
                {
                    await CancelPendingOrderAsync(order, cancellationToken);
                    results.Add(new OrderResult(order.ExternalId, order.Status, order.FilledAmount, order.AverageFillPrice, order.FeePaid));
                }
            }

            return results;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<OrderResult>> GetOpenOrdersAsync(string? market = null, CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepository.GetOpenOrdersAsync(TradingMode.Paper, papertradingProfileName: _papertradingProfileName, cancellationToken: cancellationToken);
        var filtered = market is null ? orders : orders.Where(o => o.Market == market);
        return filtered.Select(o => new OrderResult(o.ExternalId, o.Status, o.FilledAmount, o.AverageFillPrice, o.FeePaid)).ToList();
    }

    private async Task CancelPendingOrderAsync(Order order, CancellationToken cancellationToken)
    {
        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTimeOffset.UtcNow;
        order.UpdatedAt = order.CancelledAt.Value;
        await _orderRepository.UpdateAsync(order, cancellationToken);

        if (_pendingByMarket.TryGetValue(order.Market, out var orders))
        {
            orders.TryRemove(order.Id, out _);
        }
    }

    private void OnTickerUpdated(object? sender, Ticker ticker) => _ = HandleTickerUpdateAsync(ticker);

    private async Task HandleTickerUpdateAsync(Ticker ticker)
    {
        if (!_pendingByMarket.TryGetValue(ticker.Market, out var orders) || orders.IsEmpty) return;

        await _lock.WaitAsync();
        try
        {
            foreach (var order in orders.Values.ToArray())
            {
                if (order.Status == OrderStatus.Open && IsCrossed(order, ticker))
                {
                    await FillOrderAsync(order, order.Price!.Value, isMaker: true, CancellationToken.None);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// A limit order fills once the market price reaches or passes the limit level (functional
    /// spec 8.2): a buy fills once the ask drops to/through the limit, a sell fills once the bid
    /// rises to/through the limit. v1 assumes the full amount fills at the limit price with no
    /// partial fills or order-book-depth checks, as explicitly allowed by the spec.
    /// </summary>
    private static bool IsCrossed(Order order, Ticker ticker)
    {
        if (order.Price is not { } limitPrice) return false;
        return order.Side == OrderSide.Buy ? ticker.Ask <= limitPrice : ticker.Bid >= limitPrice;
    }

    private async Task FillOrderAsync(Order order, decimal fillPrice, bool isMaker, CancellationToken cancellationToken)
    {
        var feeSchedule = await _marketData.GetFeeScheduleAsync(order.Market, cancellationToken);
        var feePercentage = isMaker ? feeSchedule.MakerFeePercentage : feeSchedule.TakerFeePercentage;
        var quoteAmount = order.Amount * fillPrice;
        var fee = quoteAmount * (feePercentage / 100m);

        var profile = await GetProfileAsync(cancellationToken);
        var asset = order.Market.Split('-')[0];
        var now = DateTimeOffset.UtcNow;

        decimal? realizedPnl = null;

        if (order.Side == OrderSide.Buy)
        {
            profile.CurrentBalance -= quoteAmount + fee;

            var position = await _positionRepository.GetOpenPositionAsync(TradingMode.Paper, asset, _papertradingProfileName, cancellationToken)
                ?? new Position { Mode = TradingMode.Paper, Market = order.Market, Asset = asset, PapertradingProfileName = _papertradingProfileName, OpenedAt = now };

            position.ApplyBuy(order.Amount, fillPrice);
            await _positionRepository.UpsertAsync(position, cancellationToken);
        }
        else
        {
            profile.CurrentBalance += quoteAmount - fee;

            var position = await _positionRepository.GetOpenPositionAsync(TradingMode.Paper, asset, _papertradingProfileName, cancellationToken);
            if (position is not null)
            {
                realizedPnl = (fillPrice - position.AverageEntryPrice) * order.Amount - fee;
                position.ApplySell(order.Amount);
                await _positionRepository.UpsertAsync(position, cancellationToken);
            }
        }

        await _profileRepository.UpdateAsync(profile, cancellationToken);

        order.Status = OrderStatus.Filled;
        order.FilledAmount = order.Amount;
        order.AverageFillPrice = fillPrice;
        order.FeePaid = fee;
        order.FilledAt = now;
        order.UpdatedAt = now;
        await _orderRepository.UpdateAsync(order, cancellationToken);

        await _tradeRepository.AddAsync(new Trade
        {
            OrderId = order.Id,
            Mode = TradingMode.Paper,
            Market = order.Market,
            Side = order.Side,
            Price = fillPrice,
            Amount = order.Amount,
            Fee = fee,
            FeeCurrency = "EUR",
            ProfitLoss = realizedPnl,
            Timestamp = now
        }, cancellationToken);

        if (_pendingByMarket.TryGetValue(order.Market, out var orders))
        {
            orders.TryRemove(order.Id, out _);
        }

        OrderUpdated?.Invoke(this, new OrderResult(order.ExternalId, order.Status, order.FilledAmount, order.AverageFillPrice, order.FeePaid));
    }

    private async Task<PapertradingProfile> GetProfileAsync(CancellationToken cancellationToken) =>
        await _profileRepository.GetByNameAsync(_papertradingProfileName, cancellationToken)
        ?? throw new InvalidOperationException($"Papertrading profile '{_papertradingProfileName}' not found.");

    public ValueTask DisposeAsync()
    {
        _marketData.TickerUpdated -= OnTickerUpdated;
        _lock.Dispose();
        return ValueTask.CompletedTask;
    }
}
