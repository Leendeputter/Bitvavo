using BitvavoBot.Domain.Enums;

namespace BitvavoBot.Domain.Entities;

/// <summary>
/// A single fill (execution) resulting from an order, Live or Paper.
/// </summary>
public class Trade
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public TradingMode Mode { get; set; }
    public string Market { get; set; } = string.Empty;
    public OrderSide Side { get; set; }
    public decimal Price { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public string FeeCurrency { get; set; } = "EUR";

    /// <summary>Realized profit/loss in quote currency for closing (sell) trades; null for opening (buy) trades.</summary>
    public decimal? ProfitLoss { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
