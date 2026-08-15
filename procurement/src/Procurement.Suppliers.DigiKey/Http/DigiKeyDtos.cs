using System.Collections.Generic;
using Newtonsoft.Json;

namespace Procurement.Suppliers.DigiKey.Http
{
    // Shapes below follow DigiKey's publicly documented V4 Product Information API
    // (api.digikey.com/products/v4/...). Not yet verified against a real sandbox response —
    // DigiKeyAdapter's real-mode parsing should be re-checked against actual JSON the first time
    // real credentials are available, and these DTOs adjusted if any field name is off.

    internal class DigiKeyTokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresInSeconds { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }
    }

    internal class DigiKeyKeywordSearchResponse
    {
        [JsonProperty("Products")]
        public List<DigiKeyProductDto> Products { get; set; }

        [JsonProperty("ProductsCount")]
        public int ProductsCount { get; set; }
    }

    internal class DigiKeyProductDetailsResponse
    {
        [JsonProperty("Product")]
        public DigiKeyProductDto Product { get; set; }
    }

    internal class DigiKeyProductDto
    {
        [JsonProperty("DigiKeyPartNumber")]
        public string DigiKeyPartNumber { get; set; }

        [JsonProperty("ManufacturerPartNumber")]
        public string ManufacturerPartNumber { get; set; }

        [JsonProperty("Manufacturer")]
        public DigiKeyValueDto Manufacturer { get; set; }

        [JsonProperty("ProductDescription")]
        public string ProductDescription { get; set; }

        [JsonProperty("DetailedDescription")]
        public string DetailedDescription { get; set; }

        [JsonProperty("DatasheetUrl")]
        public string DatasheetUrl { get; set; }

        [JsonProperty("UnitPrice")]
        public decimal UnitPrice { get; set; }

        [JsonProperty("QuantityAvailable")]
        public int QuantityAvailable { get; set; }

        [JsonProperty("ManufacturerLeadWeeks")]
        public string ManufacturerLeadWeeks { get; set; }

        [JsonProperty("StandardPricing")]
        public List<DigiKeyPriceBreakDto> StandardPricing { get; set; }

        [JsonProperty("ProductVariations")]
        public List<DigiKeyProductVariationDto> ProductVariations { get; set; }
    }

    internal class DigiKeyValueDto
    {
        [JsonProperty("Value")]
        public string Value { get; set; }
    }

    internal class DigiKeyPriceBreakDto
    {
        [JsonProperty("BreakQuantity")]
        public int BreakQuantity { get; set; }

        [JsonProperty("UnitPrice")]
        public decimal UnitPrice { get; set; }

        [JsonProperty("TotalPrice")]
        public decimal TotalPrice { get; set; }
    }

    internal class DigiKeyProductVariationDto
    {
        [JsonProperty("DigiKeyProductNumber")]
        public string DigiKeyProductNumber { get; set; }

        [JsonProperty("PackageType")]
        public DigiKeyValueDto PackageType { get; set; }

        [JsonProperty("StandardPackage")]
        public int StandardPackage { get; set; }

        [JsonProperty("MinimumOrderQuantity")]
        public int MinimumOrderQuantity { get; set; }

        [JsonProperty("QuantityAvailableforPackageType")]
        public int QuantityAvailableForPackageType { get; set; }

        [JsonProperty("StandardPricing")]
        public List<DigiKeyPriceBreakDto> StandardPricing { get; set; }
    }
}
