namespace Procurement.Core.Enums
{
    public enum PurchaseRequestStatus
    {
        Pending,
        Sourcing,
        WaitingApproval,
        ReadyToOrder,
        Ordered,
        Exception
    }

    public enum PackagingRequirement
    {
        NoPreference,
        CutTape,
        Tray,
        Tube,
        Reel,
        OriginalReel
    }
}
