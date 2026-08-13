using Procurement.Core.Enums;

namespace Procurement.Core.Models
{
    public class SupplierPackagingOption
    {
        public string SupplierPartNumber { get; set; }
        public PackagingType PackagingType { get; set; }
        public int PackagingQuantity { get; set; }
        public ReelType? ReelType { get; set; }
        public decimal ReelingFee { get; set; }
        public int AvailableQuantity { get; set; }
    }
}
