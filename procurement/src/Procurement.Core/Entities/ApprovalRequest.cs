using System;
using Procurement.Core.Enums;

namespace Procurement.Core.Entities
{
    /// <summary>
    /// Raised when a line fails the auto-approve criteria of <see cref="ApprovalPolicy"/> (spec §8.3).
    /// Holds the reason(s) it needs a human, and the eventual decision.
    /// </summary>
    public class ApprovalRequest
    {
        public int Id { get; set; }
        public int PurchaseRequestLineId { get; set; }
        public virtual PurchaseRequestLine PurchaseRequestLine { get; set; }

        public int ProposedOfferId { get; set; }
        public virtual SupplierOffer ProposedOffer { get; set; }

        /// <summary>Why this needs manual approval, e.g. "onbekende leverancier", "prijsafwijking &gt;10%".</summary>
        public string Reasons { get; set; }

        public ApprovalDecision Decision { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string DecidedBy { get; set; }

        public ApprovalRequest()
        {
            CreatedAt = DateTime.UtcNow;
            Decision = ApprovalDecision.Pending;
        }
    }
}
