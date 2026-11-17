namespace Procurement.Core.Entities
{
    /// <summary>
    /// "Extra voor uitval" (user request, 17 nov 2026): extra pieces bought per request line to cover
    /// machine attrition. Extra = max(ceil(requested × Percentage / 100), MinimumExtra). A rule applies
    /// to a Category from UniPro_OmnifyPartType (e.g. "Resistors SMT") or a whole component type
    /// ("SMT"/"THT"), within a MAX unit cost band [MinUnitPrice, MaxUnitPrice). A Category rule beats a
    /// type rule. See AttritionCalculator.
    /// </summary>
    public class AttritionRule
    {
        public int Id { get; set; }

        /// <summary>A Category from UniPro_OmnifyPartType, or "SMT"/"THT".</summary>
        public string AppliesTo { get; set; }

        /// <summary>Inclusive lower bound of the MAX unit cost; null = no lower bound.</summary>
        public decimal? MinUnitPrice { get; set; }

        /// <summary>Exclusive upper bound of the MAX unit cost; null = no upper bound.</summary>
        public decimal? MaxUnitPrice { get; set; }

        /// <summary>Percentage of the requested quantity, e.g. 10 for 10%.</summary>
        public decimal Percentage { get; set; }

        public int MinimumExtra { get; set; }
    }
}
