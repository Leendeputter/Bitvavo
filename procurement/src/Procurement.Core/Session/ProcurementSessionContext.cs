using Procurement.Core.Interfaces;

namespace Procurement.Core.Session
{
    /// <summary>
    /// Read-only adapter over the static <see cref="ProcurementSession"/>, so repositories/
    /// services can take <see cref="ISessionContext"/> via constructor injection (and be
    /// mocked in tests) without ProcurementSession itself having to disappear. Mirrors
    /// UniPro2026.Core.Session.AppSessionContext.
    /// </summary>
    public class ProcurementSessionContext : ISessionContext
    {
        public string UserName => ProcurementSession.UserName;
        public long CompanyId => ProcurementSession.CompanyId;
        public string CompanyName => ProcurementSession.CompanyName;
        public string SharedConnectionString => ProcurementSession.SharedConnectionString;
        public string AdminConnectionString => ProcurementSession.AdminConnectionString;
        public bool TestMode => ProcurementSession.TestMode;
    }
}
