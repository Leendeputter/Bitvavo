using System;
using System.Collections.Generic;
using Procurement.Core.Enums;

namespace Procurement.Core.Entities
{
    /// <summary>
    /// A PO always has exactly one supplier (spec correction, aug 2026) — what used to be split
    /// across a PurchaseOrder (ERP-side, spec §6 step 9) and a separate SupplierOrder (spec §10) is
    /// the same thing, both in MAX's own terminology and in how purchasing actually works here:
    /// grouped by supplier, never by project/customer. A PO can therefore span lines that originated
    /// from different PurchaseRequests, as long as they share a supplier and were placed together
    /// via ProcurementEngine.PlaceOrdersAsync.
    /// </summary>
    public class PurchaseOrder
    {
        public int Id { get; set; }
        public string ErpPoNumber { get; set; }
        public string SupplierCode { get; set; }
        public string SupplierOrderNumber { get; set; }

        /// <summary>Format: PROC-{jaar}-{volgnummer}-{SUPPLIERCODE} (spec §10). Generated when the ERP PO is created, so it's always present even for a PO whose adapter doesn't support Ordering (manual process).</summary>
        public string IdempotencyKey { get; set; }

        /// <summary>Incremented on every (re)submission attempt for the same PO.</summary>
        public int OrderVersion { get; set; }

        public PurchaseOrderStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }

        public string Currency { get; set; }
        public decimal OrderTotal { get; set; }

        public virtual List<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();

        public PurchaseOrder()
        {
            CreatedAt = DateTime.UtcNow;
            Currency = "EUR";
            OrderVersion = 1;
            Status = PurchaseOrderStatus.Created;
        }
    }

    public class PurchaseOrderLine
    {
        public int Id { get; set; }
        public int PurchaseOrderId { get; set; }
        public virtual PurchaseOrder PurchaseOrder { get; set; }

        public int PurchaseRequestLineId { get; set; }
        public string SupplierPartNumber { get; set; }
        public int Quantity { get; set; }
        public int? ConfirmedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }

        /// <summary>Partial shipments against this line (spec correction, aug 2026: "eventueel per lijn nog verschillende deliveries").</summary>
        public virtual List<PurchaseOrderDelivery> Deliveries { get; set; } = new List<PurchaseOrderDelivery>();
    }

    public class PurchaseOrderDelivery
    {
        public int Id { get; set; }
        public int PurchaseOrderLineId { get; set; }
        public virtual PurchaseOrderLine PurchaseOrderLine { get; set; }

        public int Quantity { get; set; }
        public DateTime? EstimatedShipDate { get; set; }
        public DateTime? ActualShipDate { get; set; }
        public string TrackingNumber { get; set; }
    }
}
