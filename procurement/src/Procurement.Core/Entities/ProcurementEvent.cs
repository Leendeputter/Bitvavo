using System;

namespace Procurement.Core.Entities
{
    /// <summary>Append-only audit trail entry. Every workflow step in spec §6 writes at least one of these.</summary>
    public class ProcurementEvent
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string EntityType { get; set; }
        public string EntityId { get; set; }

        /// <summary>e.g. PRODUCT_SEARCHED, SUPPLIER_SELECTED, ERP_PO_CREATED, ...</summary>
        public string EventType { get; set; }
        public string UserOrSystem { get; set; }
        public string SupplierCode { get; set; }

        /// <summary>Masked/stripped of credentials before being written — never store secrets here.</summary>
        public string RequestPayload { get; set; }
        public string ResponsePayload { get; set; }
        public string Status { get; set; }
        public string Error { get; set; }

        public ProcurementEvent()
        {
            Timestamp = DateTime.UtcNow;
        }
    }
}
