using System;
using System.Collections.Generic;
using System.Linq;
using Procurement.Core.Entities;
using Procurement.Core.Models;

namespace Procurement.Core.BusinessLogic
{
    /// <summary>
    /// "Extra voor uitval" (user request, 17 nov 2026): extra pieces per request line for machine
    /// attrition, added before rounding to MOQ/reel and before bundling. Pure logic, no I/O.
    /// </summary>
    public static class AttritionCalculator
    {
        /// <summary>
        /// The rule for this article: a rule for its Category beats one for its component type;
        /// within those, the price band containing the MAX unit cost. With no known unit cost only a
        /// rule without a price band ("alle") can match. Null when nothing matches.
        /// </summary>
        public static AttritionRule FindRule(IEnumerable<AttritionRule> rules, ComponentClassification classification, decimal? unitCost)
        {
            if (classification == null) return null;
            var list = (rules ?? Enumerable.Empty<AttritionRule>()).ToList();
            return FindFor(list, classification.Category, unitCost)
                ?? FindFor(list, classification.ComponentType, unitCost);
        }

        /// <summary>max(ceil(requested × percentage / 100), minimum extra); 0 without a rule.</summary>
        public static int CalculateExtra(int requestedQuantity, AttritionRule rule)
        {
            if (rule == null || requestedQuantity <= 0) return 0;
            var byPercentage = (int)Math.Ceiling(requestedQuantity * Math.Max(rule.Percentage, 0m) / 100m);
            return Math.Max(byPercentage, Math.Max(rule.MinimumExtra, 0));
        }

        public static int CalculateExtra(int requestedQuantity, IEnumerable<AttritionRule> rules, ComponentClassification classification, decimal? unitCost) =>
            CalculateExtra(requestedQuantity, FindRule(rules, classification, unitCost));

        private static AttritionRule FindFor(List<AttritionRule> rules, string appliesTo, decimal? unitCost)
        {
            var key = PackagingRules.Normalize(appliesTo);
            if (string.IsNullOrEmpty(key)) return null;

            return rules
                .Where(r => PackagingRules.Normalize(r.AppliesTo) == key)
                .Where(r => InBand(r, unitCost))
                // The narrowest band first, so an "alle" rule only applies where no band matches.
                .OrderBy(r => r.MinUnitPrice.HasValue || r.MaxUnitPrice.HasValue ? 0 : 1)
                .ThenBy(r => r.MinUnitPrice ?? decimal.MinValue)
                .FirstOrDefault();
        }

        private static bool InBand(AttritionRule rule, decimal? unitCost)
        {
            if (!unitCost.HasValue)
                return !rule.MinUnitPrice.HasValue && !rule.MaxUnitPrice.HasValue;
            if (rule.MinUnitPrice.HasValue && unitCost.Value < rule.MinUnitPrice.Value) return false;
            if (rule.MaxUnitPrice.HasValue && unitCost.Value >= rule.MaxUnitPrice.Value) return false;
            return true;
        }
    }
}
