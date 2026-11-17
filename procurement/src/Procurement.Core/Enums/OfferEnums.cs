namespace Procurement.Core.Enums
{
    public enum MatchConfidence
    {
        Unknown,
        Low,
        Medium,
        High,
        Verified,
        Exact
    }

    public enum PackagingType
    {
        Any,
        OriginalReel,
        ReReel,
        EitherReel,
        CutTape,
        Tray,
        Tube,
        // Appended (not inserted) so the stored int values of the existing members stay the same.
        Bulk
    }

    public enum ReelType
    {
        ManufacturerOriginal,
        SupplierReReel
    }

    public enum OfferStatus
    {
        Active,
        Selected,
        Rejected,
        Expired
    }

    public enum SelectionMode
    {
        Automatic,
        Manual
    }
}
