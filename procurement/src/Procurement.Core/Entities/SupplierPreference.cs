namespace Procurement.Core.Entities
{
    /// <summary>Which suppliers are active/eligible for auto-ordering, and in what priority order. Managed via §8.6.</summary>
    public class SupplierPreference
    {
        public int Id { get; set; }
        public string SupplierCode { get; set; }
        public int Priority { get; set; }
        public bool Active { get; set; }
        public bool AllowedForAutoOrder { get; set; }
    }
}
