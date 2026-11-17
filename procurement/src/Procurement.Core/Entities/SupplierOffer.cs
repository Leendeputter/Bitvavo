using System;
using System.Collections.Generic;
using Procurement.Core.Enums;

namespace Procurement.Core.Entities
{
    public class SupplierOffer
    {
        public int Id { get; set; }
        public int PurchaseRequestLineId { get; set; }
        public virtual PurchaseRequestLine PurchaseRequestLine { get; set; }

        public string SupplierCode { get; set; }
        public string SupplierPartNumber { get; set; }
        public string Manufacturer { get; set; }
        public string ManufacturerPartNumber { get; set; }

        public int RequestedQuantity { get; set; }
        public int OfferedQuantity { get; set; }

        public decimal UnitPrice { get; set; }
        public string Currency { get; set; }
        public decimal TotalPrice { get; set; }

        public int MinimumOrderQuantity { get; set; }
        public int OrderMultiple { get; set; }

        public PackagingType PackagingType { get; set; }
        public int PackagingQuantity { get; set; }
        public ReelType? ReelType { get; set; }
        public decimal ReelingFee { get; set; }

        public int AvailableQuantity { get; set; }
        public int LeadTimeDays { get; set; }
        public DateTime EstimatedDeliveryDate { get; set; }

        public decimal ShippingCost { get; set; }
        public decimal Tax { get; set; }
        public decimal LandedCost { get; set; }

        /// <summary>Extra pieces for machine attrition included in OfferedQuantity (see AttritionRule).</summary>
        public int AttritionQuantity { get; set; }

        /// <summary>OfferedQuantity minus (RequestedQuantity + AttritionQuantity): what is left over after rounding to MOQ/reel.</summary>
        public int SurplusQuantity { get; set; }

        /// <summary>
        /// What the selection compares (17 nov 2026): LandedCost + surplus penalty
        /// (PackagingPolicy.SurplusPenaltyFactor × SurplusQuantity × unit cost). 0 on offers created
        /// before this existed; the selection falls back to LandedCost then.
        /// </summary>
        public decimal ComparisonCost { get; set; }

        public MatchConfidence MatchConfidence { get; set; }
        public DateTime OfferTimestamp { get; set; }
        public DateTime ExpiresAt { get; set; }
        public OfferStatus Status { get; set; }

        public virtual List<SupplierOfferPackagingOption> PackagingOptions { get; set; } = new List<SupplierOfferPackagingOption>();

        public SupplierOffer()
        {
            Currency = "EUR";
            OfferTimestamp = DateTime.UtcNow;
            Status = OfferStatus.Active;
        }
    }

    /// <summary>All packaging alternatives available for one offer (maps to "SupplierOfferPackaging" table).</summary>
    public class SupplierOfferPackagingOption
    {
        public int Id { get; set; }
        public int SupplierOfferId { get; set; }
        public virtual SupplierOffer SupplierOffer { get; set; }

        public PackagingType PackagingType { get; set; }
        public int PackagingQuantity { get; set; }
        public ReelType? ReelType { get; set; }
        public decimal ReelingFee { get; set; }
        public int AvailableQuantity { get; set; }
    }
}
