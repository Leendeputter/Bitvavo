using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using Procurement.Core.Session;

namespace Procurement.Data.Secrets
{
    /// <summary>
    /// CRUD around SharedSecrets.dbo.AppSecrets — the shared, cross-application secrets database
    /// UniPro2026 introduced on 9 nov 2026 (UniPro: Database/Migrations/2026-11-09_create-sharedsecrets-database.sql).
    /// A near-literal port of UniPro2026's Data/AppSecretsRepository.cs (user request, 13 nov 2026:
    /// "secret keys op dezelfde manier opslaan als we dat voor UniPro hebben opgezet"), so that the
    /// planned merge of Procurement into UniPro can drop this class in favor of UniPro's own.
    ///
    /// Since 18 nov 2026 (same change as UniPro: SecretProtector and UNIPRO_SECRET_KEY removed) values
    /// are stored as plain text in the [Value] column; protection comes from SQL rights on the
    /// SharedSecrets database plus the per-secret SecretAccess check below. The old ValueEncrypted
    /// column is no longer read or written.
    ///
    /// <see cref="ApplicationName"/> = "Procurement" keeps these rows apart from UniPro's own
    /// ("UniPro") — UniPro's App Secrets screen only ever lists its own ApplicationName, so the two
    /// apps can share the database without seeing (or tripping over) each other's rows. After the
    /// merge, a single UPDATE of ApplicationName (plus adding these names to UniPro's
    /// SecretDefinitions) moves them over.
    /// </summary>
    public class AppSecretsRepository
    {
        public const string ApplicationName = "Procurement";

        public List<string> GetAllNames()
        {
            using (var cn = new SqlConnection(ProcurementSession.SharedSecretsConnectionString))
            {
                return cn.Query<string>(
                    "SELECT [Name] FROM dbo.AppSecrets WHERE ApplicationName = @ApplicationName AND [Value] IS NOT NULL AND [Value] <> '' ORDER BY [Name]",
                    new { ApplicationName }
                ).ToList();
            }
        }

        /// <summary>
        /// Returns null if there's no row (or an empty value) — the caller decides whether that means
        /// "not configured". Checks first that the current user (ProcurementSession.UserName) has an
        /// explicit CanRead=1 row in dbo.SecretAccess for this exact secret, and throws a clear
        /// UnauthorizedAccessException otherwise — same rule as UniPro, so a missing grant shows up
        /// as "no permission" instead of as a confusing "API key missing" further down.
        /// </summary>
        public string GetValue(string name)
        {
            using (var cn = new SqlConnection(ProcurementSession.SharedSecretsConnectionString))
            {
                bool hasAccess = cn.Query<bool>(
                    @"SELECT CanRead FROM dbo.SecretAccess
                      WHERE ApplicationName = @ApplicationName AND SecretName = @Name AND UserName = @UserName",
                    new { ApplicationName, Name = name, UserName = ProcurementSession.UserName }).FirstOrDefault();

                if (!hasAccess)
                {
                    throw new UnauthorizedAccessException(
                        $"Je account ({ProcurementSession.UserName}) heeft geen toestemming om de instelling '{name}' te " +
                        "gebruiken. Vraag een beheerder om dit via Beheer > App Secrets toe te kennen.");
                }

                string value = cn.Query<string>(
                    "SELECT [Value] FROM dbo.AppSecrets WHERE ApplicationName = @ApplicationName AND [Name] = @Name",
                    new { ApplicationName, Name = name }).FirstOrDefault();

                // Trim: a copy-pasted key with a trailing space/newline otherwise fails as "invalid clientId".
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
        }

        /// <summary>
        /// Upsert. Deliberately no SecretAccess check (same as UniPro): editing values is gated by the
        /// MGR User Security right that Beheer > App Secrets demands before it opens; SecretAccess only
        /// governs who may *use* an existing value. Also used for DigiKey's automatic refresh-token
        /// rotation, which is a write the app makes on its own, not a user reading a secret.
        /// </summary>
        public void Set(string name, string value)
        {
            const string sql = @"
            IF EXISTS (SELECT 1 FROM dbo.AppSecrets WHERE ApplicationName = @ApplicationName AND [Name] = @Name)
                UPDATE dbo.AppSecrets
                SET [Value] = @Value,
                    ModifiedBy = @ModifiedBy,
                    ModificationDate = GETDATE()
                WHERE ApplicationName = @ApplicationName AND [Name] = @Name
            ELSE
                INSERT INTO dbo.AppSecrets (ApplicationName, [Name], [Value], ModifiedBy, ModificationDate)
                VALUES (@ApplicationName, @Name, @Value, @ModifiedBy, GETDATE())";

            using (var cn = new SqlConnection(ProcurementSession.SharedSecretsConnectionString))
            {
                cn.Execute(sql, new
                {
                    ApplicationName,
                    Name = name,
                    Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim(),
                    ModifiedBy = ProcurementSession.UserName
                });
            }
        }

        /// <summary>Saves only the secrets that actually got a new value — an empty field on the App Secrets screen leaves the stored value alone (write-only convention: a stored value is never shown back).</summary>
        public void UpdateIfProvided(IDictionary<string, string> nameToNewValue)
        {
            foreach (var kvp in nameToNewValue)
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                    Set(kvp.Key, kvp.Value);
            }
        }
    }
}
