namespace BitvavoBot.Domain.Models;

public sealed record OrderBookLevel(decimal Price, decimal Amount);

public sealed record OrderBookSnapshot(
    string Market,
    IReadOnlyList<OrderBookLevel> Bids,
    IReadOnlyList<OrderBookLevel> Asks,
    DateTimeOffset Timestamp);
