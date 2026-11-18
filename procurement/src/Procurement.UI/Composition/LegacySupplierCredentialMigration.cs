using System;
using System.Collections.Generic;
using System.Linq;
using Procurement.Core.Entities;
using Procurement.Core.Models;
using Procurement.Core.Security;
using Procurement.Core.Session;
using Procurement.Data;
using Procurement.Data.Secrets;

namespace Procurement.UI.Composition
{
    /// <summary>
    /// One-time move of supplier credentials from the old Procurement_Supplier.*Encrypted columns
    /// (encrypted with PROCUREMENT_SECRET_KEY) into SharedSecrets.dbo.AppSecrets (ApplicationName
    /// "Procurement"; plain-text [Value] since 18 nov 2026) — 13 nov 2026, when Procurement switched to
    /// UniPro's way of storing secrets.
    ///
    /// Runs at every startup but only does something for a secret that has a legacy value AND isn't
    /// in SharedSecrets yet, so once everything is moved it's a no-op (one cheap query). It needs
    /// PROCUREMENT_SECRET_KEY on the machine that runs it once; without PROCUREMENT_SECRET_KEY it skips and says
    /// so, and the values can simply be typed in again via Beheer > App Secrets instead.
    ///
    /// The user running the move gets a SecretAccess grant for each moved secret: they could already
    /// use these credentials before the move, and without a grant nobody could (SecretAccess denies
    /// by default) until an admin hands them out. The legacy columns are left as they are, not
    /// cleared — same caution UniPro applied to its old UniPro2026_AppSecrets table.
    /// </summary>
    public static class LegacySupplierCredentialMigration
    {
        private static readonly (string SupplierCode, Func<Supplier, string> LegacyColumn, string SecretName)[] Mapping =
        {
            ("DIGIKEY", s => s.ClientIdEncrypted, SecretSettings.DigiKeyClientIdName),
            ("DIGIKEY", s => s.ClientSecretEncrypted, SecretSettings.DigiKeyClientSecretName),
            ("DIGIKEY", s => s.RefreshTokenEncrypted, SecretSettings.DigiKeyRefreshTokenName),
            ("FARNELL", s => s.ApiKeyEncrypted, SecretSettings.FarnellApiKeyName),
            ("MOUSER", s => s.ApiKeyEncrypted, SecretSettings.MouserApiKeyName),
            // TME used to reuse ClientId for its Token and ClientSecret for its HMAC signing key.
            ("TME", s => s.ClientIdEncrypted, SecretSettings.TmeTokenName),
            ("TME", s => s.ClientSecretEncrypted, SecretSettings.TmeApiKeyName),
            ("ARROW", s => s.ClientIdEncrypted, SecretSettings.ArrowClientIdName),
            ("ARROW", s => s.ClientSecretEncrypted, SecretSettings.ArrowClientSecretName),
            ("RUTRONIK", s => s.ApiKeyEncrypted, SecretSettings.RutronikApiKeyName),
        };

        /// <summary>Returns messages for the user (empty when there was nothing to do).</summary>
        public static IList<string> Run(ProcurementDbContext context)
        {
            var messages = new List<string>();
            var repo = new AppSecretsRepository();

            var suppliers = context.Suppliers.ToList();
            var alreadyInSharedSecrets = new HashSet<string>(repo.GetAllNames(), StringComparer.OrdinalIgnoreCase);

            var pending = Mapping
                .Select(m => (m.SecretName, Legacy: m.LegacyColumn(suppliers.FirstOrDefault(s => s.SupplierCode == m.SupplierCode) ?? new Supplier())))
                .Where(x => !string.IsNullOrEmpty(x.Legacy) && !alreadyInSharedSecrets.Contains(x.SecretName))
                .ToList();

            if (pending.Count == 0)
                return messages;

            if (!SecretProtector.IsLegacyKeyAvailable)
            {
                messages.Add(
                    $"{pending.Count} supplier-credential(s) staan nog in de oude Procurement_Supplier-kolommen " +
                    $"({string.Join(", ", pending.Select(p => p.SecretName))}), maar PROCUREMENT_SECRET_KEY is niet gezet " +
                    "op deze machine, dus ze konden niet automatisch naar SharedSecrets verhuisd worden. Vul ze opnieuw " +
                    "in via Beheer > App Secrets, of zet PROCUREMENT_SECRET_KEY tijdelijk en start de app opnieuw.");
                return messages;
            }

            var moved = new List<string>();
            foreach (var item in pending)
            {
                try
                {
                    repo.Set(item.SecretName, SecretProtector.DecryptLegacy(item.Legacy));
                    moved.Add(item.SecretName);
                }
                catch (Exception ex)
                {
                    messages.Add($"'{item.SecretName}' kon niet verhuisd worden naar SharedSecrets: {ex.Message}");
                }
            }

            if (moved.Count > 0)
            {
                new SecretAccessRepository().Save(moved.Select(name => new SecretAccessEntry
                {
                    SecretName = name,
                    UserName = ProcurementSession.UserName,
                    CanRead = true
                }));

                messages.Insert(0,
                    $"{moved.Count} supplier-credential(s) zijn verhuisd naar de gedeelde SharedSecrets-database " +
                    $"({string.Join(", ", moved)}), en jouw account ({ProcurementSession.UserName}) mag ze gebruiken. " +
                    "Andere gebruikers kun je rechten geven via Beheer > App Secrets.");
            }

            return messages;
        }
    }
}
