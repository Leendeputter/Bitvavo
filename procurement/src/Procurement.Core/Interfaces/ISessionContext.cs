namespace Procurement.Core.Interfaces
{
    /// <summary>
    /// Read-only view of the current login/company session. Mirrors
    /// UniPro2026.Application.Interfaces.ISessionContext — same idea: new code programs against
    /// this interface (mockable/testable) instead of reading the static session class directly,
    /// without requiring that static class to go away.
    /// </summary>
    public interface ISessionContext
    {
        string UserName { get; }
        long CompanyId { get; }
        string CompanyName { get; }

        /// <summary>Connection string for this app's own tables (the "Unitron" database, or "Unitron_test" when TestMode is on).</summary>
        string SharedConnectionString { get; }

        /// <summary>Per-company MAX administration connection — used to read real MAX tables (e.g. Order_Master/Part_Master), not this app's own tables.</summary>
        string AdminConnectionString { get; }

        bool TestMode { get; }

        // The four below are only needed to construct a MAX50 MaxOrderModule instance for writing
        // (MaxPurchaseOrderRepository) — LoginForm already resolves/caches all of them at login,
        // this just exposes them through the interface instead of the ISessionContext consumer
        // reaching into the static ProcurementSession class directly.
        /// <summary>MAX "primary" connection — same one LoginForm used to look up ExactRMCompanies.</summary>
        string PrimaryConnectionString { get; }
        string LicensePath { get; }
        string LogPath { get; }
        string LogFile { get; }
    }
}
