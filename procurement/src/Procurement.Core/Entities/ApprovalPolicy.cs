namespace Procurement.Core.Entities
{
    /// <summary>Auto-approve thresholds, kept as a single configurable row (spec §3.5), managed via §8.6.</summary>
    public class ApprovalPolicy
    {
        public int Id { get; set; }
        public decimal MaxOrderValueForAutoApproval { get; set; }
        public decimal MaxPriceVariancePercentage { get; set; }
        public bool AllowExternalSupplier { get; set; }
        public bool AllowNonOriginalPackaging { get; set; }
        public bool AllowAlternativePart { get; set; }
    }
}
