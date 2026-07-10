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
/// Exchange-agnostic spot trading client. Bitvavo is the first full implementation
/// (<c>BitvavoExchangeClient</c>); <c>PaperExchangeClient</c> implements the same contract but
/// simulates every call locally instead of talking to a real exchange. The active implementation
/// is chosen by dependency injection based on the globally selected <see cref="TradingMode"/>.
/// </summary>
public interface IExchangeClient
{
    TradingMode Mode { get; }
    ConnectionStatus ConnectionStatus { get; }

    event EventHandler<ConnectionStatus>? ConnectionStatusChanged;
    event EventHandler<Ticker>? TickerUpdated;
    event EventHandler<OrderBookSnapshot>? OrderBookUpdated;
    event EventHandler<OrderResult>? OrderUpdated;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MarketInfo>> GetMarketsAsync(CancellationToken cancellationToken = default);
    Task<Ticker> GetTickerAsync(string market, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Ticker>> GetAllTickersAsync(CancellationToken cancellationToken = default);
    Task<OrderBookSnapshot> GetOrderBookAsync(string market, int depth = 25, CancellationToken cancellationToken = default);
    Task<FeeSchedule> GetFeeScheduleAsync(string market, CancellationToken cancellationToken = default);

    Task SubscribeTickerAsync(string market, CancellationToken cancellationToken = default);
    Task UnsubscribeTickerAsync(string market, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssetBalance>> GetBalanceAsync(CancellationToken cancellationToken = default);

    Task<OrderResult> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default);
    Task<OrderResult> CancelOrderAsync(string market, string externalOrderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderResult>> CancelAllOrdersAsync(string? market = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderResult>> GetOpenOrdersAsync(string? market = null, CancellationToken cancellationToken = default);
}
