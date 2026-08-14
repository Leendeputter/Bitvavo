namespace Procurement.Core.Enums
{
    /// <summary>
    /// Full lifecycle of a (merged) PurchaseOrder — spec correction, aug 2026: this used to be two
    /// separate enums (PurchaseOrderStatus for the ERP-side PO, SupplierOrderStatusEnum for the
    /// actual supplier-side order) because PurchaseOrder and SupplierOrder used to be two entities.
    /// They're the same thing now (see PurchaseOrder.cs), so one status enum covers both: Created is
    /// the ERP-only state right after CreatePurchaseOrderAsync, before any supplier-side submission
    /// (or forever, for a supplier whose adapter doesn't support Ordering — a manual process).
    /// </summary>
    public enum PurchaseOrderStatus
    {
        Created,
        Submitted,
        Acknowledged,
        Confirmed,
        PartiallyConfirmed,
        Backorder,
        PartiallyShipped,
        Shipped,
        Completed,
        Cancelled,
        Rejected
    }

    public enum ApprovalDecision
    {
        Pending,
        Approved,
        Rejected
    }
}
