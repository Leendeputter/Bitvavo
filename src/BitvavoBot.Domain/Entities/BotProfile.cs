using BitvavoBot.Domain.Enums;

namespace BitvavoBot.Domain.Entities;

/// <summary>
/// A named, savable trading bot configuration produced by the configuration wizard (functional spec 4.1).
/// Multiple profiles can coexist (e.g. "Conservatief BTC" and "Agressief Altcoins").
/// </summary>
public class BotProfile
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Step 1: trade amount
    public TradeAmountMode TradeAmountMode { get; set; }
    public decimal TradeAmountValue { get; set; }

    // Step 2: markets (comma-separated market symbols, e.g. "BTC-EUR,ETH-EUR")
    public string Markets { get; set; } = string.Empty;

    // Step 3: scrape/analysis interval
    public int ScrapeIntervalSeconds { get; set; } = 30;

    // Step 4: limit order settings
    public decimal BuyMarginPercentage { get; set; }
    public decimal SellMarginPercentage { get; set; }

    // Step 5: open orders control
    public decimal MinOpenOrdersPercentage { get; set; } = 80m;
    public bool AutoReplenishOpenOrders { get; set; }

    // Step 6: risk management
    public int MaxTradesPerDay { get; set; }
    public decimal MaxLossPerDay { get; set; }
    public decimal MaxInvestment { get; set; }
    public decimal StopLossPercentage { get; set; }
    public decimal TakeProfitPercentage { get; set; }
    public int MaxConcurrentOrders { get; set; }
    public int WebSocketFallbackSeconds { get; set; } = 30;

    // Step 8: mode
    public TradingMode Mode { get; set; }
    public string? PapertradingProfileName { get; set; }

    public BotState State { get; set; } = BotState.Stopped;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public IReadOnlyList<string> GetMarketList() =>
        Markets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
