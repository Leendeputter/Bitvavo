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

        bool TestMode { get; }
    }
}
