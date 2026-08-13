using System.Collections.Generic;

namespace Procurement.Core.Models
{
    public class SupplierPricing
    {
        public string SupplierPartNumber { get; set; }
        public int RequestedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; }
        public int MinimumOrderQuantity { get; set; }
        public int OrderMultiple { get; set; }
        public List<PriceBreak> PriceBreaks { get; set; } = new List<PriceBreak>();
    }

    public class PriceBreak
    {
        public int BreakQuantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
