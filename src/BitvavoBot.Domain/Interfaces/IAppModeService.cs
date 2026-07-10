using BitvavoBot.Domain.Enums;

namespace BitvavoBot.Domain.Interfaces;

/// <summary>
/// Holds the single, application-wide Live/Paper switch (functional spec 1.2). Switching modes
/// while a bot is running must go through <see cref="IBotOrchestrator"/> first, which stops the
/// bot and only then flips this switch, so it is never possible for orders to be misrouted.
/// </summary>
public interface IAppModeService
{
    TradingMode CurrentMode { get; }
    event EventHandler<TradingMode>? ModeChanged;

    /// <summary>Switches mode directly. Throws <see cref="InvalidOperationException"/> if any bot profile is currently running.</summary>
    void SwitchMode(TradingMode newMode);
}
