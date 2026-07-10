using System.Collections.Concurrent;
using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;

namespace BitvavoBot.Exchange.Paper;

/// <summary>
/// Simulated leverage/futures trading for one Papertrading profile (functional spec 6). Bitvavo
/// itself is spot-only, so this is the only <see cref="IExchangeLeverageClient"/> implementation
/// in v1; later exchanges (Bybit, Binance Futures) implement the same interface for Live trading.
/// Auto stop-loss is always required by <see cref="OpenPositionRequest"/> — there is no code path
/// that opens a position without one.
/// </summary>
public sealed class PaperLeverageClient : IExchangeLeverageClient
{
    private readonly IMarketDataFeed _marketData;
    private readonly IPositionRepository _positionRepository;
    private readonly string _papertradingProfileName;
    private readonly ConcurrentDictionary<string, Position> _openPositions = new();

    public TradingMode Mode => TradingMode.Paper;

    public PaperLeverageClient(IMarketDataFeed marketData, IPositionRepository positionRepository, string papertradingProfileName)
    {
        _marketData = marketData;
        _positionRepository = positionRepository;
        _papertradingProfileName = papertradingProfileName;
    }

    public async Task<LeveragePositionResult> OpenPositionAsync(OpenPositionRequest request, CancellationToken cancellationToken = default)
    {
        var ticker = await _marketData.GetTickerAsync(request.Market, cancellationToken);
        var entryPrice = request.Side == PositionSide.Long ? ticker.Ask : ticker.Bid;
        var amount = request.PositionSizeInQuoteCurrency * request.Leverage / entryPrice;
        var liquidationPrice = CalculateLiquidationPrice(entryPrice, request.Leverage, request.Side, request.MarginType);
        var asset = request.Market.Split('-')[0];

        var position = new Position
        {
            Mode = TradingMode.Paper,
            Market = request.Market,
            Asset = asset,
            PapertradingProfileName = _papertradingProfileName,
            Amount = amount,
            AverageEntryPrice = entryPrice,
            Side = request.Side,
            Leverage = request.Leverage,
            MarginType = request.MarginType,
            LiquidationPrice = liquidationPrice,
            OpenedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _positionRepository.UpsertAsync(position, cancellationToken);
        var externalId = $"PAPER-LEV-{Guid.NewGuid():N}";
        _openPositions[externalId] = position;

        return new LeveragePositionResult(externalId, request.Market, request.Side, request.Leverage, request.MarginType, amount, entryPrice, liquidationPrice);
    }

    public async Task<LeveragePositionResult> ClosePositionAsync(string market, string externalPositionId, CancellationToken cancellationToken = default)
    {
        if (!_openPositions.TryRemove(externalPositionId, out var position))
        {
            throw new InvalidOperationException($"Papertrading leverage position {externalPositionId} not found.");
        }

        position.ClosedAt = DateTimeOffset.UtcNow;
        position.UpdatedAt = position.ClosedAt.Value;
        await _positionRepository.UpsertAsync(position, cancellationToken);

        return new LeveragePositionResult(externalPositionId, market, position.Side!.Value, position.Leverage!.Value, position.MarginType!.Value, position.Amount, position.AverageEntryPrice, position.LiquidationPrice ?? 0m);
    }

    public Task<IReadOnlyList<LeveragePositionResult>> GetOpenPositionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LeveragePositionResult>>(_openPositions
            .Select(kv => new LeveragePositionResult(kv.Key, kv.Value.Market, kv.Value.Side!.Value, kv.Value.Leverage!.Value, kv.Value.MarginType!.Value, kv.Value.Amount, kv.Value.AverageEntryPrice, kv.Value.LiquidationPrice ?? 0m))
            .ToList());

    /// <summary>
    /// Simplified maintenance-margin-free liquidation estimate (v1): the price move against the
    /// position that would wipe out the full margin, i.e. entryPrice adjusted by 1/leverage.
    /// </summary>
    public decimal CalculateLiquidationPrice(decimal entryPrice, decimal leverage, PositionSide side, MarginType marginType)
    {
        if (leverage <= 0) throw new ArgumentOutOfRangeException(nameof(leverage));
        var move = entryPrice / leverage;
        return side == PositionSide.Long ? entryPrice - move : entryPrice + move;
    }
}
