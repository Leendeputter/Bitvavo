namespace Procurement.UI.Security
{
    /// <summary>
    /// MAX access-right strings checked through <see cref="SecurityHelper"/>. Same convention (and,
    /// where the same screen exists in both apps, the same values) as UniPro2026's
    /// Security/AccessRights.cs, so these merge one-to-one later.
    /// </summary>
    public static class AccessRights
    {
        /// <summary>
        /// Gate for Beheer > App Secrets — the exact right UniPro2026 uses for its own App Secrets
        /// screen: an existing MAX right (module MGR, "Activity.User Security"), not a made-up new
        /// one, because a right that isn't configured in MAX silently locks everyone out (UniPro
        /// learned that the hard way). Requires at least Supervisor level on System Manager - User
        /// Security.
        /// </summary>
        public const string MgrUserSecurity = "MGR.Activity.User Security";
    }
}
