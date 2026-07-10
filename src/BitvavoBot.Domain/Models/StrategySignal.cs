using BitvavoBot.Domain.Enums;

namespace BitvavoBot.Domain.Models;

public enum SignalAction
{
    Buy,
    Sell,
    CancelOpenOrders
}

/// <summary>A trading signal emitted by a strategy for the trading engine to act on.</summary>
public sealed record StrategySignal(
    string Market,
    SignalAction Action,
    OrderType OrderType,
    decimal? SuggestedPrice,
    string Reason);
