using System.Collections.Generic;
using Newtonsoft.Json;

namespace Procurement.Suppliers.Farnell.Http
{
    // Shapes below follow the Order API documentation the user pasted on 21 okt 2026 (from
    // partner.element14.com/order, which this session's egress proxy blocks — see README).
    // That page gives field names but, unlike the Product Search API's confirmed real responses,
    // no actual sample JSON for most of these — several numeric-looking fields (prices, totals,
    // the "True"/"False"-strings for shippingPreferredMethod) are documented only as
    // placeholder text. Declared as string here throughout, matching the one field the doc is
    // explicit about ("quantity... String"), and parsed defensively (TryParse) wherever used —
    // re-check against a real account's actual response the first time one is available.

    internal class FarnellAuthTokenResponse
    {
        [JsonProperty("Authorization")]
        public string Authorization { get; set; }
    }

    internal class FarnellAddCartItemRequest
    {
        [JsonProperty("orderItem")]
        public List<FarnellCartItemRequest> OrderItem { get; set; } = new List<FarnellCartItemRequest>();
    }

    internal class FarnellCartItemRequest
    {
        [JsonProperty("quantity")]
        public string Quantity { get; set; }

        [JsonProperty("sku")]
        public string Sku { get; set; }

        [JsonProperty("customerPartNumber")]
        public string CustomerPartNumber { get; set; }

        [JsonProperty("lineNote")]
        public string LineNote { get; set; }
    }

    internal class FarnellCartIdResponse
    {
        [JsonProperty("orderId")]
        public string OrderId { get; set; }
    }

    internal class FarnellShippingAddressListResponse
    {
        [JsonProperty("address")]
        public List<FarnellShippingAddressDto> Address { get; set; }
    }

    internal class FarnellShippingAddressDto
    {
        [JsonProperty("addressId")]
        public string AddressId { get; set; }

        [JsonProperty("nickName")]
        public string NickName { get; set; }

        [JsonProperty("organizationName")]
        public string OrganizationName { get; set; }

        [JsonProperty("attention")]
        public string Attention { get; set; }

        [JsonProperty("addressLine1")]
        public string AddressLine1 { get; set; }

        [JsonProperty("addressLine2")]
        public string AddressLine2 { get; set; }

        [JsonProperty("addressLine3")]
        public string AddressLine3 { get; set; }

        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("postalCode")]
        public string PostalCode { get; set; }

        [JsonProperty("region")]
        public string Region { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }
    }

    internal class FarnellConfirmAddressRequest
    {
        [JsonProperty("addressId")]
        public string AddressId { get; set; }
    }

    internal class FarnellShippingMethodsResponse
    {
        [JsonProperty("usableShippingMode")]
        public List<FarnellShippingMethodDto> UsableShippingMode { get; set; }

        [JsonProperty("requireShippingMethod")]
        public string RequireShippingMethod { get; set; }
    }

    internal class FarnellShippingMethodDto
    {
        [JsonProperty("shippingMethodID")]
        public string ShippingMethodId { get; set; }

        [JsonProperty("shippingMethodDesc")]
        public string ShippingMethodDesc { get; set; }

        [JsonProperty("shippingMethodPrice")]
        public string ShippingMethodPrice { get; set; }

        [JsonProperty("shippingPreferredMethod")]
        public string ShippingPreferredMethod { get; set; }
    }

    internal class FarnellConfirmShippingMethodRequest
    {
        [JsonProperty("shippingMethodID")]
        public string ShippingMethodId { get; set; }
    }

    internal class FarnellOrderReviewResponse
    {
        [JsonProperty("grandTotal")]
        public string GrandTotal { get; set; }

        [JsonProperty("productsTotal")]
        public string ProductsTotal { get; set; }
    }

    internal class FarnellOrderSubmitRequest
    {
        [JsonProperty("poNumberId")]
        public string PoNumberId { get; set; }
    }

    internal class FarnellOrderSubmitResponse
    {
        [JsonProperty("orderReference")]
        public string OrderReference { get; set; }
    }
}
