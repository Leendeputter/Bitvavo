using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;

namespace BitvavoBot.Trading.Strategies;

public interface IStrategyFactory
{
    IStrategy Create(StrategyType type);
}

public sealed class StrategyFactory : IStrategyFactory
{
    public IStrategy Create(StrategyType type) => type switch
    {
        StrategyType.GridTrading => new GridTradingStrategy(),
        StrategyType.MarketMaking => new MarketMakingStrategy(),
        StrategyType.MeanReversion => new MeanReversionStrategy(),
        StrategyType.MovingAverage => new MovingAverageStrategy(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown strategy type.")
    };
}
