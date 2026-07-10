using System.Text.Json;
using System.Text.Json.Serialization;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Domain.Models;

namespace BitvavoBot.Trading.Strategies;

public sealed class GridTradingParameters
{
    [JsonPropertyName("gridSpacingPercentage")] public decimal GridSpacingPercentage { get; set; } = 1m;
    [JsonPropertyName("gridCount")] public int GridCount { get; set; } = 5;
}

/// <summary>
/// Places a symmetric grid of buy/sell levels around the price observed on the first market data
/// event. Each level fires exactly one signal per crossing (functional spec 7); calling
/// <see cref="Reset"/> re-anchors the grid on the next event.
/// </summary>
public sealed class GridTradingStrategy : IStrategy
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private GridTradingParameters _parameters = new();
    private decimal? _basePrice;
    private readonly HashSet<int> _triggeredBuyLevels = new();
    private readonly HashSet<int> _triggeredSellLevels = new();

    public StrategyType Type => StrategyType.GridTrading;
    public string Name => "Grid Trading";

    public void Configure(string parametersJson)
    {
        _parameters = string.IsNullOrWhiteSpace(parametersJson)
            ? new GridTradingParameters()
            : JsonSerializer.Deserialize<GridTradingParameters>(parametersJson, JsonOptions) ?? new GridTradingParameters();
        Reset();
    }

    public IReadOnlyList<StrategySignal> OnMarketData(MarketDataEvent marketData)
    {
        var price = marketData.Ticker.Last;
        _basePrice ??= price;

        var signals = new List<StrategySignal>();
        var spacing = _parameters.GridSpacingPercentage / 100m;

        for (var level = 1; level <= _parameters.GridCount; level++)
        {
            var buyLevelPrice = _basePrice.Value * (1m - spacing * level);
            var sellLevelPrice = _basePrice.Value * (1m + spacing * level);

            if (!_triggeredBuyLevels.Contains(level) && price <= buyLevelPrice)
            {
                _triggeredBuyLevels.Add(level);
                signals.Add(new StrategySignal(marketData.Market, SignalAction.Buy, OrderType.Limit, buyLevelPrice, $"Grid buy level {level} reached"));
            }

            if (!_triggeredSellLevels.Contains(level) && price >= sellLevelPrice)
            {
                _triggeredSellLevels.Add(level);
                signals.Add(new StrategySignal(marketData.Market, SignalAction.Sell, OrderType.Limit, sellLevelPrice, $"Grid sell level {level} reached"));
            }
        }

        return signals;
    }

    public void Reset()
    {
        _basePrice = null;
        _triggeredBuyLevels.Clear();
        _triggeredSellLevels.Clear();
    }
}
