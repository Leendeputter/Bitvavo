using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Models;

namespace BitvavoBot.Domain.Interfaces;

public sealed record OrderResult(
    string ExternalId,
    OrderStatus Status,
    decimal FilledAmount,
    decimal? AverageFillPrice,
    decimal FeePaid);

/// <summary>
/// Read-only live market data: market list, tickers, order book depth and fee schedule. Both the
/// real Bitvavo client and <c>PaperExchangeClient</c> read from one shared instance of this so
/// Papertrading always reacts to the same live data as Live trading (functional spec 8.2) — the
/// Marktmonitor screen is mode-independent and always uses this feed directly.
/// </summary>
public interface IMarketDataFeed
{
    ConnectionStatus ConnectionStatus { get; }

    event EventHandler<ConnectionStatus>? ConnectionStatusChanged;
    event EventHandler<Ticker>? TickerUpdated;
    event EventHandler<OrderBookSnapshot>? OrderBookUpdated;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MarketInfo>> GetMarketsAsync(CancellationToken cancellationToken = default);
    Task<Ticker> GetTickerAsync(string market, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Ticker>> GetAllTickersAsync(CancellationToken cancellationToken = default);
    Task<OrderBookSnapshot> GetOrderBookAsync(string market, int depth = 25, CancellationToken cancellationToken = default);
    Task<FeeSchedule> GetFeeScheduleAsync(string market, CancellationToken cancellationToken = default);

    Task SubscribeTickerAsync(string market, CancellationToken cancellationToken = default);
    Task UnsubscribeTickerAsync(string market, CancellationToken cancellationToken = default);
}

/// <summary>
/// Exchange-agnostic spot trading client. Bitvavo is the first full implementation
/// (<c>BitvavoExchangeClient</c>); <c>PaperExchangeClient</c> implements the same contract but
/// simulates every call locally instead of talking to a real exchange, reading prices from a
/// shared <see cref="IMarketDataFeed"/> instead of maintaining its own connection. The active
/// implementation is chosen by dependency injection based on the globally selected
/// <see cref="TradingMode"/>.
/// </summary>
public interface IExchangeClient : IMarketDataFeed
{
    TradingMode Mode { get; }

    event EventHandler<OrderResult>? OrderUpdated;

    Task<IReadOnlyList<AssetBalance>> GetBalanceAsync(CancellationToken cancellationToken = default);

    Task<OrderResult> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default);
    Task<OrderResult> CancelOrderAsync(string market, string externalOrderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderResult>> CancelAllOrdersAsync(string? market = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderResult>> GetOpenOrdersAsync(string? market = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks locally-open orders against the exchange for fills that happened asynchronously
    /// since they were placed, recording any new trades/position changes found. Live has no
    /// authenticated push channel wired up for order updates, so BotRunner calls this once per
    /// tick to discover fills; Papertrading fills are already fully event-driven, so its
    /// implementation is a no-op.
    /// </summary>
    Task ReconcileOpenOrdersAsync(CancellationToken cancellationToken = default);
}
