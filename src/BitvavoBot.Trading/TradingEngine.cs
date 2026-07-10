using BitvavoBot.Domain.Calculations;
using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Domain.Models;

namespace BitvavoBot.Trading;

public enum SkipReason
{
    None,
    RiskBlocked,
    NoOpenPosition,
    BelowMinimumOrderSize
}

public sealed record ExecutionResult(bool Placed, SkipReason SkipReason, OrderResult? Order, RiskBlockReason? RiskReason = null);

/// <summary>
/// Shared trading logic between Live and Papertrading (functional spec 8.6 / 9): turns a strategy
/// signal into an order using the exact calculation rules from spec 4.2, after clearing the risk
/// engine. Which <see cref="IExchangeClient"/> is passed in (real or simulated) is the only thing
/// that differs between the two modes.
/// </summary>
public interface ITradingEngine
{
    Task<ExecutionResult> ExecuteSignalAsync(
        BotProfile profile, TradingMode mode, IExchangeClient exchangeClient, MarketInfo marketInfo,
        StrategySignal signal, Ticker ticker, CancellationToken cancellationToken = default);

    /// <summary>Checks an open position's stop-loss/take-profit levels against the latest ticker and closes it if triggered.</summary>
    Task<ExecutionResult?> CheckStopLossTakeProfitAsync(
        BotProfile profile, TradingMode mode, IExchangeClient exchangeClient, MarketInfo marketInfo,
        Ticker ticker, CancellationToken cancellationToken = default);

    /// <summary>Places one additional buy limit order to bring the open-orders percentage back above the configured threshold.</summary>
    Task<ExecutionResult> ReplenishOpenOrdersAsync(
        BotProfile profile, TradingMode mode, IExchangeClient exchangeClient, MarketInfo marketInfo,
        Ticker ticker, CancellationToken cancellationToken = default);
}

public sealed class TradingEngine : ITradingEngine
{
    private readonly IRiskEngine _riskEngine;
    private readonly IPositionRepository _positionRepository;

    public TradingEngine(IRiskEngine riskEngine, IPositionRepository positionRepository)
    {
        _riskEngine = riskEngine;
        _positionRepository = positionRepository;
    }

    public async Task<ExecutionResult> ExecuteSignalAsync(
        BotProfile profile, TradingMode mode, IExchangeClient exchangeClient, MarketInfo marketInfo,
        StrategySignal signal, Ticker ticker, CancellationToken cancellationToken = default)
    {
        return signal.Action switch
        {
            SignalAction.Buy => await PlaceBuyAsync(profile, mode, exchangeClient, marketInfo, signal, ticker, cancellationToken),
            SignalAction.Sell => await PlaceSellAsync(profile, mode, exchangeClient, marketInfo, signal, ticker, cancellationToken),
            SignalAction.CancelOpenOrders => await CancelOpenOrdersAsync(exchangeClient, marketInfo.Market, cancellationToken),
            _ => new ExecutionResult(false, SkipReason.None, null)
        };
    }

    public async Task<ExecutionResult?> CheckStopLossTakeProfitAsync(
        BotProfile profile, TradingMode mode, IExchangeClient exchangeClient, MarketInfo marketInfo,
        Ticker ticker, CancellationToken cancellationToken = default)
    {
        var position = await _positionRepository.GetOpenPositionAsync(mode, marketInfo.BaseAsset, profile.PapertradingProfileName, cancellationToken);
        if (position is null || !position.IsOpen) return null;

        var stopLossPrice = profile.StopLossPercentage > 0
            ? TradingCalculations.StopLossPrice(position.AverageEntryPrice, profile.StopLossPercentage)
            : (decimal?)null;
        var takeProfitPrice = profile.TakeProfitPercentage > 0
            ? TradingCalculations.TakeProfitPrice(position.AverageEntryPrice, profile.TakeProfitPercentage)
            : (decimal?)null;

        var stopLossTriggered = stopLossPrice.HasValue && ticker.Bid <= stopLossPrice.Value;
        var takeProfitTriggered = takeProfitPrice.HasValue && ticker.Bid >= takeProfitPrice.Value;
        if (!stopLossTriggered && !takeProfitTriggered) return null;

        var request = new PlaceOrderRequest(marketInfo.Market, OrderSide.Sell, OrderType.Market, position.Amount, null, profile.Name, null);
        var order = await exchangeClient.PlaceOrderAsync(request, cancellationToken);
        return new ExecutionResult(true, SkipReason.None, order);
    }

