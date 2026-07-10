namespace BitvavoBot.Domain.Enums;

/// <summary>
/// Global mode switch. Never mix data or orders between these two values.
/// </summary>
public enum TradingMode
{
    Live = 0,
    Paper = 1
}
