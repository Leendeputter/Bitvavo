using System.Collections.Concurrent;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Trading.Strategies;

namespace BitvavoBot.Trading;

/// <summary>UI-facing facade to start/stop/pause bot profiles (functional spec 2.3, 4).</summary>
public sealed class BotOrchestrator : IBotOrchestrator
{
    private readonly IBotProfileRepository _botProfileRepository;
    private readonly IExchangeClientFactory _exchangeClientFactory;
    private readonly IRiskEngine _riskEngine;
    private readonly ITradingEngine _tradingEngine;
    private readonly ILogRepository _logRepository;
    private readonly IStrategyRunRepository _strategyRunRepository;
    private readonly IStrategyFactory _strategyFactory;
    private readonly ConcurrentDictionary<string, BotRunner> _runners = new();

    public event EventHandler<(string ProfileName, BotState State)>? StateChanged;

    public IReadOnlyDictionary<string, BotState> RunningProfiles =>
        _runners.ToDictionary(kv => kv.Key, kv => kv.Value.State);

    public BotOrchestrator(
        IBotProfileRepository botProfileRepository, IExchangeClientFactory exchangeClientFactory,
        IRiskEngine riskEngine, ITradingEngine tradingEngine, ILogRepository logRepository,
        IStrategyRunRepository strategyRunRepository, IStrategyFactory strategyFactory)
    {
        _botProfileRepository = botProfileRepository;
        _exchangeClientFactory = exchangeClientFactory;
        _riskEngine = riskEngine;
        _tradingEngine = tradingEngine;
        _logRepository = logRepository;
        _strategyRunRepository = strategyRunRepository;
        _strategyFactory = strategyFactory;
    }

    public async Task StartAsync(string botProfileName, CancellationToken cancellationToken = default)
    {
        if (_runners.ContainsKey(botProfileName)) return;

        var profile = await _botProfileRepository.GetByNameAsync(botProfileName, cancellationToken)
            ?? throw new InvalidOperationException($"Bot profile '{botProfileName}' not found.");

        var exchangeClient = _exchangeClientFactory.GetClient(profile.Mode, profile.PapertradingProfileName);
        var runner = await BotRunner.CreateAsync(
            profile, exchangeClient, _riskEngine, _tradingEngine, _botProfileRepository,
            _logRepository, _strategyRunRepository, _strategyFactory, cancellationToken);

        runner.StateChanged += (_, state) => StateChanged?.Invoke(this, (botProfileName, state));
        _runners[botProfileName] = runner;
        runner.Start();
        StateChanged?.Invoke(this, (botProfileName, BotState.Running));
    }

    public Task PauseAsync(string botProfileName, CancellationToken cancellationToken = default)
    {
        if (_runners.TryGetValue(botProfileName, out var runner))
        {
            runner.Pause();
        }
        return Task.CompletedTask;
    }

    public async Task StopAsync(string botProfileName, CancellationToken cancellationToken = default)
    {
        if (_runners.TryRemove(botProfileName, out var runner))
        {
            await runner.StopAsync();
            await runner.DisposeAsync();
        }
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var profileName in _runners.Keys.ToArray())
        {
            await StopAsync(profileName, cancellationToken);
        }
    }
}
