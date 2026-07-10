using BitvavoBot.Domain.Enums;

namespace BitvavoBot.Domain.Interfaces;

/// <summary>
/// Resolves the correct <see cref="IExchangeClient"/> instance for a bot profile: the single Live
/// Bitvavo client, or the <c>PaperExchangeClient</c> bound to a specific Papertrading profile
/// (functional spec 9 — dependency injection decides which implementation is active).
/// </summary>
public interface IExchangeClientFactory
{
    IExchangeClient GetClient(TradingMode mode, string? papertradingProfileName);
}
