using MAX50;
using Procurement.Core.Session;
using Procurement.Services.Login;

namespace Procurement.UI.Security
{
    /// <summary>
    /// MAX rights check for the desktop's process-wide login. Mirrors UniPro2026's
    /// Security/SecurityHelper.cs; the actual check lives in <see cref="MaxRights"/> since 18 nov 2026
    /// so the web app does exactly the same. Access strings: see <see cref="AccessRights"/>.
    /// </summary>
    public static class SecurityHelper
    {
        public static bool HasAccess(string accessString, SecLevel requiredLevel) =>
            MaxRights.HasAccess(new ProcurementSessionContext(), accessString, requiredLevel);

        public static void DemandAccess(string accessString, SecLevel requiredLevel) =>
            MaxRights.DemandAccess(new ProcurementSessionContext(), accessString, requiredLevel);
    }
}
