using System.Text.Json;
using System.Text.Json.Serialization;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Domain.Models;

namespace BitvavoBot.Trading.Strategies;

public sealed class MovingAverageParameters
{
    [JsonPropertyName("fastPeriod")] public int FastPeriod { get; set; } = 9;
    [JsonPropertyName("slowPeriod")] public int SlowPeriod { get; set; } = 21;
}

/// <summary>
/// Classic moving-average crossover: buys on a golden cross (fast SMA crosses above slow SMA),
/// sells on a death cross (fast crosses below). Evaluated only on closed bars (functional spec 7).
/// </summary>
public sealed class MovingAverageStrategy : IStrategy
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private MovingAverageParameters _parameters = new();
    private readonly Queue<decimal> _closes = new();
    private bool? _fastAboveSlow;

    public StrategyType Type => StrategyType.MovingAverage;
    public string Name => "Moving Average";

    public void Configure(string parametersJson)
    {
        _parameters = string.IsNullOrWhiteSpace(parametersJson)
            ? new MovingAverageParameters()
            : JsonSerializer.Deserialize<MovingAverageParameters>(parametersJson, JsonOptions) ?? new MovingAverageParameters();

        if (_parameters.FastPeriod >= _parameters.SlowPeriod)
        {
            throw new ArgumentException("Fast MA period must be smaller than Slow MA period.", nameof(parametersJson));
        }

        Reset();
    }

    public IReadOnlyList<StrategySignal> OnMarketData(MarketDataEvent marketData)
    {
        if (marketData.ClosedBar is null) return Array.Empty<StrategySignal>();

        _closes.Enqueue(marketData.ClosedBar.Close);
        while (_closes.Count > _parameters.SlowPeriod)
        {
            _closes.Dequeue();
        }

        if (_closes.Count < _parameters.SlowPeriod) return Array.Empty<StrategySignal>();

        var closesArray = _closes.ToArray();
        var fastSma = closesArray[^_parameters.FastPeriod..].Average();
        var slowSma = closesArray.Average();

        var isFastAboveSlow = fastSma > slowSma;
        var previousState = _fastAboveSlow;
        _fastAboveSlow = isFastAboveSlow;

        if (previousState is null) return Array.Empty<StrategySignal>();

        if (!previousState.Value && isFastAboveSlow)
        {
            return new[] { new StrategySignal(marketData.Market, SignalAction.Buy, OrderType.Market, null, $"Golden cross: fast SMA {fastSma:F4} > slow SMA {slowSma:F4}") };
        }

        if (previousState.Value && !isFastAboveSlow)
        {
            return new[] { new StrategySignal(marketData.Market, SignalAction.Sell, OrderType.Market, null, $"Death cross: fast SMA {fastSma:F4} < slow SMA {slowSma:F4}") };
        }

        return Array.Empty<StrategySignal>();
    }

    public void Reset()
    {
        _closes.Clear();
        _fastAboveSlow = null;
    }
}
