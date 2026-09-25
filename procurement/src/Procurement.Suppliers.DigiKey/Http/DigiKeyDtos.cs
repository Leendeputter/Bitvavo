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

        // Only present for the 3-legged (Authorization Code) flow used by Ordering — the 2-legged
        // client-credentials flow used for search/pricing never returns one, so this stays null
        // there, harmlessly (GetAccessTokenAsync's caller never reads it).
        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }

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

    // Everything below follows DigiKey's Ordering API v3 Swagger/OpenAPI spec exactly (supplied
    // directly by the user, unlike the Product Information shapes above — no guessing here).
    // POST https://{host}/Ordering/v3/Orders, 3-legged OAuth (DigiKeyHttpClientWrapper.PostOrderAsync).

    internal class DigiKeyOrderRequestDto
    {
        [JsonProperty("PurchaseOrderNumber")]
        public string PurchaseOrderNumber { get; set; }

        [JsonProperty("Currency")]
        public string Currency { get; set; }

        [JsonProperty("BuyerContact")]
        public DigiKeyContactDto BuyerContact { get; set; }

        [JsonProperty("ShippingContact")]
        public DigiKeyContactDto ShippingContact { get; set; }

        [JsonProperty("LineItems")]
        public List<DigiKeyLineItemDto> LineItems { get; set; }
    }

    // BuyerContact and ShippingContact are separate properties in OrderRequest, but identically
    // shaped in the spec (both "required": ["Address", "Name"], same fields) — one DTO for both.
    internal class DigiKeyContactDto
    {
        [JsonProperty("CustomerId")]
        public string CustomerId { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("Address")]
        public DigiKeyAddressDto Address { get; set; }

        [JsonProperty("Telephone")]
        public string Telephone { get; set; }
    }

    internal class DigiKeyAddressDto
    {
        [JsonProperty("Company")]
        public string Company { get; set; }

        [JsonProperty("FirstName")]
        public string FirstName { get; set; }

        [JsonProperty("LastName")]
        public string LastName { get; set; }

        [JsonProperty("Email")]
        public string Email { get; set; }

        [JsonProperty("AddressLineOne")]
        public string AddressLineOne { get; set; }

        [JsonProperty("AddressLineTwo")]
        public string AddressLineTwo { get; set; }

        [JsonProperty("City")]
        public string City { get; set; }

        [JsonProperty("Province")]
        public string Province { get; set; }

        [JsonProperty("PostalCode")]
        public string PostalCode { get; set; }

        [JsonProperty("Country")]
        public string Country { get; set; }
    }

    internal class DigiKeyLineItemDto
    {
        [JsonProperty("DigiKeyPartNumber")]
        public string DigiKeyPartNumber { get; set; }

        [JsonProperty("RequestedQuantity")]
        public int RequestedQuantity { get; set; }

        [JsonProperty("UnitPrice")]
        public double UnitPrice { get; set; }
    }

    internal class DigiKeyOrderResponseDto
    {
        [JsonProperty("Message")]
        public string Message { get; set; }

        // Three cases per the spec: a real order number; "0" if the order is entirely fulfilled by
        // DigiKey Marketplace partners (see SalesOrderIds_DKPlus instead); "-1" if DigiKey received
        // the request but hadn't minted a SalesOrderId yet when it had to respond — explicitly *not*
        // an error, the order still gets processed and must not be resubmitted.
        [JsonProperty("SalesOrderId")]
        public long SalesOrderId { get; set; }

        [JsonProperty("PurchaseOrderNumber")]
        public string PurchaseOrderNumber { get; set; }

        [JsonProperty("SalesOrderIds_DKPlus")]
        public List<long> SalesOrderIdsDkPlus { get; set; }
    }

    internal class DigiKeyApiErrorResponseDto
    {
        [JsonProperty("StatusCode")]
        public int StatusCode { get; set; }

        [JsonProperty("ErrorMessage")]
        public string ErrorMessage { get; set; }

        [JsonProperty("ErrorDetails")]
        public string ErrorDetails { get; set; }

        [JsonProperty("RequestId")]
        public string RequestId { get; set; }

        [JsonProperty("ValidationErrors")]
        public List<DigiKeyApiValidationErrorDto> ValidationErrors { get; set; }
    }

    internal class DigiKeyApiValidationErrorDto
    {
        [JsonProperty("Field")]
        public string Field { get; set; }

        [JsonProperty("Message")]
        public string Message { get; set; }
    }
}
