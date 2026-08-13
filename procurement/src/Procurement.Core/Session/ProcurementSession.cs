namespace Procurement.Core.Session
{
    /// <summary>
    /// Process-wide session state, populated once by the login form and read everywhere
    /// downstream. Mirrors UniPro2026.Core.AppSession, including its known trade-off (mutable
    /// static state that most of the app reads directly) — kept for consistency with how the
    /// rest of the Unitron toolset works, not because it's the ideal pattern for new code.
    /// New/rewritten code should prefer depending on <see cref="Interfaces.ISessionContext"/>
    /// (see <see cref="ProcurementSessionContext"/>) instead of this class directly.
    /// </summary>
    public static class ProcurementSession
    {
        public static string UserName { get; set; }
        public static string LicensePath { get; set; }

        /// <summary>Passed into MyLogManager.Create at login, before any MaxSQL call — MaxSQL reads MyLogManager.Instance() internally and NREs if it was never initialized.</summary>
        public static string LogPath { get; set; }
        public static string LogFile { get; set; }

        /// <summary>MAX "primary" connection, used to look up the list of companies (ExactRMCompanies).</summary>
        public static string PrimaryConnectionString { get; set; }

        /// <summary>Per-company MAX administration connection. Not used for this app's own tables — reserved for future MaxSecurity rights checks (see Security/SecurityHelper).</summary>
        public static string AdminConnectionString { get; set; }

        /// <summary>Connection string for this app's own tables — the "Unitron" (or "Unitron_test") database.</summary>
        public static string SharedConnectionString { get; set; }

        public static long CompanyId { get; set; }
        public static string CompanyName { get; set; }

        /// <summary>Set from the Testmodus checkbox on the login screen — same effect as UniPro: the shared database gets a "_test" suffix.</summary>
        public static bool TestMode { get; set; }
    }
}
