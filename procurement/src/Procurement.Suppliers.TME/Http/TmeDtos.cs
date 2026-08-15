using System.Collections.Generic;
using Newtonsoft.Json;

namespace Procurement.Suppliers.TME.Http
{
    // Shapes below follow TME's publicly documented REST API (api.tme.eu, Products/Search.json,
    // Products/GetProducts.json, Products/GetPrices.json). This is the least-verified DTO set of
    // any supplier in this project — TME's HMAC-SHA1 request signing scheme is reproduced here
    // from memory of the general shape of their docs, not a real tested call. Re-check every field
    // name and the signing scheme itself against TME's actual API documentation and a real
    // sandbox response before relying on this.

    internal class TmeEnvelope<TData>
    {
        [JsonProperty("Status")]
        public string Status { get; set; }

        [JsonProperty("Data")]
        public TData Data { get; set; }

        [JsonProperty("Error")]
        public TmeErrorDto Error { get; set; }
    }

    internal class TmeErrorDto
    {
        [JsonProperty("Message")]
        public string Message { get; set; }

        [JsonProperty("Code")]
        public string Code { get; set; }
    }

    internal class TmeSearchData
    {
        [JsonProperty("ProductList")]
        public List<TmeSearchProductDto> ProductList { get; set; }
    }

    internal class TmeSearchProductDto
    {
        [JsonProperty("Symbol")]
        public string Symbol { get; set; }

        [JsonProperty("OriginalSymbol")]
        public string OriginalSymbol { get; set; }

        [JsonProperty("Producer")]
        public string Producer { get; set; }

        [JsonProperty("Description")]
        public string Description { get; set; }
    }

    internal class TmeProductsData
    {
        [JsonProperty("ProductList")]
        public List<TmeProductDetailDto> ProductList { get; set; }
    }

    internal class TmeProductDetailDto
    {
        [JsonProperty("Symbol")]
        public string Symbol { get; set; }

        [JsonProperty("OriginalSymbol")]
        public string OriginalSymbol { get; set; }

        [JsonProperty("Producer")]
        public string Producer { get; set; }

        [JsonProperty("Description")]
        public string Description { get; set; }

        [JsonProperty("InStock")]
        public int InStock { get; set; }

        [JsonProperty("MinAmount")]
        public int MinAmount { get; set; }

        [JsonProperty("Multiplier")]
        public int Multiplier { get; set; }

        [JsonProperty("Photo")]
        public string Photo { get; set; }
    }

    internal class TmePricesData
    {
        [JsonProperty("ProductList")]
        public List<TmePriceProductDto> ProductList { get; set; }
    }

    internal class TmePriceProductDto
    {
        [JsonProperty("Symbol")]
        public string Symbol { get; set; }

        [JsonProperty("PriceList")]
        public List<TmePriceBreakDto> PriceList { get; set; }
    }

    internal class TmePriceBreakDto
    {
        [JsonProperty("Amount")]
        public int Amount { get; set; }

        [JsonProperty("PriceValue")]
        public decimal PriceValue { get; set; }
    }
}
