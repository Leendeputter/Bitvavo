using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;

namespace BitvavoBot.Tests.Fakes;

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private long _nextId = 1;
    public List<Order> Orders { get; } = new();

    public Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        order.Id = _nextId++;
        Orders.Add(order);
        return Task.FromResult(order);
    }

    public Task UpdateAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<Order?> GetByExternalIdAsync(TradingMode mode, string externalId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Orders.FirstOrDefault(o => o.Mode == mode && o.ExternalId == externalId));

    public Task<IReadOnlyList<Order>> GetOpenOrdersAsync(TradingMode mode, string? botProfileName = null, string? papertradingProfileName = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Order>>(Orders.Where(o => o.Mode == mode && o.IsOpen
            && (botProfileName is null || o.BotProfileName == botProfileName)
            && (papertradingProfileName is null || o.PapertradingProfileName == papertradingProfileName)).ToList());

    public Task<IReadOnlyList<Order>> GetAllAsync(TradingMode mode, string? botProfileName = null, string? papertradingProfileName = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Order>>(Orders.Where(o => o.Mode == mode
            && (botProfileName is null || o.BotProfileName == botProfileName)
            && (papertradingProfileName is null || o.PapertradingProfileName == papertradingProfileName)).ToList());

    public Task<int> CountActiveForProfileAsync(TradingMode mode, string botProfileName, CancellationToken cancellationToken = default) =>
        Task.FromResult(Orders.Count(o => o.Mode == mode && o.BotProfileName == botProfileName));

    public Task<int> CountOpenForProfileAsync(TradingMode mode, string botProfileName, CancellationToken cancellationToken = default) =>
        Task.FromResult(Orders.Count(o => o.Mode == mode && o.BotProfileName == botProfileName && o.IsOpen));
}

public sealed class InMemoryTradeRepository : ITradeRepository
{
    private long _nextId = 1;
    public List<Trade> Trades { get; } = new();
    public List<Order> Orders { get; set; } = new();

    public Task<Trade> AddAsync(Trade trade, CancellationToken cancellationToken = default)
    {
        trade.Id = _nextId++;
        Trades.Add(trade);
        return Task.FromResult(trade);
    }

    public Task<IReadOnlyList<Trade>> GetAllAsync(TradingMode mode, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Trade>>(Trades.Where(t => t.Mode == mode
            && (!from.HasValue || t.Timestamp >= from.Value)
            && (!to.HasValue || t.Timestamp <= to.Value)).ToList());

    public Task<int> CountTradesTodayAsync(TradingMode mode, string botProfileName, CancellationToken cancellationToken = default)
    {
        var today = DateTimeOffset.UtcNow.Date;
        var count = Trades.Count(t => t.Mode == mode && t.Timestamp.Date == today
            && Orders.Any(o => o.Id == t.OrderId && o.BotProfileName == botProfileName));
        return Task.FromResult(count);
    }

    public Task<decimal> GetRealizedPnLTodayAsync(TradingMode mode, string botProfileName, CancellationToken cancellationToken = default)
    {
        var today = DateTimeOffset.UtcNow.Date;
        var sum = Trades.Where(t => t.Mode == mode && t.Timestamp.Date == today && t.ProfitLoss.HasValue
            && Orders.Any(o => o.Id == t.OrderId && o.BotProfileName == botProfileName))
            .Sum(t => t.ProfitLoss!.Value);
        return Task.FromResult(sum);
    }
}

public sealed class InMemoryPositionRepository : IPositionRepository
{
    private long _nextId = 1;
    public List<Position> Positions { get; } = new();

    public Task<Position?> GetOpenPositionAsync(TradingMode mode, string asset, string? papertradingProfileName = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Positions.FirstOrDefault(p => p.Mode == mode && p.Asset == asset && p.IsOpen
            && (mode != TradingMode.Paper || p.PapertradingProfileName == papertradingProfileName)));

    public Task<IReadOnlyList<Position>> GetOpenPositionsAsync(TradingMode mode, string? papertradingProfileName = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Position>>(Positions.Where(p => p.Mode == mode && p.IsOpen
            && (mode != TradingMode.Paper || p.PapertradingProfileName == papertradingProfileName)).ToList());

    public Task UpsertAsync(Position position, CancellationToken cancellationToken = default)
    {
        if (position.Id == 0)
        {
            position.Id = _nextId++;
            Positions.Add(position);
        }
        return Task.CompletedTask;
    }
}

public sealed class InMemoryPapertradingProfileRepository : IPapertradingProfileRepository
{
    private long _nextId = 1;
    public List<PapertradingProfile> Profiles { get; } = new();

    public Task<PapertradingProfile> AddAsync(PapertradingProfile profile, CancellationToken cancellationToken = default)
    {
        profile.Id = _nextId++;
        Profiles.Add(profile);
        return Task.FromResult(profile);
    }

    public Task UpdateAsync(PapertradingProfile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<PapertradingProfile?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(Profiles.FirstOrDefault(p => p.Name == name));

    public Task<IReadOnlyList<PapertradingProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PapertradingProfile>>(Profiles.ToList());

    public Task ResetAsync(long profileId, CancellationToken cancellationToken = default)
    {
        var profile = Profiles.First(p => p.Id == profileId);
        profile.CurrentBalance = profile.StartingBalance;
        return Task.CompletedTask;
    }
}
