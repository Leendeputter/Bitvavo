using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Models;

namespace BitvavoBot.Domain.Interfaces;

public sealed record OpenPositionRequest(
    string Market,
    PositionSide Side,
    decimal Leverage,
    MarginType MarginType,
    decimal PositionSizeInQuoteCurrency,
    decimal AutoStopLossPercentage,
    decimal? AutoTakeProfitPercentage);

public sealed record LeveragePositionResult(
    string ExternalId,
    string Market,
    PositionSide Side,
    decimal Leverage,
    MarginType MarginType,
    decimal Amount,
    decimal EntryPrice,
    decimal LiquidationPrice);

/// <summary>
/// Generic leverage/futures trading contract so Bitvavo (spot-only today), and later Bybit/Binance
/// Futures, can share one UI and one risk engine. Auto stop-loss is mandatory by design: there is no
/// method to disable it, matching the functional requirement that the UI control must be grayed out.
/// </summary>
public interface IExchangeLeverageClient
{
    TradingMode Mode { get; }

    Task<LeveragePositionResult> OpenPositionAsync(OpenPositionRequest request, CancellationToken cancellationToken = default);
    Task<LeveragePositionResult> ClosePositionAsync(string market, string externalPositionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeveragePositionResult>> GetOpenPositionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Computes the simulated/estimated liquidation price for a would-be position, used by both Live previews and Papertrading.</summary>
    decimal CalculateLiquidationPrice(decimal entryPrice, decimal leverage, PositionSide side, MarginType marginType);
}
