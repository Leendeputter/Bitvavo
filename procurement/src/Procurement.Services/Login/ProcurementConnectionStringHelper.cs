using System.Data.SqlClient;

namespace Procurement.Services.Login
{
    /// <summary>
    /// Builds this app's own connection string from the MAX admin connection resolved at login
    /// (same server/auth, different catalog) — mirrors UniPro2026's
    /// Core/Configuration/ConnectionStringHelper.BuildSharedConnectionString, including the
    /// Testmodus "_test" suffix behavior.
    ///
    /// TODO: verify "Unitron" is really the catalog name to use — UniPro2026's own equivalent
    /// constant was redacted before this codebase was shared, so this is inferred from the
    /// user's own naming ("de database Unitron") rather than read directly from their source.
    /// </summary>
    public static class ProcurementConnectionStringHelper
    {
        public const string SharedCatalogName = "Unitron";

        public static string BuildSharedConnectionString(string adminConnectionString, bool testMode)
        {
            var builder = new SqlConnectionStringBuilder(adminConnectionString)
            {
                InitialCatalog = testMode ? SharedCatalogName + "_test" : SharedCatalogName
            };

            return builder.ConnectionString;
        }

        /// <summary>
        /// SharedSecrets — the shared secrets database UniPro2026 created on 9 nov 2026 (same server
        /// as Unitron). Mirrors UniPro's ConnectionStringHelper.BuildSharedSecretsConnectionString
        /// exactly, including NOT adding a "_test" suffix: test vs. live is part of a secret's name.
        /// </summary>
        public const string SharedSecretsCatalogName = "SharedSecrets";

        public static string BuildSharedSecretsConnectionString(string adminConnectionString)
        {
            var builder = new SqlConnectionStringBuilder(adminConnectionString)
            {
                InitialCatalog = SharedSecretsCatalogName
            };

            return builder.ConnectionString;
        }
    }
}
