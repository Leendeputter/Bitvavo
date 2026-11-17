using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Procurement.Core.Enums;

namespace Procurement.Core.Entities
{
    /// <summary>
    /// Packaging rules per component type or category (spec §3.5, managed via §8.6). Since 17 nov 2026
    /// (user request, see the "Voorstel: verpakkingsbeleid per componentsoort" doc) a policy applies to:
    /// a Category from the MAX view UniPro_OmnifyPartType (e.g. "Integrated Circuits SMT"), a whole
    /// component type ("SMT" = EntryType 16, "THT" = EntryType 17), or "Default". The most specific
    /// match wins (see PackagingRules.ResolvePolicy). Every allowed packaging variant of every supplier
    /// becomes its own offer; cost decides between them, not a preference.
    /// </summary>
    public class PackagingPolicy
    {
        public int Id { get; set; }

        /// <summary>A Category from UniPro_OmnifyPartType, "SMT", "THT" or "Default".</summary>
        public string ComponentCategory { get; set; }

        /// <summary>
        /// No longer used by the selection logic (cost decides between the allowed variants since
        /// 17 nov 2026). Kept as a column so this change needs no table rebuild; hidden in Settings.
        /// </summary>
        public PackagingType PreferredPackaging { get; set; }

        /// <summary>Persisted as a comma-separated list of enum names; use <see cref="AllowedPackaging"/> in code.</summary>
        public string AllowedPackagingCsv { get; set; }

        [NotMapped]
        public List<PackagingType> AllowedPackaging
        {
            get => ParseCsv(AllowedPackagingCsv);
            set => AllowedPackagingCsv = ToCsv(value);
        }

        /// <summary>
        /// From this MAX unit cost (COST_01 / CSTCNV_01) up, <see cref="ExceptionPackaging"/> is allowed
        /// too — e.g. cut tape and tray for expensive SMT ICs, bulk for expensive THT parts. Null = never.
        /// </summary>
        public decimal? ExceptionPriceThreshold { get; set; }

        /// <summary>Persisted as a comma-separated list of enum names; use <see cref="ExceptionPackaging"/> in code.</summary>
        public string ExceptionPackagingCsv { get; set; }

        [NotMapped]
        public List<PackagingType> ExceptionPackaging
        {
            get => ParseCsv(ExceptionPackagingCsv);
            set => ExceptionPackagingCsv = ToCsv(value);
        }

        /// <summary>
        /// How heavily surplus stock counts against an offer (user decision, 17 nov 2026: "Overschot
        /// wordt meegeteld als nadeel"). Comparison cost = landed cost + factor × surplus × unit cost.
        /// 0 = surplus is free, 1 = surplus counts at full value.
        /// </summary>
        public decimal SurplusPenaltyFactor { get; set; } = 0.5m;

        public bool OriginalReelRequired { get; set; }

        private static List<PackagingType> ParseCsv(string csv) =>
            string.IsNullOrWhiteSpace(csv)
                ? new List<PackagingType>()
                : csv.Split(',')
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .Select(s => (PackagingType)Enum.Parse(typeof(PackagingType), s))
                    .ToList();

        private static string ToCsv(IEnumerable<PackagingType> values) =>
            values == null ? null : string.Join(",", values.Distinct().Select(v => v.ToString()));
    }
}
