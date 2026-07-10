using BitvavoBot.Domain.Enums;

namespace BitvavoBot.Domain.Entities;

/// <summary>
/// A spot holding or a leverage position for one asset, in one mode.
/// Average purchase price is a weighted average, recalculated on every new buy.
/// </summary>
public class Position
{
    public long Id { get; set; }
    public TradingMode Mode { get; set; }
    public string Market { get; set; } = string.Empty;
    public string Asset { get; set; } = string.Empty;

    /// <summary>
    /// Set only when Mode = Paper: which Papertrading profile (virtual account) owns this
    /// position. Live positions have exactly one real account, so this is null for Mode = Live.
    /// </summary>
    public string? PapertradingProfileName { get; set; }

    public decimal Amount { get; set; }
    public decimal AverageEntryPrice { get; set; }

    /// <summary>Null for plain spot holdings; set for leverage positions.</summary>
    public PositionSide? Side { get; set; }
    public decimal? Leverage { get; set; }
    public MarginType? MarginType { get; set; }
    public decimal? LiquidationPrice { get; set; }

    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    public bool IsOpen => ClosedAt is null && Amount != 0m;

    /// <summary>
    /// Recalculates the weighted-average entry price after adding a new buy of <paramref name="addAmount"/>
    /// at <paramref name="addPrice"/>.
    /// </summary>
    public void ApplyBuy(decimal addAmount, decimal addPrice)
    {
        if (addAmount <= 0m) throw new ArgumentOutOfRangeException(nameof(addAmount));

        var totalCost = (Amount * AverageEntryPrice) + (addAmount * addPrice);
        var totalAmount = Amount + addAmount;
        AverageEntryPrice = totalAmount == 0m ? 0m : totalCost / totalAmount;
        Amount = totalAmount;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ApplySell(decimal removeAmount)
    {
        if (removeAmount <= 0m) throw new ArgumentOutOfRangeException(nameof(removeAmount));
        Amount -= removeAmount;
        UpdatedAt = DateTimeOffset.UtcNow;
        if (Amount <= 0m)
        {
            Amount = 0m;
            ClosedAt = UpdatedAt;
        }
    }
}
