namespace BitvavoBot.Domain.Models;

/// <summary>
/// A single market data update fed into the strategy engine. The engine treats live WebSocket
/// events and historical/simulated events identically (functional spec 9).
/// </summary>
public sealed record MarketDataEvent(string Market, Ticker Ticker, PriceBar? ClosedBar = null);

/// <summary>A closed OHLCV bar, used by indicator-based strategies (RSI, moving averages).</summary>
public sealed record PriceBar(DateTimeOffset OpenTime, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume);
