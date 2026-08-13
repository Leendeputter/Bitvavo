namespace Procurement.Core.Enums
{
    public enum PurchaseOrderStatus
    {
        Draft,
        Submitted,
        PartiallyOrdered,
        FullyOrdered,
        Cancelled
    }

    public enum SupplierOrderStatusEnum
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
