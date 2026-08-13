namespace Procurement.Core.Enums
{
    /// <summary>
    /// Status of one capability (search, pricing, ordering, ...) for one supplier adapter.
    /// An adapter reports this instead of throwing, so the engine can skip a step cleanly.
    /// </summary>
    public enum CapabilityStatus
    {
        Supported,
        NotSupported,
        NotAvailable,
        ManualProcess
    }

    /// <summary>
    /// Standardized error codes every ISupplierAdapter must translate its own (mock or real)
    /// errors into, so ProcurementEngine never needs to know supplier-specific error shapes.
    /// </summary>
    public enum SupplierErrorCode
    {
        ProductNotFound,
        InsufficientStock,
        InvalidQuantity,
        PackagingNotAvailable,
        PriceChanged,
        SupplierRejectedOrder,
        AuthenticationError,
        RateLimit,
        Timeout,
        UnknownError
    }
}
