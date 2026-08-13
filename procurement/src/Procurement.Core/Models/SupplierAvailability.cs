using System;

namespace Procurement.Core.Models
{
    public class SupplierAvailability
    {
        public string SupplierPartNumber { get; set; }
        public int AvailableQuantity { get; set; }
        public int LeadTimeDays { get; set; }
        public DateTime EstimatedDeliveryDate { get; set; }
        public bool OnBackorder { get; set; }
    }
}
