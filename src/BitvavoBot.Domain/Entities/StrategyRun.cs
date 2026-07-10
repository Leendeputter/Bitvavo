using BitvavoBot.Domain.Enums;

namespace BitvavoBot.Domain.Entities;

/// <summary>
/// One execution of a strategy (live/paper run or historical backtest) so results can be compared.
/// </summary>
public class StrategyRun
{
    public long Id { get; set; }
    public TradingMode Mode { get; set; }
    public StrategyType StrategyType { get; set; }
    public string StrategyName { get; set; } = string.Empty;
    public string BotProfileName { get; set; } = string.Empty;

    /// <summary>Serialized (JSON) parameter set used for this run.</summary>
    public string ParametersJson { get; set; } = string.Empty;

    public string Markets { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public decimal TotalProfitLoss { get; set; }
    public decimal ReturnPercentage { get; set; }

    public decimal WinRate => TotalTrades == 0 ? 0m : (decimal)WinningTrades / TotalTrades * 100m;
}
