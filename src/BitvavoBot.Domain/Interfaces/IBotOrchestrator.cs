using BitvavoBot.Domain.Enums;

namespace BitvavoBot.Domain.Interfaces;

/// <summary>Facade used by the UI layer to start/stop/pause bot profiles, shared by Live and Paper.</summary>
public interface IBotOrchestrator
{
    IReadOnlyDictionary<string, BotState> RunningProfiles { get; }

    event EventHandler<(string ProfileName, BotState State)>? StateChanged;

    Task StartAsync(string botProfileName, CancellationToken cancellationToken = default);
    Task PauseAsync(string botProfileName, CancellationToken cancellationToken = default);
    Task StopAsync(string botProfileName, CancellationToken cancellationToken = default);
    Task StopAllAsync(CancellationToken cancellationToken = default);
}
