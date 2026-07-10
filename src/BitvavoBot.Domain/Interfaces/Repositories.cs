using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;

namespace BitvavoBot.Domain.Interfaces;

public interface IOrderRepository
{
    Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default);
    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
    Task<Order?> GetByExternalIdAsync(TradingMode mode, string externalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetOpenOrdersAsync(TradingMode mode, string? botProfileName = null, string? papertradingProfileName = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetAllAsync(TradingMode mode, string? botProfileName = null, string? papertradingProfileName = null, CancellationToken cancellationToken = default);
    Task<int> CountActiveForProfileAsync(TradingMode mode, string botProfileName, CancellationToken cancellationToken = default);
    Task<int> CountOpenForProfileAsync(TradingMode mode, string botProfileName, CancellationToken cancellationToken = default);
}

public interface ITradeRepository
{
    Task<Trade> AddAsync(Trade trade, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Trade>> GetAllAsync(TradingMode mode, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default);
    Task<int> CountTradesTodayAsync(TradingMode mode, string botProfileName, CancellationToken cancellationToken = default);
    Task<decimal> GetRealizedPnLTodayAsync(TradingMode mode, string botProfileName, CancellationToken cancellationToken = default);
}

public interface IPositionRepository
{
    Task<Position?> GetOpenPositionAsync(TradingMode mode, string asset, string? papertradingProfileName = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Position>> GetOpenPositionsAsync(TradingMode mode, string? papertradingProfileName = null, CancellationToken cancellationToken = default);
    Task UpsertAsync(Position position, CancellationToken cancellationToken = default);
}

public interface ICandleRepository
{
    Task AddRangeAsync(IEnumerable<Candle> candles, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Candle>> GetAsync(string market, string timeframe, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

public interface IStrategyRunRepository
{
    Task<StrategyRun> AddAsync(StrategyRun run, CancellationToken cancellationToken = default);
    Task UpdateAsync(StrategyRun run, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StrategyRun>> GetAllAsync(TradingMode? mode = null, CancellationToken cancellationToken = default);
}

public interface IPapertradingProfileRepository
{
    Task<PapertradingProfile> AddAsync(PapertradingProfile profile, CancellationToken cancellationToken = default);
    Task UpdateAsync(PapertradingProfile profile, CancellationToken cancellationToken = default);
    Task<PapertradingProfile?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PapertradingProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Resets balance, orders, positions and history of this profile back to its initial state.</summary>
    Task ResetAsync(long profileId, CancellationToken cancellationToken = default);
}

public interface IBotProfileRepository
{
    Task<BotProfile> AddAsync(BotProfile profile, CancellationToken cancellationToken = default);
    Task UpdateAsync(BotProfile profile, CancellationToken cancellationToken = default);
    Task<BotProfile?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BotProfile>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StrategyAssignment>> GetStrategyAssignmentsAsync(long botProfileId, CancellationToken cancellationToken = default);
    Task SaveStrategyAssignmentsAsync(long botProfileId, IReadOnlyList<StrategyAssignment> assignments, CancellationToken cancellationToken = default);
}

public interface ILogRepository
{
    Task AddAsync(LogEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LogEntry>> GetRecentAsync(int count = 500, LogEntryType? type = null, TradingMode? mode = null, CancellationToken cancellationToken = default);
}
