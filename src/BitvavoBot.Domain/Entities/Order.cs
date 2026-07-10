using BitvavoBot.Domain.Enums;

namespace BitvavoBot.Domain.Entities;

/// <summary>
/// An order placed by the bot or manually, either sent to Bitvavo (Live) or simulated (Paper).
/// </summary>
public class Order
{
    public long Id { get; set; }

    /// <summary>Exchange order id (Live) or generated simulation id (Paper).</summary>
    public string ExternalId { get; set; } = string.Empty;

    public TradingMode Mode { get; set; }
    public string Market { get; set; } = string.Empty;
    public OrderSide Side { get; set; }
    public OrderType Type { get; set; }
    public OrderStatus Status { get; set; }

    /// <summary>Limit price; null for market orders.</summary>
    public decimal? Price { get; set; }
    public decimal Amount { get; set; }
    public decimal FilledAmount { get; set; }
    public decimal? AverageFillPrice { get; set; }
    public decimal FeePaid { get; set; }
    public string FeeCurrency { get; set; } = "EUR";

    /// <summary>Name of the bot profile that placed this order, empty for manual orders.</summary>
    public string BotProfileName { get; set; } = string.Empty;
    public string? StrategyName { get; set; }

    /// <summary>
    /// Set only when Mode = Paper: the Papertrading profile (virtual account) this order belongs
    /// to, so multiple Papertrading profiles never mix orders/history (functional spec 8.3).
    /// </summary>
    public string? PapertradingProfileName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? FilledAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public bool IsOpen => Status is OrderStatus.New or OrderStatus.Open or OrderStatus.PartiallyFilled;
}
