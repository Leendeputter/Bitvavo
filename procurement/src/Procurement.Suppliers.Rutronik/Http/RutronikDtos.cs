using System.Collections.Generic;
using Newtonsoft.Json;

namespace Procurement.Suppliers.Rutronik.Http
{
    /// <summary>
    /// One search hit from GET /api/search/?apikey=...&amp;searchterm=... (array response, confirmed
    /// field names and native JSON types — real sample JSON pasted by the user, 22 okt 2026, not
    /// placeholders). "packaging" is a free-text enum (TRAY / BULK / REEL / TUBE per the doc).
    /// </summary>
    public class RutronikSearchResultDto
    {
        [JsonProperty("sku")] public string Sku { get; set; }
        [JsonProperty("manufacturer")] public string Manufacturer { get; set; }
        [JsonProperty("mpn")] public string Mpn { get; set; }
        [JsonProperty("matchcode")] public string Matchcode { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("package")] public string Package { get; set; }
        [JsonProperty("packaging")] public string Packaging { get; set; }
        [JsonProperty("url")] public string Url { get; set; }
        [JsonProperty("pu")] public int Pu { get; set; }
        [JsonProperty("moq")] public int Moq { get; set; }
        [JsonProperty("stock")] public int Stock { get; set; }
        [JsonProperty("leadtime")] public int LeadTime { get; set; }
        [JsonProperty("currency")] public string Currency { get; set; }
        [JsonProperty("price")] public decimal Price { get; set; }
        [JsonProperty("pricebreaks")] public List<RutronikPriceBreakDto> PriceBreaks { get; set; }
    }

    /// <summary>GET /api/pricerequest/?apikey=...&amp;sku=... (or manufacturer+mpn) returns one object, not an array — same field names/types as RutronikSearchResultDto.</summary>
    public class RutronikPriceRequestResultDto
    {
        [JsonProperty("sku")] public string Sku { get; set; }
        [JsonProperty("manufacturer")] public string Manufacturer { get; set; }
        [JsonProperty("mpn")] public string Mpn { get; set; }
        [JsonProperty("matchcode")] public string Matchcode { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("package")] public string Package { get; set; }
        [JsonProperty("packaging")] public string Packaging { get; set; }
        [JsonProperty("url")] public string Url { get; set; }
        [JsonProperty("pu")] public int Pu { get; set; }
        [JsonProperty("moq")] public int Moq { get; set; }
        [JsonProperty("stock")] public int Stock { get; set; }
        [JsonProperty("leadtime")] public int LeadTime { get; set; }
        [JsonProperty("currency")] public string Currency { get; set; }
        [JsonProperty("price")] public decimal Price { get; set; }
        [JsonProperty("pricebreaks")] public List<RutronikPriceBreakDto> PriceBreaks { get; set; }
    }

    /// <summary>
    /// GET /api/article/?apikey=...&amp;mpn=... — "designed for search engines", exact-mpn match only.
    /// Its real sample response (pasted by the user) shows "price" and pricebreaks[].price/quantity
    /// as QUOTED STRINGS (e.g. "price": "0.00179"), unlike Search/Pricerequest's native numbers — a
    /// confirmed type inconsistency between this endpoint and the other two, not a guess. Also
    /// carries one extra field the other two don't: "pipeline" (e.g. "In Stock").
    /// </summary>
    public class RutronikArticleResultDto
    {
        [JsonProperty("sku")] public string Sku { get; set; }
        [JsonProperty("manufacturer")] public string Manufacturer { get; set; }
        [JsonProperty("mpn")] public string Mpn { get; set; }
        [JsonProperty("matchcode")] public string Matchcode { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("package")] public string Package { get; set; }
        [JsonProperty("packaging")] public string Packaging { get; set; }
        [JsonProperty("url")] public string Url { get; set; }
        [JsonProperty("pu")] public int Pu { get; set; }
        [JsonProperty("moq")] public int Moq { get; set; }
        [JsonProperty("stock")] public int Stock { get; set; }
        [JsonProperty("leadtime")] public int LeadTime { get; set; }
        [JsonProperty("currency")] public string Currency { get; set; }
        [JsonProperty("pipeline")] public string Pipeline { get; set; }
        [JsonProperty("price")] public string Price { get; set; }
        [JsonProperty("pricebreaks")] public List<RutronikArticlePriceBreakDto> PriceBreaks { get; set; }
    }

    public class RutronikPriceBreakDto
    {
        [JsonProperty("quantity")] public int Quantity { get; set; }
        [JsonProperty("price")] public decimal Price { get; set; }
    }

    /// <summary>Same shape as RutronikPriceBreakDto, but both fields are quoted strings in the Article endpoint's real sample response — see RutronikArticleResultDto's doc comment.</summary>
    public class RutronikArticlePriceBreakDto
    {
        [JsonProperty("quantity")] public string Quantity { get; set; }
        [JsonProperty("price")] public string Price { get; set; }
    }
}
