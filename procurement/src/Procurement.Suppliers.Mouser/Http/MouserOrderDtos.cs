using System.Collections.Generic;
using Newtonsoft.Json;

namespace Procurement.Suppliers.Mouser.Http
{
    // Shapes below follow the Mouser Cart/Order API's confirmed Swagger 2.0 spec (pasted by the
    // user, 21 okt 2026 — api.mouser.com, same host/auth as the Search API in MouserDtos.cs).
    // Unlike that earlier file, this one has a real, machine-readable contract to work from rather
    // than a guess — field names below are taken directly from the spec's "definitions" section.

    internal class MouserCartItemRequestRoot
    {
        [JsonProperty("CartKey")]
        public string CartKey { get; set; }

        [JsonProperty("CartItems")]
        public List<MouserCartItemRequest> CartItems { get; set; } = new List<MouserCartItemRequest>();
    }

    internal class MouserCartItemRequest
    {
        [JsonProperty("MouserPartNumber")]
        public string MouserPartNumber { get; set; }

        [JsonProperty("Quantity")]
        public int Quantity { get; set; }

        [JsonProperty("CustomerPartNumber")]
        public string CustomerPartNumber { get; set; }
    }

    internal class MouserCartResponseRoot
    {
        [JsonProperty("Errors")]
        public List<MouserErrorDto> Errors { get; set; }

        [JsonProperty("CartKey")]
        public string CartKey { get; set; }

        [JsonProperty("CurrencyCode")]
        public string CurrencyCode { get; set; }

        [JsonProperty("MerchandiseTotal")]
        public decimal MerchandiseTotal { get; set; }
    }

    internal class MouserOrderInitializeRequestRoot
    {
        [JsonProperty("OrderInitialize")]
        public MouserOrderInitializeRequest OrderInitialize { get; set; }
    }

    internal class MouserOrderInitializeRequest
    {
        [JsonProperty("CurrencyCode")]
        public string CurrencyCode { get; set; }

        [JsonProperty("CartKey")]
        public string CartKey { get; set; }
    }

    internal class MouserOrderInitializeResponseRoot
    {
        [JsonProperty("OrderInitialize")]
        public MouserOrderInitializeResponse OrderInitialize { get; set; }
    }

    internal class MouserOrderInitializeResponse
    {
        [JsonProperty("CurrencyCode")]
        public string CurrencyCode { get; set; }

        [JsonProperty("Shipping")]
        public MouserShippingRoot Shipping { get; set; }

        [JsonProperty("Payment")]
        public MouserPaymentRoot Payment { get; set; }

        [JsonProperty("Errors")]
        public List<MouserErrorDto> Errors { get; set; }
    }

    internal class MouserShippingRoot
    {
        [JsonProperty("Methods")]
        public List<MouserShippingMethodDto> Methods { get; set; }
    }

    internal class MouserShippingMethodDto
    {
        [JsonProperty("Method")]
        public string Method { get; set; }

        [JsonProperty("Rate")]
        public decimal Rate { get; set; }

        [JsonProperty("Code")]
        public int Code { get; set; }
    }

    internal class MouserPaymentRoot
    {
        [JsonProperty("PaymentTypes")]
        public List<MouserPaymentTypeDto> PaymentTypes { get; set; }
    }

    internal class MouserPaymentTypeDto
    {
        [JsonProperty("Method")]
        public int Method { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }
    }

    internal class MouserOrderRequestRootType
    {
        [JsonProperty("Order")]
        public MouserOrderRequestType Order { get; set; }
    }

    internal class MouserOrderRequestType
    {
        [JsonProperty("PrimaryShipping")]
        public MouserShippingMethod PrimaryShipping { get; set; }

        [JsonProperty("Payment")]
        public MouserPaymentTypeRequest Payment { get; set; }

        [JsonProperty("CurrencyCode")]
        public string CurrencyCode { get; set; }

        [JsonProperty("CartKey")]
        public string CartKey { get; set; }

        /// <summary>
        /// Defaults to false per the spec — MUST be set true to actually place the order; false
        /// only returns a preview/summary. Easy to get wrong silently (an order that looks placed
        /// but never was), so CreateRealOrderAsync sets this explicitly rather than relying on
        /// whatever this DTO's own C# default would otherwise serialize as.
        /// </summary>
        [JsonProperty("SubmitOrder")]
        public bool SubmitOrder { get; set; }
    }

    internal class MouserShippingMethod
    {
        [JsonProperty("Code")]
        public int Code { get; set; }
    }

    internal class MouserPaymentTypeRequest
    {
        [JsonProperty("Method")]
        public int Method { get; set; }

        [JsonProperty("PoNumber")]
        public string PoNumber { get; set; }
    }

    internal class MouserOrderRoot
    {
        [JsonProperty("Order")]
        public MouserOrderResponse Order { get; set; }
    }

    internal class MouserOrderResponse
    {
        [JsonProperty("CurrencyCode")]
        public string CurrencyCode { get; set; }

        [JsonProperty("MerchandiseTotal")]
        public decimal MerchandiseTotal { get; set; }

        [JsonProperty("additionalFeesTotal")]
        public decimal AdditionalFeesTotal { get; set; }

        [JsonProperty("OrderTotal")]
        public decimal OrderTotal { get; set; }

        [JsonProperty("OrderID")]
        public string OrderId { get; set; }

        [JsonProperty("Errors")]
        public List<MouserErrorDto> Errors { get; set; }

        [JsonProperty("SubmitOrder")]
        public bool SubmitOrder { get; set; }
    }

    /// <summary>GET /orderhistory/salesOrderNumber — used by GetOrderStatusAsync.</summary>
    internal class MouserOrderDetailDto
    {
        [JsonProperty("SalesOrderId")]
        public string SalesOrderId { get; set; }

        [JsonProperty("OrderStatus")]
        public int OrderStatus { get; set; }

        /// <summary>
        /// A human-readable label (e.g. "Processing"/"Shipped") — the numeric OrderStatus code's
        /// own meaning isn't documented anywhere in the spec, so GetOrderStatusAsync maps this
        /// string instead, best-effort (case-insensitive keyword match against our own
        /// PurchaseOrderStatus names), falling back to Acknowledged for anything unrecognized.
        /// Re-check against a real response once available.
        /// </summary>
        [JsonProperty("OrderStatusName")]
        public string OrderStatusName { get; set; }

        [JsonProperty("CurrencyCode")]
        public string CurrencyCode { get; set; }

        [JsonProperty("OrderLines")]
        public List<MouserOrderLineItemDto> OrderLines { get; set; }
    }

    internal class MouserOrderLineItemDto
    {
        [JsonProperty("Quantity")]
        public int Quantity { get; set; }

        [JsonProperty("UnitPrice")]
        public decimal UnitPrice { get; set; }

        [JsonProperty("ProductInfo")]
        public MouserOrderLineProductDto ProductInfo { get; set; }
    }

    internal class MouserOrderLineProductDto
    {
        [JsonProperty("MouserPartNumber")]
        public string MouserPartNumber { get; set; }
    }
}
