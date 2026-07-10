namespace BitvavoBot.Domain.Models;

/// <summary>Real-time snapshot of a market's best bid/ask and 24h stats.</summary>
public sealed record Ticker(
    string Market,
    decimal Bid,
    decimal Ask,
    decimal Last,
    decimal Volume24h,
    decimal Change24hPercentage,
    decimal High24h,
    decimal Low24h,
    DateTimeOffset Timestamp)
{
    public decimal SpreadPercentage => Bid == 0m ? 0m : (Ask - Bid) / Bid * 100m;
}
