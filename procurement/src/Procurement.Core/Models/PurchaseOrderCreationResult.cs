using System.Collections.Generic;

namespace Procurement.Core.Models
{
    /// <summary>
    /// Richer result of writing a PO into the real ERP than just the PO number — carries back the
    /// MAX-assigned LINNUM_10/DELNUM_10 per line (needed later to find the exact Order_Master row
    /// again when processing a supplier confirmation), so MaxErpConnector can persist them onto the
    /// local PurchaseOrderLine right after creation instead of having to re-derive them. Only used
    /// between MaxErpConnector and MaxPurchaseOrderRepository — IErpConnector.CreatePurchaseOrderAsync
    /// itself keeps returning just the plain ErpPoNumber string, since nothing above MaxErpConnector
    /// needs to know MAX's line/delivery-number scheme.
    /// </summary>
    public class PurchaseOrderCreationResult
    {
        public string ErpPoNumber { get; set; }
        public List<PurchaseOrderCreationResultLine> Lines { get; set; } = new List<PurchaseOrderCreationResultLine>();
    }

    public class PurchaseOrderCreationResultLine
    {
        public int PurchaseRequestLineId { get; set; }
        public string MaxLineNumber { get; set; }
        public string MaxDeliveryNumber { get; set; }
    }
}
