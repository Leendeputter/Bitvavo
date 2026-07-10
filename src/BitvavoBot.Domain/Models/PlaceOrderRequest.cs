using BitvavoBot.Domain.Enums;

namespace BitvavoBot.Domain.Models;

public sealed record PlaceOrderRequest(
    string Market,
    OrderSide Side,
    OrderType Type,
    decimal Amount,
    decimal? LimitPrice,
    string BotProfileName,
    string? StrategyName);
