namespace Procurement.Core.Models
{
    /// <summary>
    /// An article's component type and category from the MAX view UniPro_OmnifyPartType (user, 17 nov
    /// 2026): EntryType 16 = SMT, 17 = THT; Category e.g. "Resistors SMT", "Connectors THT".
    /// </summary>
    public class ComponentClassification
    {
        public const string Smt = "SMT";
        public const string Tht = "THT";

        /// <summary>"SMT", "THT" or null when unknown.</summary>
        public string ComponentType { get; set; }

        /// <summary>Null when unknown.</summary>
        public string Category { get; set; }

        public static string ComponentTypeFromEntryType(int entryType)
        {
            switch (entryType)
            {
                case 16: return Smt;
                case 17: return Tht;
                default: return null;
            }
        }

        /// <summary>"SMT"/"THT" when the Category name ends with it (e.g. "Resistors SMT"), else null.</summary>
        public static string ComponentTypeFromCategory(string category)
        {
            var trimmed = category?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return null;
            if (trimmed.EndsWith(" " + Smt, System.StringComparison.OrdinalIgnoreCase)) return Smt;
            if (trimmed.EndsWith(" " + Tht, System.StringComparison.OrdinalIgnoreCase)) return Tht;
            return null;
        }
    }
}
