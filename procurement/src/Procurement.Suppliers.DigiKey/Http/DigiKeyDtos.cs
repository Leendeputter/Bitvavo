using System;
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

    // Everything below follows DigiKey's OrderStatus API v4 Swagger/OpenAPI spec exactly (supplied
    // directly by the user, same as Ordering — no guessing here either).
    // GET https://{host}/orderstatus/v4/salesorder/{salesOrderId}. Per the spec this endpoint "uses
    // both two-legged OAuth and three-legged OAuth", so DigiKeyAdapter.GetOrderStatusAsync calls it
    // through the existing 2-legged ISupplierHttpClient.GetAsync — the same client-credentials token
    // Product Information already uses, no 3-legged refresh-token flow needed for this one.
    // Deliberately separate DTOs from the Ordering ones above even where a name looks similar
    // (Address/Contact/LineItem) — OrderStatus v4's shapes for those are genuinely different fields,
    // not the same structure reused.

    internal class DigiKeySalesOrderDto
    {
        [JsonProperty("CustomerId")]
        public int? CustomerId { get; set; }

        [JsonProperty("Contact")]
        public DigiKeyOrderStatusContactDto Contact { get; set; }

        [JsonProperty("SalesOrderId")]
        public int SalesOrderId { get; set; }

        [JsonProperty("Status")]
        public DigiKeySalesOrderStatusInfoDto Status { get; set; }

        [JsonProperty("PurchaseOrder")]
        public string PurchaseOrder { get; set; }

        [JsonProperty("TotalPrice")]
        public double TotalPrice { get; set; }

        [JsonProperty("DateEntered")]
        public DateTime? DateEntered { get; set; }

        [JsonProperty("OrderNumber")]
        public long OrderNumber { get; set; }

        [JsonProperty("ShipMethod")]
        public string ShipMethod { get; set; }

        [JsonProperty("Currency")]
        public string Currency { get; set; }

        [JsonProperty("ShippingAddress")]
        public DigiKeyOrderStatusAddressDto ShippingAddress { get; set; }

        [JsonProperty("LineItems")]
        public List<DigiKeyOrderStatusLineItemDto> LineItems { get; set; }
    }

    internal class DigiKeyOrderStatusContactDto
    {
        [JsonProperty("FirstName")]
        public string FirstName { get; set; }

        [JsonProperty("LastName")]
        public string LastName { get; set; }

        [JsonProperty("Email")]
        public string Email { get; set; }
    }

    internal class DigiKeySalesOrderStatusInfoDto
    {
        [JsonProperty("SalesOrderStatus")]
        public string SalesOrderStatus { get; set; }

        [JsonProperty("ShortDescription")]
        public string ShortDescription { get; set; }

        [JsonProperty("LongDescription")]
        public string LongDescription { get; set; }
    }

    internal class DigiKeyOrderStatusAddressDto
    {
        [JsonProperty("FirstName")]
        public string FirstName { get; set; }

        [JsonProperty("LastName")]
        public string LastName { get; set; }

        [JsonProperty("CompanyName")]
        public string CompanyName { get; set; }

        [JsonProperty("AddressLine1")]
        public string AddressLine1 { get; set; }

        [JsonProperty("AddressLine2")]
        public string AddressLine2 { get; set; }

        [JsonProperty("AddressLine3")]
        public string AddressLine3 { get; set; }

        [JsonProperty("City")]
        public string City { get; set; }

        [JsonProperty("State")]
        public string State { get; set; }

        [JsonProperty("County")]
        public string County { get; set; }

        [JsonProperty("ZipCode")]
        public string ZipCode { get; set; }

        [JsonProperty("IsoCode")]
        public string IsoCode { get; set; }

        [JsonProperty("Phone")]
        public string Phone { get; set; }

        [JsonProperty("InvoiceId")]
        public int? InvoiceId { get; set; }
    }

    internal class DigiKeyOrderStatusLineItemDto
    {
        [JsonProperty("SalesOrderId")]
        public int SalesOrderId { get; set; }

        [JsonProperty("DetailId")]
        public int DetailId { get; set; }

        [JsonProperty("TotalPrice")]
        public double TotalPrice { get; set; }

        [JsonProperty("PurchaseOrder")]
        public string PurchaseOrder { get; set; }

        [JsonProperty("CustomerReference")]
        public string CustomerReference { get; set; }

        [JsonProperty("CountryOfOrigin")]
        public string CountryOfOrigin { get; set; }

        [JsonProperty("DigiKeyProductNumber")]
        public string DigiKeyProductNumber { get; set; }

        [JsonProperty("ManufacturerProductNumber")]
        public string ManufacturerProductNumber { get; set; }

        [JsonProperty("Description")]
        public string Description { get; set; }

        [JsonProperty("PackType")]
        public string PackType { get; set; }

        [JsonProperty("QuantityInitialRequested")]
        public int QuantityInitialRequested { get; set; }

        // "QuantityOrdered will be the same as QuantityInitialRequested at order entry. If changes
        // are made to quantity it will be reflected in QuantityOrdered" (spec) — this is DigiKey's
        // closest equivalent to "confirmed quantity", used for that below.
        [JsonProperty("QuantityOrdered")]
        public int QuantityOrdered { get; set; }

        [JsonProperty("QuantityShipped")]
        public int QuantityShipped { get; set; }

        [JsonProperty("QuantityReserved")]
        public int QuantityReserved { get; set; }

        [JsonProperty("QuantityBackOrder")]
        public int QuantityBackOrder { get; set; }

        [JsonProperty("UnitPrice")]
        public double UnitPrice { get; set; }

        [JsonProperty("PoLineItemNumber")]
        public string PoLineItemNumber { get; set; }

        [JsonProperty("ItemShipments")]
        public List<DigiKeyItemShipInfoDto> ItemShipments { get; set; }

        [JsonProperty("Schedules")]
        public List<DigiKeyScheduleDto> Schedules { get; set; }
    }

    internal class DigiKeyItemShipInfoDto
    {
        [JsonProperty("QuantityShipped")]
        public int QuantityShipped { get; set; }

        [JsonProperty("InvoiceId")]
        public int InvoiceId { get; set; }

        [JsonProperty("ShippedDate")]
        public DateTime? ShippedDate { get; set; }

        [JsonProperty("TrackingNumber")]
        public string TrackingNumber { get; set; }

        [JsonProperty("ExpectedDeliveryDate")]
        public string ExpectedDeliveryDate { get; set; }
    }

    // "Date the shipment is scheduled to be delivered to the customer" (spec) — this is what
    // DigiKeyAdapter maps onto SupplierOrderStatusLine.EstimatedShipDate (despite the name
    // mismatch: it's a delivery date, not a ship-from-DigiKey date — ScheduledDate is the field
    // that matches EstimatedShipDate's actual use downstream, comparing against the requested due
    // date for a late-delivery check).
    internal class DigiKeyScheduleDto
    {
        [JsonProperty("QuantityScheduled")]
        public int QuantityScheduled { get; set; }

        [JsonProperty("ScheduledDate")]
        public DateTime? ScheduledDate { get; set; }

        [JsonProperty("DigiKeyReleaseDate")]
        public DateTime? DigiKeyReleaseDate { get; set; }
    }

    // Everything below follows DigiKey's Order Management API v1 Swagger/OpenAPI spec exactly
    // (supplied directly by the user, same batch as OrderStatus v4 above — no guessing here either).
    // GET https://{host}/OrderManagement/v1/SalesOrders/{salesOrderId}. Used as the *active*
    // GetOrderStatusAsync implementation instead of OrderStatus v4 above: OrderStatus v4 is fully
    // implemented and spec-correct (see DigiKeySalesOrderDto etc.), but as of oct 2026 returns a 403
    // "client credentials are not authorized" against the sandbox despite showing Enabled in
    // DigiKey's portal — the same portal/backend-desync symptom seen earlier for Product Information
    // V4, which DigiKey fixed on their end after being contacted with the correlationId. Order
    // Management uses oauth2AccessCodeSecurity (3-legged, same as Ordering v3) rather than
    // OrderStatus v4's 2-legged-or-3-legged — and since Ordering's 3-legged credentials are already
    // proven working (a real sandbox order was placed with them), this path works today. Trade-off:
    // SalesOrderDetailView has no price field at all (no price-deviation detection possible via this
    // API) and no explicit order-status enum (PurchaseOrderStatus is inferred from
    // Quantity/QuantityShipped/QuantityBackorder instead of DigiKey's own, much richer status code) —
    // switch GetOrderStatusAsync back to the OrderStatus v4 path once DigiKey resolves the 403.

    internal class DigiKeySalesOrderViewDto
    {
        [JsonProperty("Currency")]
        public string Currency { get; set; }

        [JsonProperty("SalesOrderId")]
        public int SalesOrderId { get; set; }

        [JsonProperty("CustomerId")]
        public int CustomerId { get; set; }

        [JsonProperty("PurchaseOrderNumber")]
        public string PurchaseOrderNumber { get; set; }

        [JsonProperty("ShipMethod")]
        public string ShipMethod { get; set; }

        [JsonProperty("Details")]
        public List<DigiKeySalesOrderDetailViewDto> Details { get; set; }
    }

    internal class DigiKeySalesOrderDetailViewDto
    {
        [JsonProperty("PoLineItemNumber")]
        public string PoLineItemNumber { get; set; }

        [JsonProperty("SalesOrderId")]
        public int SalesOrderId { get; set; }

        [JsonProperty("DetailId")]
        public int DetailId { get; set; }

        [JsonProperty("DigiKeyPartNumber")]
        public string DigiKeyPartNumber { get; set; }

        [JsonProperty("ManufacturerPartNumber")]
        public string ManufacturerPartNumber { get; set; }

        [JsonProperty("Quantity")]
        public int Quantity { get; set; }

        [JsonProperty("QuantityBackorder")]
        public int QuantityBackorder { get; set; }

        [JsonProperty("QuantityShipped")]
        public int QuantityShipped { get; set; }

        [JsonProperty("InvoiceId")]
        public long InvoiceId { get; set; }

        [JsonProperty("StandardPackSize")]
        public int StandardPackSize { get; set; }

        [JsonProperty("Schedules")]
        public List<DigiKeySalesOrderScheduleViewDto> Schedules { get; set; }
    }

    internal class DigiKeySalesOrderScheduleViewDto
    {
        // Note the field name: "ScheduleDate" here, not "ScheduledDate" like OrderStatus v4's
        // Schedule DTO above — genuinely different APIs, checked against the spec, not a typo.
        [JsonProperty("ScheduleDate")]
        public DateTime? ScheduleDate { get; set; }

        [JsonProperty("DetailId")]
        public int DetailId { get; set; }

        [JsonProperty("QuantityScheduled")]
        public int QuantityScheduled { get; set; }

        [JsonProperty("SalesOrderId")]
        public int SalesOrderId { get; set; }

        [JsonProperty("ScheduleIndex")]
        public int ScheduleIndex { get; set; }
    }
}
