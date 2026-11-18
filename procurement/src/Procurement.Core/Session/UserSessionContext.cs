using Procurement.Core.Interfaces;

namespace Procurement.Core.Session
{
    /// <summary>
    /// One login as a plain object, instead of the process-wide <see cref="ProcurementSession"/>.
    /// The web app (plan of 18 nov 2026) keeps one per logged-in user, so two users with a different
    /// company or test mode never see each other's settings. The desktop keeps using
    /// <see cref="ProcurementSessionContext"/>; both implement <see cref="ISessionContext"/>, so the
    /// shared libraries don't care which one they get.
    /// </summary>
    public class UserSessionContext : ISessionContext
    {
        public string UserName { get; set; }
        public long CompanyId { get; set; }
        public string CompanyName { get; set; }
        public string SharedConnectionString { get; set; }
        public string AdminConnectionString { get; set; }
        public bool TestMode { get; set; }
        public string SharedSecretsConnectionString { get; set; }
        public string PrimaryConnectionString { get; set; }
        public string LicensePath { get; set; }
        public string LogPath { get; set; }
        public string LogFile { get; set; }

        /// <summary>Snapshot of the current desktop login, e.g. for tests or tools.</summary>
        public static UserSessionContext CopyOf(ISessionContext source) => new UserSessionContext
        {
            UserName = source.UserName,
            CompanyId = source.CompanyId,
            CompanyName = source.CompanyName,
            SharedConnectionString = source.SharedConnectionString,
            AdminConnectionString = source.AdminConnectionString,
            TestMode = source.TestMode,
            SharedSecretsConnectionString = source.SharedSecretsConnectionString,
            PrimaryConnectionString = source.PrimaryConnectionString,
            LicensePath = source.LicensePath,
            LogPath = source.LogPath,
            LogFile = source.LogFile
        };
    }
}
