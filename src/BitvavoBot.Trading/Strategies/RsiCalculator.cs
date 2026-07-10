namespace BitvavoBot.Trading.Strategies;

/// <summary>Wilder's RSI, computed incrementally as closed bars arrive.</summary>
public sealed class RsiCalculator
{
    private readonly int _period;
    private decimal? _previousClose;
    private decimal? _avgGain;
    private decimal? _avgLoss;
    private readonly List<(decimal Gain, decimal Loss)> _seedBuffer = new();

    public RsiCalculator(int period)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        _period = period;
    }

    /// <summary>Feeds one closed bar's close price. Returns the RSI value once enough history has accumulated, otherwise null.</summary>
    public decimal? AddClose(decimal close)
    {
        if (_previousClose is null)
        {
            _previousClose = close;
            return null;
        }

        var diff = close - _previousClose.Value;
        _previousClose = close;
        var gain = Math.Max(diff, 0m);
        var loss = Math.Max(-diff, 0m);

        if (_avgGain is null)
        {
            _seedBuffer.Add((gain, loss));
            if (_seedBuffer.Count < _period) return null;

            _avgGain = _seedBuffer.Sum(x => x.Gain) / _period;
            _avgLoss = _seedBuffer.Sum(x => x.Loss) / _period;
        }
        else
        {
            _avgGain = (_avgGain.Value * (_period - 1) + gain) / _period;
            _avgLoss = (_avgLoss!.Value * (_period - 1) + loss) / _period;
        }

        if (_avgLoss.Value == 0m) return 100m;
        var rs = _avgGain!.Value / _avgLoss.Value;
        return 100m - 100m / (1m + rs);
    }

    public void Reset()
    {
        _previousClose = null;
        _avgGain = null;
        _avgLoss = null;
        _seedBuffer.Clear();
    }
}
