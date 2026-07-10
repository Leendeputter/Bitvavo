namespace BitvavoBot.Domain.Models;

/// <summary>Static trading rules for a market, needed to round orders to the correct tick/step size.</summary>
public sealed record MarketInfo(
    string Market,
    string BaseAsset,
    string QuoteAsset,
    int PricePrecision,
    int AmountPrecision,
    decimal MinOrderAmountInBaseAsset,
    decimal MinOrderAmountInQuoteAsset,
    bool TradingAllowed);
