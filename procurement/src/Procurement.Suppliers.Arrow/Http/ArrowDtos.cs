using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Procurement.Suppliers.Arrow.Http
{
    /// <summary>Standard OAuth2 client_credentials token response — same shape as every other 2-legged flow in this app (DigiKey), not supplier-specific, so safe to assume even though Arrow's own doc doesn't show a sample body.</summary>
    public class ArrowTokenResponse
    {
        [JsonProperty("access_token")] public string AccessToken { get; set; }
        [JsonProperty("expires_in")] public int ExpiresInSeconds { get; set; }
        [JsonProperty("token_type")] public string TokenType { get; set; }
    }

    /// <summary>
    /// GET /priceandavail/parts (and the cpns/{cpn}/parts, parts/{docId} variants) response
    /// envelope — top-level field names ARE confirmed from the pasted spec. "pricingResponse"
    /// itself is deliberately left as a raw JArray/JObject: every example in the spec shows it as
    /// an empty "{}" placeholder, never a populated sample, so the per-part field names (price,
    /// stock, lead time, etc. — described only in prose, never named) are NOT confirmed. See
    /// ArrowAdapter's doc comment for why this app does not guess them.
    /// </summary>
    public class ArrowPriceAvailResponse
    {
        [JsonProperty("currentPage")] public int CurrentPage { get; set; }
        [JsonProperty("nextPageNumber")] public int NextPageNumber { get; set; }
        [JsonProperty("pages")] public int Pages { get; set; }
        [JsonProperty("previousPageNumber")] public int PreviousPageNumber { get; set; }
        [JsonProperty("pricingResponse")] public JArray PricingResponse { get; set; }
        [JsonProperty("requestedQuantity")] public string RequestedQuantity { get; set; }
        [JsonProperty("responseMessage")] public string ResponseMessage { get; set; }
        [JsonProperty("results")] public int Results { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("totalRecords")] public int TotalRecords { get; set; }
    }

    /// <summary>
    /// GET /orders/{orderId}/updatedStatus — a dedicated order-status lookup, distinct from the
    /// order-detail endpoint. Top-level fields are confirmed; "messages" and "orderLineItems" are
    /// left unmapped (raw JArray) since their item shape is, again, only shown as an empty "{}"
    /// placeholder in the spec.
    /// </summary>
    public class ArrowOrderStatusResponse
    {
        [JsonProperty("orderStatus")] public string OrderStatus { get; set; }
        [JsonProperty("orderId")] public string OrderId { get; set; }
        [JsonProperty("salesOrderNumber")] public string SalesOrderNumber { get; set; }
        [JsonProperty("messages")] public JArray Messages { get; set; }
        [JsonProperty("orderLineItems")] public JArray OrderLineItems { get; set; }
        [JsonProperty("origSysDocumentRef")] public string OrigSysDocumentRef { get; set; }
    }
}
