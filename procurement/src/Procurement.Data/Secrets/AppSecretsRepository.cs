using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using Procurement.Core.Security;
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
    /// Values never enter or leave the database unencrypted (SecretProtector does that here), so
    /// callers (SecretSettings, AppSecretsForm) only ever deal in plain text.
    ///
    /// <see cref="ApplicationName"/> = "Procurement" keeps these rows apart from UniPro's own
    /// ("UniPro") — UniPro's App Secrets screen only ever lists its own ApplicationName, so the two
    /// apps can share the database without seeing (or tripping over) each other's rows. After the
    /// merge, a single UPDATE of ApplicationName (plus adding these names to UniPro's
    /// SecretDefinitions) moves them over; no re-encryption, since both apps use UNIPRO_SECRET_KEY.
    /// </summary>
    public class AppSecretsRepository
    {
        public const string ApplicationName = "Procurement";

        public List<string> GetAllNames()
        {
            using (var cn = new SqlConnection(ProcurementSession.SharedSecretsConnectionString))
            {
                return cn.Query<string>(
                    "SELECT [Name] FROM dbo.AppSecrets WHERE ApplicationName = @ApplicationName AND ValueEncrypted IS NOT NULL ORDER BY [Name]",
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
        public string GetDecryptedValue(string name)
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

                string encrypted = cn.Query<string>(
                    "SELECT ValueEncrypted FROM dbo.AppSecrets WHERE ApplicationName = @ApplicationName AND [Name] = @Name",
                    new { ApplicationName, Name = name }).FirstOrDefault();

                return SecretProtector.Decrypt(encrypted);
            }
        }

        /// <summary>
        /// Upsert. Deliberately no SecretAccess check (same as UniPro): editing values is gated by the
        /// MGR User Security right that Beheer > App Secrets demands before it opens; SecretAccess only
        /// governs who may *use* an existing value. Also used for DigiKey's automatic refresh-token
        /// rotation, which is a write the app makes on its own, not a user reading a secret.
        /// </summary>
        public void Set(string name, string plaintextValue)
        {
            string encrypted = SecretProtector.Encrypt(plaintextValue);

            const string sql = @"
            IF EXISTS (SELECT 1 FROM dbo.AppSecrets WHERE ApplicationName = @ApplicationName AND [Name] = @Name)
                UPDATE dbo.AppSecrets
                SET ValueEncrypted = @ValueEncrypted,
                    ModifiedBy = @ModifiedBy,
                    ModificationDate = GETDATE()
                WHERE ApplicationName = @ApplicationName AND [Name] = @Name
            ELSE
                INSERT INTO dbo.AppSecrets (ApplicationName, [Name], ValueEncrypted, ModifiedBy, ModificationDate)
                VALUES (@ApplicationName, @Name, @ValueEncrypted, @ModifiedBy, GETDATE())";

            using (var cn = new SqlConnection(ProcurementSession.SharedSecretsConnectionString))
            {
                cn.Execute(sql, new
                {
                    ApplicationName,
                    Name = name,
                    ValueEncrypted = encrypted,
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
