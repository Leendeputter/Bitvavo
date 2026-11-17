using System.Collections.Generic;
using Procurement.Core.Enums;

namespace Procurement.Core.Models
{
    /// <summary>
    /// One packaging variant of a part at a supplier. Since 17 nov 2026 every allowed variant becomes
    /// its own offer, so a variant carries its own part number and, where the supplier gives them,
    /// its own price breaks and MOQ (e.g. DigiKey's CT / TR / Digi-Reel numbers).
    /// </summary>
    public class SupplierPackagingOption
    {
        public string SupplierPartNumber { get; set; }
        public PackagingType PackagingType { get; set; }
        public int PackagingQuantity { get; set; }
        public ReelType? ReelType { get; set; }
        public decimal ReelingFee { get; set; }
        public int AvailableQuantity { get; set; }

        /// <summary>0 = unknown; the part's general pricing MOQ is used then.</summary>
        public int MinimumOrderQuantity { get; set; }

        /// <summary>Null or empty = unknown; the part's general pricing call is used then.</summary>
        public List<PriceBreak> PriceBreaks { get; set; }
    }
}
