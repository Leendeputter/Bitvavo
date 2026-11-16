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
                        $"Prijsafwijking ({variance:0.0}%) t.o.v. de MAX-kostprijs ({referenceUnitPrice.Value:0.0000}) overschrijdt de toegestane afwijking ({policy.MaxPriceVariancePercentage:0.0}%).");
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

        /// <summary>
        /// Reference price for the "Max price variance" check: the article's known cost price in MAX
        /// (user request, 16 nov 2026: "gerelateerd aan de bekende kostprijs in MAX van het artikel"),
        /// i.e. Order_Master.COST_10 / CSTCNV_10 as synced onto the line — the same unit cost the
        /// Purchase Requests grid uses for Ext. Cost. Replaces the earlier "average of the other
        /// offers". Null (check skipped) when MAX has no usable cost, e.g. a manual test request.
        /// </summary>
        public static decimal? GetMaxReferenceUnitPrice(PurchaseRequestLine line)
        {
            if (line?.Cost == null || line.Cost.Value <= 0m) return null;
            if (line.CostConv == null || line.CostConv.Value == 0m) return null;
            return line.Cost.Value / line.CostConv.Value;
        }
    }
}
