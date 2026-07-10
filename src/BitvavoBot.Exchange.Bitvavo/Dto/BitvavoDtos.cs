using System.Text.Json.Serialization;

namespace BitvavoBot.Exchange.Bitvavo.Dto;

public sealed class MarketDto
{
    [JsonPropertyName("market")] public string Market { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("base")] public string Base { get; set; } = string.Empty;
    [JsonPropertyName("quote")] public string Quote { get; set; } = string.Empty;
    [JsonPropertyName("pricePrecision")] public int PricePrecision { get; set; }
    [JsonPropertyName("minOrderInBaseAsset")] public string? MinOrderInBaseAsset { get; set; }
    [JsonPropertyName("minOrderInQuoteAsset")] public string? MinOrderInQuoteAsset { get; set; }
}

public sealed class Ticker24hDto
{
    [JsonPropertyName("market")] public string Market { get; set; } = string.Empty;
    [JsonPropertyName("last")] public string? Last { get; set; }
    [JsonPropertyName("bid")] public string? Bid { get; set; }
    [JsonPropertyName("ask")] public string? Ask { get; set; }
    [JsonPropertyName("volume")] public string? Volume { get; set; }
    [JsonPropertyName("high")] public string? High { get; set; }
    [JsonPropertyName("low")] public string? Low { get; set; }
    [JsonPropertyName("open")] public string? Open { get; set; }
    [JsonPropertyName("timestamp")] public long Timestamp { get; set; }
}

public sealed class BalanceDto
{
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = string.Empty;
    [JsonPropertyName("available")] public string Available { get; set; } = "0";
    [JsonPropertyName("inOrder")] public string InOrder { get; set; } = "0";
}

public sealed class OrderDto
{
    [JsonPropertyName("orderId")] public string OrderId { get; set; } = string.Empty;
    [JsonPropertyName("market")] public string Market { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("side")] public string Side { get; set; } = string.Empty;
    [JsonPropertyName("orderType")] public string OrderType { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public string? Amount { get; set; }
    [JsonPropertyName("amountRemaining")] public string? AmountRemaining { get; set; }
    [JsonPropertyName("price")] public string? Price { get; set; }
    [JsonPropertyName("filledAmount")] public string? FilledAmount { get; set; }
    [JsonPropertyName("feePaid")] public string? FeePaid { get; set; }
    [JsonPropertyName("feeCurrency")] public string? FeeCurrency { get; set; }
}

public sealed class PlaceOrderBody
{
    [JsonPropertyName("market")] public string Market { get; set; } = string.Empty;
    [JsonPropertyName("side")] public string Side { get; set; } = string.Empty;
    [JsonPropertyName("orderType")] public string OrderType { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public string? Amount { get; set; }
    [JsonPropertyName("price")] public string? Price { get; set; }
}

public sealed class AccountFeesDto
{
    [JsonPropertyName("fees")] public AccountFeeDetailDto Fees { get; set; } = new();
}

public sealed class AccountFeeDetailDto
{
    [JsonPropertyName("taker")] public string Taker { get; set; } = "0";
    [JsonPropertyName("maker")] public string Maker { get; set; } = "0";
}

public sealed class OrderBookDto
{
    [JsonPropertyName("market")] public string Market { get; set; } = string.Empty;
    [JsonPropertyName("bids")] public List<List<string>> Bids { get; set; } = new();
    [JsonPropertyName("asks")] public List<List<string>> Asks { get; set; } = new();
}

public sealed class ErrorDto
{
    [JsonPropertyName("errorCode")] public int ErrorCode { get; set; }
    [JsonPropertyName("error")] public string Error { get; set; } = string.Empty;
}
