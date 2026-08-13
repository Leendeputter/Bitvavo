using System;
using Procurement.Core.Enums;

namespace Procurement.Core.Entities
{
    public class SupplierSelection
    {
        public int Id { get; set; }
        public int PurchaseRequestLineId { get; set; }
        public virtual PurchaseRequestLine PurchaseRequestLine { get; set; }

        public int SelectedOfferId { get; set; }
        public virtual SupplierOffer SelectedOffer { get; set; }

        /// <summary>Human-readable explanation of why this offer was chosen (spec §7).</summary>
        public string ReasonSummary { get; set; }
        public DateTime SelectedAt { get; set; }
        public SelectionMode Mode { get; set; }

        public SupplierSelection()
        {
            SelectedAt = DateTime.UtcNow;
        }
    }
}