    public async Task<ExecutionResult> ReplenishOpenOrdersAsync(
        BotProfile profile, TradingMode mode, IExchangeClient exchangeClient, MarketInfo marketInfo,
        Ticker ticker, CancellationToken cancellationToken = default)
    {
        var buyPrice = TradingCalculations.BuyLimitPrice(ticker.Bid, profile.BuyMarginPercentage);
        return await PlaceBuyOrderAsync(profile, mode, exchangeClient, marketInfo, buyPrice, cancellationToken);
    }

    private async Task<ExecutionResult> PlaceBuyAsync(
        BotProfile profile, TradingMode mode, IExchangeClient exchangeClient, MarketInfo marketInfo,
        StrategySignal signal, Ticker ticker, CancellationToken cancellationToken)
    {
        var price = signal.SuggestedPrice ?? TradingCalculations.BuyLimitPrice(ticker.Bid, profile.BuyMarginPercentage);
        return await PlaceBuyOrderAsync(profile, mode, exchangeClient, marketInfo, price, cancellationToken, signal.OrderType);
    }

    private async Task<ExecutionResult> PlaceBuyOrderAsync(
        BotProfile profile, TradingMode mode, IExchangeClient exchangeClient, MarketInfo marketInfo,
        decimal price, CancellationToken cancellationToken, OrderType orderType = OrderType.Limit)
    {
        var balances = await exchangeClient.GetBalanceAsync(cancellationToken);
        var availableQuote = balances.FirstOrDefault(b => b.Asset == marketInfo.QuoteAsset)?.Available ?? 0m;

        var quoteAmount = TradingCalculations.ResolveTradeAmount(profile.TradeAmountMode, profile.TradeAmountValue, availableQuote);
        var roundedPrice = TradingCalculations.RoundPriceToTickSize(price, marketInfo.PricePrecision);
        var baseAmount = roundedPrice == 0m ? 0m : quoteAmount / roundedPrice;
        var roundedAmount = TradingCalculations.RoundAmountToStepSize(baseAmount, marketInfo.AmountPrecision);

        if (roundedAmount <= 0m || roundedAmount < marketInfo.MinOrderAmountInBaseAsset || quoteAmount < marketInfo.MinOrderAmountInQuoteAsset)
        {
            return new ExecutionResult(false, SkipReason.BelowMinimumOrderSize, null);
        }

        var riskCheck = await _riskEngine.CheckBeforePlacingOrderAsync(profile, mode, roundedAmount * roundedPrice, cancellationToken);
        if (!riskCheck.CanPlaceOrder)
        {
            return new ExecutionResult(false, SkipReason.RiskBlocked, null, riskCheck.Reason);
        }

        var request = new PlaceOrderRequest(marketInfo.Market, OrderSide.Buy, orderType, roundedAmount, orderType == OrderType.Limit ? roundedPrice : null, profile.Name, null);
        var order = await exchangeClient.PlaceOrderAsync(request, cancellationToken);
        return new ExecutionResult(true, SkipReason.None, order);
    }

    private async Task<ExecutionResult> PlaceSellAsync(
        BotProfile profile, TradingMode mode, IExchangeClient exchangeClient, MarketInfo marketInfo,
        StrategySignal signal, Ticker ticker, CancellationToken cancellationToken)
    {
        var position = await _positionRepository.GetOpenPositionAsync(mode, marketInfo.BaseAsset, profile.PapertradingProfileName, cancellationToken);
        if (position is null || !position.IsOpen)
        {
            return new ExecutionResult(false, SkipReason.NoOpenPosition, null);
        }

        var price = signal.SuggestedPrice ?? TradingCalculations.SellLimitPrice(position.AverageEntryPrice, profile.SellMarginPercentage);
        var roundedPrice = TradingCalculations.RoundPriceToTickSize(price, marketInfo.PricePrecision);
        var roundedAmount = TradingCalculations.RoundAmountToStepSize(position.Amount, marketInfo.AmountPrecision);

        if (roundedAmount < marketInfo.MinOrderAmountInBaseAsset)
        {
            return new ExecutionResult(false, SkipReason.BelowMinimumOrderSize, null);
        }

        var request = new PlaceOrderRequest(
            marketInfo.Market, OrderSide.Sell, signal.OrderType, roundedAmount,
            signal.OrderType == OrderType.Limit ? roundedPrice : null, profile.Name, null);
        var order = await exchangeClient.PlaceOrderAsync(request, cancellationToken);
        return new ExecutionResult(true, SkipReason.None, order);
    }

    private static async Task<ExecutionResult> CancelOpenOrdersAsync(IExchangeClient exchangeClient, string market, CancellationToken cancellationToken)
    {
        await exchangeClient.CancelAllOrdersAsync(market, cancellationToken);
        return new ExecutionResult(true, SkipReason.None, null);
    }
}
