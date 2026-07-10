using BitvavoBot.Domain.Enums;

namespace BitvavoBot.Domain.Entities;

/// <summary>A strategy bound to one market within a bot profile, with its own parameter set.</summary>
public class StrategyAssignment
{
    public long Id { get; set; }
    public long BotProfileId { get; set; }
    public string Market { get; set; } = string.Empty;
    public StrategyType StrategyType { get; set; }

    /// <summary>Serialized (JSON) strategy-specific parameters, see IStrategy implementations.</summary>
    public string ParametersJson { get; set; } = string.Empty;
}
