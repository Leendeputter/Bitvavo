using System;
using System.Collections.Generic;
using Procurement.Core.Entities;
using Procurement.Core.Enums;

namespace Procurement.Core.BusinessLogic
{
    public class ApprovalEvaluation
    {
        public bool IsAutoApproved { get; }
        public IReadOnlyList<string> Reasons { get; }

        public ApprovalEvaluation(bool isAutoApproved, IReadOnlyList<string> reasons)
        {
            IsAutoApproved = isAutoApproved;
            Reasons = reasons;
        }
    }

    /// <summary>
    /// Spec §6 step 8: checks the selected offer against ApprovalPolicy (order value, price
    /// variance, external supplier, non-original packaging, alternative part, fuzzy match). Any
    /// failing check routes the line to the manual approval screen (§8.3).
    /// </summary>
    public static class ApprovalEvaluator
    {
        public static ApprovalEvaluation Evaluate(
            SupplierOffer selectedOffer,
            PurchaseRequestLine line,
            ApprovalPolicy policy,
            SupplierPreference supplierPreference,
            decimal? referenceUnitPrice)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            var reasons = new List<string>();

            if (selectedOffer == null)
            {
                reasons.Add("Geen geldige offer gevonden.");
                return new ApprovalEvaluation(false, reasons);
            }

            if (selectedOffer.TotalPrice > policy.MaxOrderValueForAutoApproval)
            {
                reasons.Add(
                    $"Orderwaarde ({selectedOffer.TotalPrice:0.00}) overschrijdt de auto-approval-grens ({policy.MaxOrderValueForAutoApproval:0.00}).");
            }

            if (referenceUnitPrice.HasValue && referenceUnitPrice.Value > 0)
            {
                var variance = Math.Abs(selectedOffer.UnitPrice - referenceUnitPrice.Value) / referenceUnitPrice.Value * 100m;
                if (variance > policy.MaxPriceVariancePercentage)
                {
                    reasons.Add(
                        $"Prijsafwijking ({variance:0.0}%) overschrijdt de toegestane afwijking ({policy.MaxPriceVariancePercentage:0.0}%).");
                }
            }

            if (!policy.AllowExternalSupplier && (supplierPreference == null || !supplierPreference.AllowedForAutoOrder))
            {
                reasons.Add($"Leverancier {selectedOffer.SupplierCode} is niet geconfigureerd voor automatisch bestellen.");
            }

            if (!policy.AllowNonOriginalPackaging && selectedOffer.ReelType == ReelType.SupplierReReel)
            {
                reasons.Add("Niet-originele (re-reel) packaging is niet toegestaan voor automatische goedkeuring.");
            }

            if (!policy.AllowAlternativePart &&
                !string.Equals(selectedOffer.ManufacturerPartNumber, line.ManufacturerPartNumber, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("Aangeboden onderdeel wijkt af van het aangevraagde MPN (alternatief onderdeel).");
            }

            if (selectedOffer.MatchConfidence != MatchConfidence.Exact && selectedOffer.MatchConfidence != MatchConfidence.Verified)
            {
                reasons.Add($"Match confidence ({selectedOffer.MatchConfidence}) is onvoldoende betrouwbaar (fuzzy match).");
            }

            return new ApprovalEvaluation(reasons.Count == 0, reasons);
        }
    }
}
