using System.Collections.Generic;
using Newtonsoft.Json;

namespace Procurement.Suppliers.Farnell.Http
{
    // Shapes below follow element14/Farnell's publicly documented Product Search API
    // (api.element14.com/catalog/products, "premierFarnellPartNumberReturn" response wrapper).
    // Not yet verified against a real account response — FarnellAdapter's real-mode parsing
    // should be re-checked against actual JSON the first time a real API key is available, and
    // these DTOs adjusted if any field name is off.

    internal class FarnellSearchResponse
    {
        [JsonProperty("premierFarnellPartNumberReturn")]
        public FarnellResultDto Result { get; set; }

        [JsonProperty("manufacturerPartNumberSearchReturn")]
        public FarnellResultDto ManufacturerPartNumberResult { get; set; }

        [JsonProperty("keywordSearchReturn")]
        public FarnellResultDto KeywordResult { get; set; }
    }

    internal class FarnellResultDto
    {
        [JsonProperty("numberOfResults")]
        public int NumberOfResults { get; set; }

        [JsonProperty("products")]
        public List<FarnellProductDto> Products { get; set; }
    }

    internal class FarnellProductDto
    {
        [JsonProperty("sku")]
        public string Sku { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("translatedManufacturerPartNumber")]
        public string ManufacturerPartNumber { get; set; }

        [JsonProperty("brandName")]
        public string BrandName { get; set; }

        [JsonProperty("prices")]
        public List<FarnellPriceDto> Prices { get; set; }

        [JsonProperty("stock")]
        public FarnellStockDto Stock { get; set; }

        [JsonProperty("datasheets")]
        public List<FarnellDatasheetDto> Datasheets { get; set; }

        [JsonProperty("translatedMinimumOrderQuality")]
        public string MinimumOrderQuantityRaw { get; set; }

        [JsonProperty("translatedMultipleQuality")]
        public string OrderMultipleRaw { get; set; }
    }

    internal class FarnellPriceDto
    {
        [JsonProperty("from")]
        public int From { get; set; }

        [JsonProperty("to")]
        public int To { get; set; }

        [JsonProperty("cost")]
        public decimal Cost { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }
    }

    internal class FarnellStockDto
    {
        [JsonProperty("level")]
        public int Level { get; set; }
    }

    internal class FarnellDatasheetDto
    {
        [JsonProperty("url")]
        public string Url { get; set; }
    }
}
