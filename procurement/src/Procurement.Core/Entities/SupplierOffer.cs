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

        /// <summary>
        /// Combination offer (user request, 18 nov 2026: "2 OriginalReels te kopen van 600 stuks en 1
        /// reReel van 60 stuks"): SupplierPartNumber/PackagingQuantity are the full reels, these four
        /// the rest from another variant of the same supplier (e.g. Digi-Reel). OfferedQuantity,
        /// TotalPrice and LandedCost cover both parts; UnitPrice is the weighted average. Null/0 on a
        /// normal offer.
        /// </summary>
        public string RemainderSupplierPartNumber { get; set; }
        public int RemainderQuantity { get; set; }
        public decimal RemainderUnitPrice { get; set; }
        public PackagingType? RemainderPackagingType { get; set; }

        /// <summary>Not mapped. True for a full-reels-plus-rest combination.</summary>
        public bool IsCombination => !string.IsNullOrEmpty(RemainderSupplierPartNumber) && RemainderQuantity > 0;

        /// <summary>Not mapped. Full-reel part of a combination (OfferedQuantity otherwise).</summary>
        public int MainQuantity => IsCombination ? OfferedQuantity - RemainderQuantity : OfferedQuantity;

        /// <summary>Unit price of the full-reel part of a combination (0 on a normal offer).</summary>
        public decimal MainUnitPrice { get; set; }

        /// <summary>Not mapped. Shown in the offers grid, e.g. "2× 600 OriginalReel + 60 ReReel".</summary>
        public string Composition => IsCombination
            ? $"{(PackagingQuantity > 0 ? MainQuantity / PackagingQuantity : 0)}× {PackagingQuantity} {PackagingType} + {RemainderQuantity} {RemainderPackagingType}"
            : string.Empty;

        /// <summary>
        /// Sourcing per article (user decision, 18 nov 2026): offers of lines of the same article that
        /// were sourced together share this key (null for a line sourced on its own). Quantities and
        /// prices are the group's; RequestedQuantity/AttritionQuantity are this line's own. Choosing,
        /// approving and ordering one line of the bundle does the same for the others.
        /// </summary>
        public string BundleKey { get; set; }

        /// <summary>Number of lines in the bundle (0 when not bundled).</summary>
        public int BundleLineCount { get; set; }

        /// <summary>Total need of the bundle: sum of requested + attrition over its lines (0 when not bundled).</summary>
        public int BundleNeededQuantity { get; set; }

        /// <summary>Not mapped. Identifies "the same offer" across the copies of one bundle.</summary>
        public string VariantKey => $"{SupplierCode}|{SupplierPartNumber}|{RemainderSupplierPartNumber}|{PackagingType}";

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
