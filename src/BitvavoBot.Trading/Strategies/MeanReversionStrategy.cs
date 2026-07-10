using System.Text.Json;
using System.Text.Json.Serialization;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Domain.Models;

namespace BitvavoBot.Trading.Strategies;

public sealed class MeanReversionParameters
{
    [JsonPropertyName("rsiPeriod")] public int RsiPeriod { get; set; } = 14;
    [JsonPropertyName("rsiBuyLevel")] public decimal RsiBuyLevel { get; set; } = 30m;
    [JsonPropertyName("rsiSellLevel")] public decimal RsiSellLevel { get; set; } = 70m;
}

/// <summary>
/// RSI-based mean reversion: buys when RSI drops to/below the buy level (oversold), sells when it
/// rises to/above the sell level (overbought). Only evaluated on closed bars (functional spec 7),
/// with hysteresis so each crossing signals once rather than on every bar while RSI stays extreme.
/// </summary>
public sealed class MeanReversionStrategy : IStrategy
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private MeanReversionParameters _parameters = new();
    private RsiCalculator _rsi = new(14);
    private bool _oversoldSignalPending;
    private bool _overboughtSignalPending;

    public StrategyType Type => StrategyType.MeanReversion;
    public string Name => "Mean Reversion";

    public void Configure(string parametersJson)
    {
        _parameters = string.IsNullOrWhiteSpace(parametersJson)
            ? new MeanReversionParameters()
            : JsonSerializer.Deserialize<MeanReversionParameters>(parametersJson, JsonOptions) ?? new MeanReversionParameters();
        Reset();
    }

    public IReadOnlyList<StrategySignal> OnMarketData(MarketDataEvent marketData)
    {
        if (marketData.ClosedBar is null) return Array.Empty<StrategySignal>();

        var rsi = _rsi.AddClose(marketData.ClosedBar.Close);
        if (rsi is null) return Array.Empty<StrategySignal>();

        if (rsi.Value <= _parameters.RsiBuyLevel)
        {
            if (_oversoldSignalPending) return Array.Empty<StrategySignal>();
            _oversoldSignalPending = true;
            _overboughtSignalPending = false;
            return new[] { new StrategySignal(marketData.Market, SignalAction.Buy, OrderType.Market, null, $"RSI {rsi.Value:F2} <= buy level {_parameters.RsiBuyLevel}") };
        }

        if (rsi.Value >= _parameters.RsiSellLevel)
        {
            if (_overboughtSignalPending) return Array.Empty<StrategySignal>();
            _overboughtSignalPending = true;
            _oversoldSignalPending = false;
            return new[] { new StrategySignal(marketData.Market, SignalAction.Sell, OrderType.Market, null, $"RSI {rsi.Value:F2} >= sell level {_parameters.RsiSellLevel}") };
        }

        _oversoldSignalPending = false;
        _overboughtSignalPending = false;
        return Array.Empty<StrategySignal>();
    }

    public void Reset()
    {
        _rsi = new RsiCalculator(_parameters.RsiPeriod);
        _oversoldSignalPending = false;
        _overboughtSignalPending = false;
    }
}
