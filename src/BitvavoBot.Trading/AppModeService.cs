using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;

namespace BitvavoBot.Trading;

/// <summary>
/// The single, application-wide Live/Paper switch (functional spec 1.2). Switching is refused
/// while any bot profile is running; the UI is expected to stop the bot (after user confirmation,
/// spec 8.5) and only then call <see cref="SwitchMode"/>.
/// </summary>
public sealed class AppModeService : IAppModeService
{
    private readonly IBotOrchestrator _orchestrator;

    public TradingMode CurrentMode { get; private set; }
    public event EventHandler<TradingMode>? ModeChanged;

    public AppModeService(IBotOrchestrator orchestrator, TradingMode initialMode = TradingMode.Paper)
    {
        _orchestrator = orchestrator;
        CurrentMode = initialMode;
    }

    public void SwitchMode(TradingMode newMode)
    {
        if (newMode == CurrentMode) return;

        if (_orchestrator.RunningProfiles.Values.Any(state => state != BotState.Stopped))
        {
            throw new InvalidOperationException("Cannot switch mode while a bot profile is running. Stop it first.");
        }

        CurrentMode = newMode;
        ModeChanged?.Invoke(this, newMode);
    }
}
