using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Domain.Models;
using BitvavoBot.Trading.Strategies;

namespace BitvavoBot.Trading;

/// <summary>
/// Runs one bot profile's analysis/trading loop at its configured scrape interval (functional spec
/// 4.1 step 3). Identical logic for Live and Paper — only the injected <see cref="IExchangeClient"/>
/// differs (functional spec 8.6).
/// </summary>
internal sealed class BotRunner : IAsyncDisposable
{
    private readonly BotProfile _profile;
    private readonly TradingMode _mode;
    private readonly IExchangeClient _exchangeClient;
    private readonly IRiskEngine _riskEngine;
    private readonly ITradingEngine _tradingEngine;
    private readonly ILogRepository _logRepository;
    private readonly IStrategyRunRepository _strategyRunRepository;
    private readonly Dictionary<string, (StrategyAssignment Assignment, IStrategy Strategy, StrategyRun Run)> _assignmentsByMarket;
    private readonly Dictionary<string, MarketInfo> _marketInfoByMarket;
    private readonly Dictionary<string, decimal> _lastBarClose = new();

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    public BotState State { get; private set; } = BotState.Stopped;
    public event EventHandler<BotState>? StateChanged;

    private BotRunner(
        BotProfile profile, TradingMode mode, IExchangeClient exchangeClient, IRiskEngine riskEngine,
        ITradingEngine tradingEngine, ILogRepository logRepository, IStrategyRunRepository strategyRunRepository,
        Dictionary<string, (StrategyAssignment, IStrategy, StrategyRun)> assignmentsByMarket,
        Dictionary<string, MarketInfo> marketInfoByMarket)
    {
        _profile = profile;
        _mode = mode;
        _exchangeClient = exchangeClient;
        _riskEngine = riskEngine;
        _tradingEngine = tradingEngine;
        _logRepository = logRepository;
        _strategyRunRepository = strategyRunRepository;
        _assignmentsByMarket = assignmentsByMarket;
        _marketInfoByMarket = marketInfoByMarket;
    }

