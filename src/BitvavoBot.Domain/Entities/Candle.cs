namespace BitvavoBot.Domain.Entities;

/// <summary>OHLCV candle. Market data is mode-independent (no Live/Paper split).</summary>
public class Candle
{
    public long Id { get; set; }
    public string Market { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public DateTimeOffset OpenTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
}
