using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;

namespace BitvavoBot.Exchange.Bitvavo;

/// <summary>
/// Bitvavo's public API is spot-only; there is no Live leverage/futures product to trade against.
/// This implementation exists purely so <see cref="IExchangeLeverageClient"/> can be resolved for
/// Live mode without a null reference — every call fails clearly instead of silently. Leverage
/// trading is only actually usable today in Papertrading (functional spec 6).
/// </summary>
public sealed class UnsupportedLeverageClient : IExchangeLeverageClient
{
    public TradingMode Mode => TradingMode.Live;

    private static Exception NotSupported() =>
        new NotSupportedException("Bitvavo does not offer leverage/margin/futures trading. Use Papertrading to simulate leverage strategies.");

    public Task<LeveragePositionResult> OpenPositionAsync(OpenPositionRequest request, CancellationToken cancellationToken = default) => throw NotSupported();
    public Task<LeveragePositionResult> ClosePositionAsync(string market, string externalPositionId, CancellationToken cancellationToken = default) => throw NotSupported();
    public Task<IReadOnlyList<LeveragePositionResult>> GetOpenPositionsAsync(CancellationToken cancellationToken = default) => throw NotSupported();
    public decimal CalculateLiquidationPrice(decimal entryPrice, decimal leverage, PositionSide side, MarginType marginType) => throw NotSupported();
}
