using BitvavoBot.Domain.Calculations;
using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;

namespace BitvavoBot.Trading;

public enum RiskBlockReason
{
    None,
    MaxTradesPerDayReached,
    MaxLossPerDayExceeded,
    MaxConcurrentOrdersReached,
    MaxInvestmentReached
}

public sealed record RiskCheckResult(bool CanPlaceOrder, RiskBlockReason Reason);

public sealed record OpenOrdersHealth(decimal OpenPercentage, bool BelowThreshold, bool ShouldAutoReplenish);

/// <summary>
/// Implements the risk rules from functional spec 4.2, shared unchanged between Live and
/// Papertrading (functional spec 8.6) — the only external dependency is the mode-scoped
/// repositories, which already isolate Live/Paper data.
/// </summary>
public interface IRiskEngine
{
    Task<RiskCheckResult> CheckBeforePlacingOrderAsync(BotProfile profile, TradingMode mode, decimal candidateOrderQuoteValue, CancellationToken cancellationToken = default);
    Task<OpenOrdersHealth> EvaluateOpenOrdersAsync(BotProfile profile, TradingMode mode, CancellationToken cancellationToken = default);
    Task<bool> IsDailyLossLimitExceededAsync(BotProfile profile, TradingMode mode, CancellationToken cancellationToken = default);
}

public sealed class RiskEngine : IRiskEngine
{
    private readonly IOrderRepository _orderRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IPositionRepository _positionRepository;

    public RiskEngine(IOrderRepository orderRepository, ITradeRepository tradeRepository, IPositionRepository positionRepository)
    {
        _orderRepository = orderRepository;
        _tradeRepository = tradeRepository;
        _positionRepository = positionRepository;
    }

    public async Task<RiskCheckResult> CheckBeforePlacingOrderAsync(BotProfile profile, TradingMode mode, decimal candidateOrderQuoteValue, CancellationToken cancellationToken = default)
    {
        if (await IsDailyLossLimitExceededAsync(profile, mode, cancellationToken))
        {
            return new RiskCheckResult(false, RiskBlockReason.MaxLossPerDayExceeded);
        }

        if (profile.MaxTradesPerDay > 0)
        {
            var tradesToday = await _tradeRepository.CountTradesTodayAsync(mode, profile.Name, cancellationToken);
            if (tradesToday >= profile.MaxTradesPerDay)
            {
                return new RiskCheckResult(false, RiskBlockReason.MaxTradesPerDayReached);
            }
        }

        if (profile.MaxConcurrentOrders > 0)
        {
            var openCount = await _orderRepository.CountOpenForProfileAsync(mode, profile.Name, cancellationToken);
            if (openCount >= profile.MaxConcurrentOrders)
            {
                return new RiskCheckResult(false, RiskBlockReason.MaxConcurrentOrdersReached);
            }
        }

        if (profile.MaxInvestment > 0)
        {
            var currentlyInvested = await GetCurrentlyInvestedAsync(profile, mode, cancellationToken);
            if (currentlyInvested + candidateOrderQuoteValue > profile.MaxInvestment)
            {
                return new RiskCheckResult(false, RiskBlockReason.MaxInvestmentReached);
            }
        }

        return new RiskCheckResult(true, RiskBlockReason.None);
    }

    public async Task<bool> IsDailyLossLimitExceededAsync(BotProfile profile, TradingMode mode, CancellationToken cancellationToken = default)
    {
        if (profile.MaxLossPerDay <= 0) return false;
        var realizedPnlToday = await _tradeRepository.GetRealizedPnLTodayAsync(mode, profile.Name, cancellationToken);
        return realizedPnlToday <= -profile.MaxLossPerDay;
    }

    public async Task<OpenOrdersHealth> EvaluateOpenOrdersAsync(BotProfile profile, TradingMode mode, CancellationToken cancellationToken = default)
    {
        var openCount = await _orderRepository.CountOpenForProfileAsync(mode, profile.Name, cancellationToken);
        var totalCount = await _orderRepository.CountActiveForProfileAsync(mode, profile.Name, cancellationToken);
        var percentage = TradingCalculations.OpenOrdersPercentage(openCount, totalCount);

        var belowThreshold = totalCount > 0 && percentage < profile.MinOpenOrdersPercentage;
        var shouldReplenish = belowThreshold && profile.AutoReplenishOpenOrders && openCount < profile.MaxConcurrentOrders;

        return new OpenOrdersHealth(percentage, belowThreshold, shouldReplenish);
    }

    private async Task<decimal> GetCurrentlyInvestedAsync(BotProfile profile, TradingMode mode, CancellationToken cancellationToken)
    {
        var openOrders = await _orderRepository.GetOpenOrdersAsync(mode, profile.Name, cancellationToken: cancellationToken);
        var reservedInOpenBuyOrders = openOrders
            .Where(o => o.Side == OrderSide.Buy)
            .Sum(o => (o.Amount - o.FilledAmount) * (o.Price ?? 0m));

        var positions = await _positionRepository.GetOpenPositionsAsync(mode, profile.PapertradingProfileName, cancellationToken);
        var positionsValue = positions.Sum(p => p.Amount * p.AverageEntryPrice);

        return reservedInOpenBuyOrders + positionsValue;
    }
}
