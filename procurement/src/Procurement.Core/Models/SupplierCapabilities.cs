using Procurement.Core.Enums;

namespace Procurement.Core.Models
{
    public class SupplierCapabilities
    {
        public CapabilityStatus ProductSearch { get; set; }
        public CapabilityStatus Pricing { get; set; }
        public CapabilityStatus Availability { get; set; }
        public CapabilityStatus Packaging { get; set; }
        public CapabilityStatus Ordering { get; set; }
        public CapabilityStatus OrderStatus { get; set; }
        public CapabilityStatus Shipment { get; set; }
        public CapabilityStatus Tracking { get; set; }
        public CapabilityStatus Invoice { get; set; }
    }
}
