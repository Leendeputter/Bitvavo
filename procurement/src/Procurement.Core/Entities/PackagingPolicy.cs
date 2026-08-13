using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Procurement.Core.Enums;

namespace Procurement.Core.Entities
{
    /// <summary>Reel/packaging business rules per component category, kept as data (spec §3.5), managed via §8.6.</summary>
    public class PackagingPolicy
    {
        public int Id { get; set; }
        public string ComponentCategory { get; set; }
        public int MinimumQuantity { get; set; }
        public PackagingType PreferredPackaging { get; set; }

        /// <summary>Persisted as a comma-separated list of enum names; use <see cref="AllowedPackaging"/> in code.</summary>
        public string AllowedPackagingCsv { get; set; }

        [NotMapped]
        public List<PackagingType> AllowedPackaging
        {
            get => string.IsNullOrWhiteSpace(AllowedPackagingCsv)
                ? new List<PackagingType>()
                : AllowedPackagingCsv.Split(',')
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .Select(s => (PackagingType)Enum.Parse(typeof(PackagingType), s))
                    .ToList();
            set => AllowedPackagingCsv = value == null ? null : string.Join(",", value.Select(v => v.ToString()));
        }

        public bool OriginalReelRequired { get; set; }
    }
}
