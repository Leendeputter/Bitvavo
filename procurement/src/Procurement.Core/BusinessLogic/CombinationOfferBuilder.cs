using System;
using System.Collections.Generic;
using System.Linq;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Procurement.Core.Models;

namespace Procurement.Core.BusinessLogic
{
    /// <summary>
    /// Full reels plus the rest from another packaging variant of the same supplier (user request,
    /// 18 nov 2026: "In dit geval zou het het voordeligst zijn om 2 OriginalReels te kopen van 600
    /// stuks en 1 reReel van 60 stuks"). Builds one extra offer next to the normal per-variant
    /// offers; the selection then simply compares it on ComparisonCost like any other offer.
    ///
    /// Deliberately limited to one supplier (no "reels at DigiKey, rest at Mouser"). The rest
    /// variant must be allowed by the packaging policy at this price (so cheap SMT: ReReel only,
    /// not cut tape), and no combination is made when the policy demands an original reel, since
    /// the rest would not be one.
    /// </summary>
    public static class CombinationOfferBuilder
    {
        /// <param name="reelOffer">The normal offer built for <paramref name="reelOption"/> (full reels, rounded up).</param>
        /// <param name="neededQuantity">RequestedQuantity + attrition.</param>
        /// <returns>The combination offer, or null when none applies or it can't be supplied.</returns>
        public static SupplierOffer TryBuild(
            SupplierOffer reelOffer, SupplierPackagingOption reelOption, IEnumerable<SupplierPackagingOption> allOptions,
            int neededQuantity, PackagingPolicy policy, bool originalReelRequired, decimal? maxUnitCost)
        {
            if (reelOffer == null || reelOption == null || allOptions == null) return null;
            if (reelOption.PackagingType != PackagingType.OriginalReel || reelOption.PackagingQuantity <= 0) return null;
            if (originalReelRequired) return null;

            var reelSize = reelOption.PackagingQuantity;
            var fullReels = neededQuantity / reelSize;
            var rest = neededQuantity - fullReels * reelSize;
            if (fullReels < 1 || rest <= 0) return null;

            var mainQuantity = fullReels * reelSize;
            if (reelOption.MinimumOrderQuantity > mainQuantity) return null;
            var mainUnitPrice = PriceAt(reelOption.PriceBreaks, mainQuantity) ?? reelOffer.UnitPrice;
            if (mainUnitPrice <= 0m) return null;

            var allowed = PackagingRules.GetAllowedPackaging(policy, maxUnitCost ?? mainUnitPrice);

            // Cheapest allowed rest variant (rest quantity rounded up to its MOQ).
            var best = allOptions
                .Where(o => o != null && !ReferenceEquals(o, reelOption)
                            && o.PackagingType != PackagingType.OriginalReel
                            && o.PackagingType != PackagingType.Any
                            && PackagingRules.IsAllowed(o.PackagingType, allowed)
                            && o.PriceBreaks != null && o.PriceBreaks.Count > 0)
                .Select(o =>
                {
                    var quantity = Math.Max(rest, Math.Max(o.MinimumOrderQuantity, 1));
                    if (o.OrderMultiple > 1)
                        quantity = (int)Math.Ceiling(quantity / (double)o.OrderMultiple) * o.OrderMultiple;
                    var price = PriceAt(o.PriceBreaks, quantity) ?? 0m;
                    return new { Option = o, Quantity = quantity, Price = price, Cost = quantity * price + o.ReelingFee };
                })
                .Where(c => c.Price > 0m && (c.Option.AvailableQuantity <= 0 || c.Option.AvailableQuantity >= c.Quantity))
                .OrderBy(c => c.Cost)
                .FirstOrDefault();
            if (best == null) return null;

            var totalQuantity = mainQuantity + best.Quantity;
            var totalPrice = mainQuantity * mainUnitPrice + best.Quantity * best.Price;

            // DigiKey reports the same stock pool for every variant; take the larger, and require it
            // to cover the whole combination, like the selection does for a normal offer.
            var availableQuantity = Math.Max(reelOption.AvailableQuantity > 0 ? reelOption.AvailableQuantity : reelOffer.AvailableQuantity,
                best.Option.AvailableQuantity);
            if (availableQuantity < totalQuantity) return null;

            var landedCost = totalPrice + best.Option.ReelingFee + reelOffer.ShippingCost + reelOffer.Tax;
            var surplusQuantity = Math.Max(totalQuantity - neededQuantity, 0);
            var unitPrice = Math.Round(totalPrice / totalQuantity, 4);

            return new SupplierOffer
            {
                PurchaseRequestLineId = reelOffer.PurchaseRequestLineId,
                SupplierCode = reelOffer.SupplierCode,
                SupplierPartNumber = reelOffer.SupplierPartNumber,
                Manufacturer = reelOffer.Manufacturer,
                ManufacturerPartNumber = reelOffer.ManufacturerPartNumber,
                RequestedQuantity = reelOffer.RequestedQuantity,
                OfferedQuantity = totalQuantity,
                AttritionQuantity = reelOffer.AttritionQuantity,
                SurplusQuantity = surplusQuantity,
                UnitPrice = unitPrice,
                MainUnitPrice = mainUnitPrice,
                Currency = reelOffer.Currency,
                TotalPrice = totalPrice,
                MinimumOrderQuantity = reelOffer.MinimumOrderQuantity,
                OrderMultiple = 1,
                PackagingType = PackagingType.OriginalReel,
                PackagingQuantity = reelSize,
                ReelType = reelOffer.ReelType,
                ReelingFee = best.Option.ReelingFee,
                RemainderSupplierPartNumber = best.Option.SupplierPartNumber,
                RemainderQuantity = best.Quantity,
                RemainderUnitPrice = best.Price,
                RemainderPackagingType = best.Option.PackagingType,
                AvailableQuantity = availableQuantity,
                LeadTimeDays = reelOffer.LeadTimeDays,
                EstimatedDeliveryDate = reelOffer.EstimatedDeliveryDate,
                ShippingCost = reelOffer.ShippingCost,
                Tax = reelOffer.Tax,
                LandedCost = landedCost,
                ComparisonCost = PackagingRules.CalculateComparisonCost(
                    landedCost, surplusQuantity, maxUnitCost ?? unitPrice, policy?.SurplusPenaltyFactor ?? 0m),
                MatchConfidence = reelOffer.MatchConfidence,
                ExpiresAt = reelOffer.ExpiresAt
            };
        }

        /// <summary>Unit price of the highest price break at or below the quantity (the first break below the lowest). Null without breaks.</summary>
        public static decimal? PriceAt(IReadOnlyCollection<PriceBreak> priceBreaks, int quantity)
        {
            if (priceBreaks == null || priceBreaks.Count == 0) return null;
            var ordered = priceBreaks.Where(pb => pb.UnitPrice > 0m).OrderBy(pb => pb.BreakQuantity).ToList();
            if (ordered.Count == 0) return null;
            return (ordered.LastOrDefault(pb => pb.BreakQuantity <= quantity) ?? ordered[0]).UnitPrice;
        }
    }
}