    public static async Task<BotRunner> CreateAsync(
        BotProfile profile, IExchangeClient exchangeClient, IRiskEngine riskEngine, ITradingEngine tradingEngine,
        IBotProfileRepository botProfileRepository, ILogRepository logRepository, IStrategyRunRepository strategyRunRepository,
        IStrategyFactory strategyFactory, CancellationToken cancellationToken)
    {
        await exchangeClient.ConnectAsync(cancellationToken);

        var assignments = await botProfileRepository.GetStrategyAssignmentsAsync(profile.Id, cancellationToken);
        var markets = await exchangeClient.GetMarketsAsync(cancellationToken);
        var marketInfoByMarket = markets.ToDictionary(m => m.Market, m => m);

        var assignmentsByMarket = new Dictionary<string, (StrategyAssignment, IStrategy, StrategyRun)>();
        foreach (var assignment in assignments)
        {
            var strategy = strategyFactory.Create(assignment.StrategyType);
            strategy.Configure(assignment.ParametersJson);

            var run = await strategyRunRepository.AddAsync(new StrategyRun
            {
                Mode = profile.Mode,
                StrategyType = assignment.StrategyType,
                StrategyName = strategy.Name,
                BotProfileName = profile.Name,
                ParametersJson = assignment.ParametersJson,
                Markets = assignment.Market,
                StartedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            assignmentsByMarket[assignment.Market] = (assignment, strategy, run);
            await exchangeClient.SubscribeTickerAsync(assignment.Market, cancellationToken);
        }

        return new BotRunner(profile, profile.Mode, exchangeClient, riskEngine, tradingEngine, logRepository, strategyRunRepository, assignmentsByMarket, marketInfoByMarket);
    }

    public void Start()
    {
        if (_loopTask is not null) return;
        _loopCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunLoopAsync(_loopCts.Token));
        SetState(BotState.Running);
    }

    public void Pause() => SetState(BotState.Paused);

    public void Resume() => SetState(BotState.Running);

    public async Task StopAsync()
    {
        _loopCts?.Cancel();
        if (_loopTask is not null)
        {
            try { await _loopTask; } catch (OperationCanceledException) { }
        }
        _loopTask = null;

        foreach (var (_, _, run) in _assignmentsByMarket.Values)
        {
            run.EndedAt = DateTimeOffset.UtcNow;
            await _strategyRunRepository.UpdateAsync(run);
        }

        SetState(BotState.Stopped);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _profile.ScrapeIntervalSeconds)));

        do
        {
            try
            {
                await TickAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await LogAsync(LogEntryType.Error, $"Bot profile '{_profile.Name}' tick failed: {ex.Message}");
            }
        } while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        if (await _riskEngine.IsDailyLossLimitExceededAsync(_profile, _mode, cancellationToken))
        {
            await LogAsync(LogEntryType.Error, $"Bot profile '{_profile.Name}' hit its daily loss limit ({_profile.MaxLossPerDay:F2}). Stopping; open orders are left untouched.");
            await StopAsync();
            return;
        }

        foreach (var (market, (assignment, strategy, _)) in _assignmentsByMarket)
        {
            if (!_marketInfoByMarket.TryGetValue(market, out var marketInfo)) continue;

            var ticker = await _exchangeClient.GetTickerAsync(market, cancellationToken);

            var slTpResult = await _tradingEngine.CheckStopLossTakeProfitAsync(_profile, _mode, _exchangeClient, marketInfo, ticker, cancellationToken);
            if (slTpResult is { Placed: true })
            {
                await LogAsync(LogEntryType.Trade, $"{market}: stop-loss/take-profit closed the position.");
            }

            if (State != BotState.Running) continue;

            var previousClose = _lastBarClose.GetValueOrDefault(market, ticker.Last);
            _lastBarClose[market] = ticker.Last;
            var closedBar = new PriceBar(ticker.Timestamp, previousClose, Math.Max(previousClose, ticker.Last), Math.Min(previousClose, ticker.Last), ticker.Last, ticker.Volume24h);

            var signals = strategy.OnMarketData(new MarketDataEvent(market, ticker, closedBar));
            foreach (var signal in signals)
            {
                var result = await _tradingEngine.ExecuteSignalAsync(_profile, _mode, _exchangeClient, marketInfo, signal, ticker, cancellationToken);
                await LogExecutionAsync(market, signal, result);
            }

            var health = await _riskEngine.EvaluateOpenOrdersAsync(_profile, _mode, cancellationToken);
            if (health.BelowThreshold)
            {
                await LogAsync(LogEntryType.Warning, $"{market}: open orders at {health.OpenPercentage:F1}%, below threshold {_profile.MinOpenOrdersPercentage:F1}%.");
                if (health.ShouldAutoReplenish)
                {
                    var replenishResult = await _tradingEngine.ReplenishOpenOrdersAsync(_profile, _mode, _exchangeClient, marketInfo, ticker, cancellationToken);
                    if (replenishResult.Placed)
                    {
                        await LogAsync(LogEntryType.Info, $"{market}: auto-replenished open orders.");
                    }
                }
            }
        }
    }

    private async Task LogExecutionAsync(string market, StrategySignal signal, ExecutionResult result)
    {
        if (result.Placed)
        {
            await LogAsync(LogEntryType.Trade, $"{market}: {signal.Action} order placed ({signal.Reason}).");
        }
        else if (result.SkipReason == SkipReason.RiskBlocked)
        {
            await LogAsync(LogEntryType.Warning, $"{market}: {signal.Action} signal blocked by risk engine ({result.RiskReason}).");
        }
    }

    private Task LogAsync(LogEntryType type, string message) => _logRepository.AddAsync(new LogEntry
    {
        Timestamp = DateTimeOffset.UtcNow,
        Type = type,
        Message = message,
        Mode = _mode,
        Source = _profile.Name
    });

    private void SetState(BotState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(this, state);
    }

    public async ValueTask DisposeAsync()
    {
        _loopCts?.Cancel();
        if (_loopTask is not null)
        {
            try { await _loopTask; } catch (OperationCanceledException) { }
        }
        _loopCts?.Dispose();
    }
}
