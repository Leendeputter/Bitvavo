using System.Collections.Generic;

namespace Procurement.Core.Models
{
    /// <summary>Input to IErpConnector.CreatePurchaseOrderAsync — the ERP PO to create before any supplier order is placed (spec §6 step 9).</summary>
    public class PurchaseOrderDraft
    {
        public int PurchaseRequestId { get; set; }
        public string SupplierCode { get; set; }
        public List<PurchaseOrderDraftLine> Lines { get; set; } = new List<PurchaseOrderDraftLine>();
    }

    public class PurchaseOrderDraftLine
    {
        public int PurchaseRequestLineId { get; set; }
        public string SupplierPartNumber { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}
