namespace BitvavoBot.Domain.Calculations;

/// <summary>
/// Pure, deterministic implementations of the calculation rules from functional spec 4.2.
/// All internal math uses <see cref="decimal"/> (28-29 significant digits), which comfortably
/// exceeds the required 8-decimal precision. Rounding to a market's tick/step size must only
/// happen just before an order is submitted (see <see cref="RoundToTickSize"/>), never earlier.
/// </summary>
public static class TradingCalculations
{
    /// <summary>Kooplimietprijs = Huidige bid × (1 − koopmarge%).</summary>
    public static decimal BuyLimitPrice(decimal currentBid, decimal buyMarginPercentage)
    {
        if (currentBid < 0m) throw new ArgumentOutOfRangeException(nameof(currentBid));
        return currentBid * (1m - buyMarginPercentage / 100m);
    }

    /// <summary>Verkooplimietprijs = Aankoopprijs × (1 + verkoopmarge%).</summary>
    public static decimal SellLimitPrice(decimal purchasePrice, decimal sellMarginPercentage)
    {
        if (purchasePrice < 0m) throw new ArgumentOutOfRangeException(nameof(purchasePrice));
        return purchasePrice * (1m + sellMarginPercentage / 100m);
    }

    /// <summary>% open = (aantal orders met status "open" / totaal geplaatste actieve orders binnen het profiel) × 100.</summary>
    public static decimal OpenOrdersPercentage(int openOrderCount, int totalActiveOrderCount)
    {
        if (openOrderCount < 0) throw new ArgumentOutOfRangeException(nameof(openOrderCount));
        if (totalActiveOrderCount < 0) throw new ArgumentOutOfRangeException(nameof(totalActiveOrderCount));
        if (totalActiveOrderCount == 0) return 0m;
        return (decimal)openOrderCount / totalActiveOrderCount * 100m;
    }

    /// <summary>Stop-loss trigger price for a long position: purchase price reduced by the stop-loss %.</summary>
    public static decimal StopLossPrice(decimal purchasePrice, decimal stopLossPercentage) =>
        purchasePrice * (1m - stopLossPercentage / 100m);

    /// <summary>Take-profit trigger price for a long position: purchase price increased by the take-profit %.</summary>
    public static decimal TakeProfitPrice(decimal purchasePrice, decimal takeProfitPercentage) =>
        purchasePrice * (1m + takeProfitPercentage / 100m);

    /// <summary>
    /// Rounds a price down/up to the market's price precision (decimal places). Must be applied
    /// just before sending an order, not during intermediate calculations.
    /// </summary>
    public static decimal RoundPriceToTickSize(decimal price, int pricePrecision) =>
        Math.Round(price, pricePrecision, MidpointRounding.ToZero);

    /// <summary>Rounds an order amount down to the market's amount precision (step size).</summary>
    public static decimal RoundAmountToStepSize(decimal amount, int amountPrecision) =>
        Math.Round(amount, amountPrecision, MidpointRounding.ToZero);

    /// <summary>Resolves the trade amount for a bot profile: either a fixed € amount or a % of the given balance.</summary>
    public static decimal ResolveTradeAmount(Enums.TradeAmountMode mode, decimal value, decimal availableBalance) =>
        mode == Enums.TradeAmountMode.FixedAmount
            ? value
            : availableBalance * (value / 100m);
}
