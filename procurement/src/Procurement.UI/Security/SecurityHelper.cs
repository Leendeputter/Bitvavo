using MAX50;
using System;
using System.Data.SqlClient;
using Procurement.Core.Session;

namespace Procurement.UI.Security
{
    /// <summary>
    /// Wraps MAX's own MaxSecurity.GetAccess, mirroring UniPro2026's Security/SecurityHelper.cs
    /// shape exactly so a real per-screen/button rights check is a small change later, not a
    /// rewrite.
    ///
    /// Not called from any UI action yet — every screen/button is currently open to whoever
    /// logged in, matching this prototype's original single-user assumption (spec §15). To wire
    /// a real check into a button, call <see cref="DemandAccess"/> from its Click handler with
    /// an access string matching however MAX access rights are named for this app (see
    /// UniPro2026's Security/AccessRights.cs for the convention it uses, e.g.
    /// "MAW.Materials.Transactions.Shop Issue") — those rights entries need to exist in MAX
    /// first; this class only reads them, it doesn't define them.
    /// </summary>
    public static class SecurityHelper
    {
        public static bool HasAccess(string accessString, SecLevel requiredLevel)
        {
            using (var cn = new SqlConnection(ProcurementSession.AdminConnectionString))
            {
                cn.Open();
                var actualLevel = MaxSecurity.GetAccess(accessString, cn, null, ProcurementSession.UserName);
                return actualLevel >= requiredLevel;
            }
        }

        public static void DemandAccess(string accessString, SecLevel requiredLevel)
        {
            using (var cn = new SqlConnection(ProcurementSession.AdminConnectionString))
            {
                cn.Open();
                var actualLevel = MaxSecurity.GetAccess(accessString, cn, null, ProcurementSession.UserName);
                if (actualLevel < requiredLevel)
                {
                    throw new UnauthorizedAccessException($"Geen rechten voor transactie: {accessString}");
                }
            }
        }
    }
}
