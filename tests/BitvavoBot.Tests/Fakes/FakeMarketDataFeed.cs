using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Domain.Models;

namespace BitvavoBot.Tests.Fakes;

/// <summary>Test double for <see cref="IMarketDataFeed"/>: lets tests push ticker updates and control the fee schedule deterministically.</summary>
public sealed class FakeMarketDataFeed : IMarketDataFeed
{
    private readonly Dictionary<string, Ticker> _tickers = new();

    public FeeSchedule FeeSchedule { get; set; } = new(0.15m, 0.25m);
    public ConnectionStatus ConnectionStatus { get; set; } = ConnectionStatus.Connected;

    public event EventHandler<ConnectionStatus>? ConnectionStatusChanged;
    public event EventHandler<Ticker>? TickerUpdated;
    public event EventHandler<OrderBookSnapshot>? OrderBookUpdated;

    public void SetTicker(Ticker ticker)
    {
        _tickers[ticker.Market] = ticker;
        TickerUpdated?.Invoke(this, ticker);
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<MarketInfo>> GetMarketsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MarketInfo>>(Array.Empty<MarketInfo>());

    public Task<Ticker> GetTickerAsync(string market, CancellationToken cancellationToken = default) =>
        Task.FromResult(_tickers[market]);

    public Task<IReadOnlyList<Ticker>> GetAllTickersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Ticker>>(_tickers.Values.ToList());

    public Task<OrderBookSnapshot> GetOrderBookAsync(string market, int depth = 25, CancellationToken cancellationToken = default) =>
        Task.FromResult(new OrderBookSnapshot(market, Array.Empty<OrderBookLevel>(), Array.Empty<OrderBookLevel>(), DateTimeOffset.UtcNow));

    public Task<FeeSchedule> GetFeeScheduleAsync(string market, CancellationToken cancellationToken = default) => Task.FromResult(FeeSchedule);

    public Task SubscribeTickerAsync(string market, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UnsubscribeTickerAsync(string market, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
