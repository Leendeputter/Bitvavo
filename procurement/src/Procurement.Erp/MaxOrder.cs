using System;

namespace Procurement.Erp
{
    /// <summary>
    /// One row from the user-supplied MAX Order_Master/Part_Master query — a real open purchase
    /// order read from the MAX admin database, before it's synced into this app's own
    /// PurchaseRequest/PurchaseRequestLine tables (see MaxErpConnector).
    /// </summary>
    public class MaxOrder
    {
        /// <summary>Order_Master.ORDNUM_10 — used as PurchaseRequest.ErpRequestNumber (the dedup key when syncing).</summary>
        public string OrderNumber { get; set; }
        public string OrderLong { get; set; }
        public string PartId { get; set; }
        public int CurrentQty { get; set; }
        public string Status { get; set; }
        public DateTime? DueDate { get; set; }
        public string Reference { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// Part_Master.VIEWER_01 — used as ManufacturerPartNumber. TODO (flagged, unconfirmed):
        /// this query has no separate manufacturer-name field, so SupplierOffer matching in the
        /// mock adapters falls back to Medium confidence (never Exact) for MAX-sourced lines,
        /// which routes every one of them to manual approval. Confirm whether Part_Master has a
        /// real manufacturer column to use instead.
        /// </summary>
        public string ManufacturerPartNumber { get; set; }

        public string PartType { get; set; }
    }
}
