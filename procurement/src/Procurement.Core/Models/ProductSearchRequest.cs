namespace Procurement.Core.Models
{
    public class ProductSearchRequest
    {
        public string Manufacturer { get; set; }
        public string ManufacturerPartNumber { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
    }
}
