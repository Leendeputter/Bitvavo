namespace BitvavoBot.Domain.Entities;

/// <summary>
/// A named papertrading context: its own virtual starting balance, current balance and history.
/// Fully isolated from Live balance and from other papertrading profiles.
/// </summary>
public class PapertradingProfile
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = "EUR";
    public decimal StartingBalance { get; set; }
    public decimal CurrentBalance { get; set; }

    /// <summary>Optional slippage percentage applied to simulated fills (0 = no slippage).</summary>
    public decimal SlippagePercentage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResetAt { get; set; }
}
