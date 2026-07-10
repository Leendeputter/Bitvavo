using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Models;

namespace BitvavoBot.Domain.Interfaces;

/// <summary>
/// A strategy is a pure function of market data: given the same input events and parameters it
/// must produce the same signals whether fed by a live WebSocket or a Papertrading/backtest feed
/// (functional spec 7). Implementations must not depend on wall-clock time or external I/O.
/// </summary>
public interface IStrategy
{
    StrategyType Type { get; }
    string Name { get; }

    /// <summary>(Re)configures the strategy from its JSON parameter set. Must be called before <see cref="OnMarketData"/>.</summary>
    void Configure(string parametersJson);

    /// <summary>Processes one market data event for the market this strategy instance is bound to and returns zero or more signals.</summary>
    IReadOnlyList<StrategySignal> OnMarketData(MarketDataEvent marketData);

    /// <summary>Resets all internal state (indicator history, grid levels, etc.) back to empty.</summary>
    void Reset();
}
