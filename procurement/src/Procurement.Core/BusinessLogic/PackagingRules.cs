using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Procurement.Core.Models;

namespace Procurement.Core.BusinessLogic
{
    /// <summary>
    /// Packaging policy per component type/category (user request, 17 nov 2026; see the doc
    /// "Voorstel: verpakkingsbeleid per componentsoort"). Pure logic, no I/O.
    /// </summary>
    public static class PackagingRules
    {
        public const string DefaultPolicyName = "Default";

        /// <summary>
        /// Most specific policy for an article: its Category ("Integrated Circuits SMT"), then its
        /// component type ("SMT"/"THT"), then "Default". Null when there is no policy at all.
        /// </summary>
        public static PackagingPolicy ResolvePolicy(IEnumerable<PackagingPolicy> policies, ComponentClassification classification)
        {
            var list = (policies ?? Enumerable.Empty<PackagingPolicy>()).ToList();
            return Find(list, classification?.Category)
                ?? Find(list, classification?.ComponentType)
                ?? Find(list, DefaultPolicyName);
        }

        /// <summary>
        /// The packaging types a policy allows at this unit cost: AllowedPackaging, plus
        /// ExceptionPackaging when the unit cost is at or above ExceptionPriceThreshold. EitherReel
        /// expands to OriginalReel + ReReel. Null = no restriction (no policy, or an empty list).
        /// </summary>
        public static HashSet<PackagingType> GetAllowedPackaging(PackagingPolicy policy, decimal? unitCost)
        {
            if (policy == null) return null;

            var allowed = new HashSet<PackagingType>(policy.AllowedPackaging);
            if (allowed.Count == 0) return null;

            if (policy.ExceptionPriceThreshold.HasValue && unitCost.HasValue && unitCost.Value >= policy.ExceptionPriceThreshold.Value)
                allowed.UnionWith(policy.ExceptionPackaging);

            if (allowed.Contains(PackagingType.EitherReel))
            {
                allowed.Add(PackagingType.OriginalReel);
                allowed.Add(PackagingType.ReReel);
            }

            // Unknown packaging (an adapter without packaging info) only passes the catch-all
            // Default policy: for SMT/THT the packaging has to be known to know it's allowed.
            if (IsDefault(policy))
                allowed.Add(PackagingType.Any);

            return allowed;
        }

        public static bool IsAllowed(PackagingType packagingType, ISet<PackagingType> allowed)
        {
            if (allowed == null) return true;
            if (packagingType == PackagingType.EitherReel)
                return allowed.Contains(PackagingType.OriginalReel) || allowed.Contains(PackagingType.ReReel);
            return allowed.Contains(packagingType);
        }

        /// <summary>
        /// What offers are compared on: landed cost plus the surplus penalty (user decision, 17 nov
        /// 2026: surplus counts as a disadvantage) = factor × surplus pieces × unit cost.
        /// </summary>
        public static decimal CalculateComparisonCost(decimal landedCost, int surplusQuantity, decimal unitCost, decimal surplusPenaltyFactor)
        {
            var surplus = Math.Max(surplusQuantity, 0);
            var factor = Math.Max(surplusPenaltyFactor, 0m);
            return landedCost + factor * surplus * Math.Max(unitCost, 0m);
        }

        /// <summary>Case-insensitive, whitespace-normalised (the MAX list has a non-breaking space in one Category).</summary>
        public static string Normalize(string value) =>
            value == null ? null : Regex.Replace(value.Replace(' ', ' '), @"\s+", " ").Trim().ToUpperInvariant();

        public static bool IsDefault(PackagingPolicy policy) =>
            policy != null && Normalize(policy.ComponentCategory) == Normalize(DefaultPolicyName);

        private static PackagingPolicy Find(List<PackagingPolicy> policies, string name)
        {
            var key = Normalize(name);
            if (string.IsNullOrEmpty(key)) return null;
            return policies.FirstOrDefault(p => Normalize(p.ComponentCategory) == key);
        }
    }
}
