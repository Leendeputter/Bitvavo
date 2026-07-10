using System.Text.Json;
using System.Text.Json.Serialization;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Domain.Models;

namespace BitvavoBot.Trading.Strategies;

public sealed class MarketMakingParameters
{
    [JsonPropertyName("spreadPercentage")] public decimal SpreadPercentage { get; set; } = 0.5m;
    [JsonPropertyName("orderSize")] public decimal OrderSize { get; set; } = 1m;
    [JsonPropertyName("levels")] public int Levels { get; set; } = 1;
}

/// <summary>
/// Quotes symmetric buy/sell limit orders around the mid price at increasing spread per level.
/// To stay deterministic and avoid re-quoting on every tick, it only re-signals once the mid
/// price has moved by at least a quarter of the configured spread since the last quote.
/// </summary>
public sealed class MarketMakingStrategy : IStrategy
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private MarketMakingParameters _parameters = new();
    private decimal? _lastQuoteMidPrice;

    public StrategyType Type => StrategyType.MarketMaking;
    public string Name => "Market Making";

    public void Configure(string parametersJson)
    {
        _parameters = string.IsNullOrWhiteSpace(parametersJson)
            ? new MarketMakingParameters()
            : JsonSerializer.Deserialize<MarketMakingParameters>(parametersJson, JsonOptions) ?? new MarketMakingParameters();
        Reset();
    }

    public IReadOnlyList<StrategySignal> OnMarketData(MarketDataEvent marketData)
    {
        var mid = (marketData.Ticker.Bid + marketData.Ticker.Ask) / 2m;
        if (mid <= 0m) return Array.Empty<StrategySignal>();

        var spacing = _parameters.SpreadPercentage / 100m;
        var repriceThreshold = mid * spacing / 4m;

        if (_lastQuoteMidPrice.HasValue && Math.Abs(mid - _lastQuoteMidPrice.Value) < repriceThreshold)
        {
            return Array.Empty<StrategySignal>();
        }

        _lastQuoteMidPrice = mid;

        var signals = new List<StrategySignal>();
        for (var level = 1; level <= _parameters.Levels; level++)
        {
            var offset = spacing * level;
            signals.Add(new StrategySignal(marketData.Market, SignalAction.Buy, OrderType.Limit, mid * (1m - offset), $"Market making quote, level {level}"));
            signals.Add(new StrategySignal(marketData.Market, SignalAction.Sell, OrderType.Limit, mid * (1m + offset), $"Market making quote, level {level}"));
        }

        return signals;
    }

    public void Reset()
    {
        _lastQuoteMidPrice = null;
    }
}
