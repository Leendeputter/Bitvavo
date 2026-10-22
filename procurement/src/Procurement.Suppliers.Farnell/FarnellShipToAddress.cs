namespace Procurement.Suppliers.Farnell
{
    /// <summary>
    /// One entry from Farnell's Order API "Retrieve Shipping Address" response — already-saved
    /// addresses on the trade account, picked by AddressId rather than submitted as free text
    /// (unlike DigiKey's BuyerContact/ShippingContact). Exposed publicly (not an internal Http DTO)
    /// so Procurement.UI can show a picker without referencing the Http namespace's JSON types.
    /// </summary>
    public class FarnellShipToAddress
    {
        public string AddressId { get; set; }
        public string NickName { get; set; }
        public string OrganizationName { get; set; }
        public string Attention { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string AddressLine3 { get; set; }
        public string City { get; set; }
        public string PostalCode { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
    }
}
