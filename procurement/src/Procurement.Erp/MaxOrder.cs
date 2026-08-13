using System;
using System.Linq;

namespace Procurement.Erp
{
    /// <summary>
    /// One row from the user-supplied MAX Order_Master/Part_Master query — a real open purchase
    /// order read from the MAX admin database, before it's synced into this app's own
    /// PurchaseRequest/PurchaseRequestLine tables (see MaxErpConnector).
    /// </summary>
    public class MaxOrder
    {
        /// <summary>Order_Master.ORDNUM_10 ([Order]) — used as PurchaseRequest.ErpRequestNumber (the dedup key when syncing).</summary>
        public string OrderNumber { get; set; }
        public string OrderLong { get; set; }
        /// <summary>Order_Master.PRTNUM_10 (PartID).</summary>
        public string PartId { get; set; }
        /// <summary>Order_Master.CURQTY_10 (CurrentQty).</summary>
        public int CurrentQty { get; set; }
        /// <summary>Order_Master.FRMPLN_10 (Firm) — MAX flag, type on disk unconfirmed, parsed defensively.</summary>
        public bool Firm { get; set; }
        /// <summary>Order_Master.STATUS_10 (Status) — "1" = Planned, "2" = Approved.</summary>
        public string Status { get; set; }
        /// <summary>Order_Master.CURDUE_10 (DueDate).</summary>
        public DateTime? DueDate { get; set; }
        /// <summary>Order_Master.STK_10 (StockID).</summary>
        public string StockId { get; set; }
        /// <summary>Order_Master.REVLEV_10 (Revision).</summary>
        public string Revision { get; set; }
        /// <summary>Order_Master.COST_10 (Cost).</summary>
        public decimal? Cost { get; set; }
        /// <summary>Order_Master.CSTCNV_10 (Cost_Cnv).</summary>
        public decimal? CostConv { get; set; }
        /// <summary>Order_Master.ORDREF_10 (Reference).</summary>
        public string Reference { get; set; }
        /// <summary>Part_Master.PMDES1_01 (Desc1).</summary>
        public string Desc1 { get; set; }
        /// <summary>Part_Master.PMDES2_01 (Desc2).</summary>
        public string Desc2 { get; set; }
        /// <summary>Part_Master.VIEWER_01 (ManufacturingPart) — used as ManufacturerPartNumber.</summary>
        public string ManufacturerPartNumber { get; set; }
        /// <summary>Part_Master.COMCDE_01 (Customer).</summary>
        public string Customer { get; set; }
        /// <summary>Part_Master.TYPE_01 (PartType).</summary>
        public string PartType { get; set; }

        /// <summary>Desc1 + Desc2 combined — kept for PurchaseRequestLine.Description, which the sourcing/matching logic (ProcurementEngine) actually reads; Desc1/Desc2 themselves are display-only.</summary>
        public string Description => string.Join(" ", new[] { Desc1, Desc2 }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}
