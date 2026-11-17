using System;
using System.Collections.Generic;
using System.Linq;
using Procurement.Core.Entities;
using Procurement.Core.Enums;

namespace Procurement.Core.BusinessLogic
{
    public class RejectedOffer
    {
        public SupplierOffer Offer { get; }
        public string Reason { get; }

        public RejectedOffer(SupplierOffer offer, string reason)
        {
            Offer = offer;
            Reason = reason;
        }
    }

    public class SelectionResult
    {
        public SupplierOffer SelectedOffer { get; set; }
        public string ReasonSummary { get; set; }
        public List<RejectedOffer> RejectedOffers { get; set; } = new List<RejectedOffer>();
    }

    /// <summary>
    /// Spec §6 step 7 / §7: filters offers on match confidence, stock, packaging/reel requirement
    /// and lead time, then picks the lowest comparison cost among what remains (landed cost plus the
    /// surplus penalty, 17 nov 2026; plain landed cost for offers without one). Since 17 nov 2026 the
    /// packaging policy's allowed packaging types are enforced too, with its price exception judged on
    /// the MAX unit cost (or the offer's own price when MAX has none). Pure logic, no I/O — this is
    /// the class exercised by the §7 unit test scenario.
    /// </summary>
    public static class SupplierSelectionEngine
    {
        public static SelectionResult SelectBestOffer(
            IEnumerable<SupplierOffer> offers, PurchaseRequestLine line, PackagingPolicy policy, decimal? referenceUnitCost = null)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));

            var result = new SelectionResult();
            var candidates = new List<SupplierOffer>();

            var originalReelRequired = line.ReelRequirement &&
                (policy?.OriginalReelRequired ?? line.PackagingRequirement == PackagingRequirement.OriginalReel);

            foreach (var offer in offers ?? Enumerable.Empty<SupplierOffer>())
            {
                if (offer.MatchConfidence != MatchConfidence.Exact && offer.MatchConfidence != MatchConfidence.Verified)
                {
                    result.RejectedOffers.Add(new RejectedOffer(offer, "Onvoldoende betrouwbare match (geen Exact/Verified match)."));
                    continue;
                }

                if (offer.AvailableQuantity < offer.OfferedQuantity)
                {
                    result.RejectedOffers.Add(new RejectedOffer(offer,
                        $"Onvoldoende voorraad: {offer.AvailableQuantity} beschikbaar, {offer.OfferedQuantity} nodig."));
                    continue;
                }

                if (originalReelRequired && offer.ReelType == ReelType.SupplierReReel)
                {
                    result.RejectedOffers.Add(new RejectedOffer(offer, "Originele reel vereist; aangeboden is een re-reel."));
                    continue;
                }

                var allowedPackaging = PackagingRules.GetAllowedPackaging(policy, referenceUnitCost ?? offer.UnitPrice);
                if (!PackagingRules.IsAllowed(offer.PackagingType, allowedPackaging))
                {
                    result.RejectedOffers.Add(new RejectedOffer(offer, offer.PackagingType == PackagingType.Any
                        ? $"Verpakking onbekend; policy '{policy.ComponentCategory}' vereist een bekende verpakking."
                        : $"Verpakking {offer.PackagingType} is niet toegestaan door policy '{policy.ComponentCategory}'."));
                    continue;
                }

                if (line.RequiredDate.HasValue && offer.EstimatedDeliveryDate.Date > line.RequiredDate.Value.Date)
                {
                    result.RejectedOffers.Add(new RejectedOffer(offer,
                        $"Levertijd ({offer.EstimatedDeliveryDate:d}) overschrijdt de vereiste datum ({line.RequiredDate:d})."));
                    continue;
                }

                candidates.Add(offer);
            }

            if (candidates.Count == 0)
            {
                result.SelectedOffer = null;
                result.ReasonSummary = "Geen enkele offer voldoet aan de selectiecriteria.";
                return result;
            }

            var best = candidates.OrderBy(EffectiveComparisonCost).ThenBy(o => o.LandedCost).First();
            result.SelectedOffer = best;
            result.ReasonSummary = BuildReasonSummary(best, originalReelRequired);
            return result;
        }

        public static decimal EffectiveComparisonCost(SupplierOffer offer) =>
            offer.ComparisonCost > 0m ? offer.ComparisonCost : offer.LandedCost;

        private static string BuildReasonSummary(SupplierOffer offer, bool originalReelRequired)
        {
            var matchText = offer.MatchConfidence == MatchConfidence.Exact ? "exacte MPN-match" : "geverifieerde match";
            var reelText = originalReelRequired ? "originele reel beschikbaar" : "geschikte packaging beschikbaar";
            return $"Geselecteerde leverancier: {offer.SupplierCode}\n" +
                   $"Reden: {matchText}, {reelText}, voldoende voorraad, " +
                   "levering binnen vereiste datum, laagste totale kosten" +
                   (offer.SurplusQuantity > 0 ? $" (incl. overschot van {offer.SurplusQuantity} st als nadeel)." : ".") +
                   $"\nVerpakking: {offer.PackagingType}" +
                   (offer.AttritionQuantity > 0 ? $", extra voor uitval: {offer.AttritionQuantity} st." : ".");
        }
    }
}
