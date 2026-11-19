using System;
using System.Collections.Generic;
using System.Linq;
using MAX50;
using Procurement.Core.Models;
using Procurement.Data.Secrets;
using Procurement.Services.Login;

namespace Procurement.Services.Options
{
    /// <summary>One secret as App Secrets shows it: only whether it is set, never the value.</summary>
    public class SecretStatusRow
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public bool Editable { get; set; }
        public bool IsSet { get; set; }
    }

    /// <summary>A new value typed in for one secret.</summary>
    public class SecretValue
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// Beheer > App Secrets for the web (19 nov 2026), the same rules as the desktop's AppSecretsForm:
    /// secrets are write-only (a saved value is never sent back, an empty box leaves the stored value
    /// alone), plus the SecretAccess rights and "copy rights from user X to Y". Every call demands
    /// MGR User Security first, as the desktop's Beheer menu does; the web checks it on the server
    /// for each request, not only by hiding the menu.
    /// </summary>
    public class AppSecretsScreenService
    {
        private readonly ProcurementServices _services;
        private readonly AppSecretsRepository _secrets;
        private readonly SecretAccessRepository _access;

        public AppSecretsScreenService(ProcurementServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            MaxRights.DemandAccess(services.Session, AccessRights.MgrUserSecurity, SecLevel.USERSEC_READWRITE);
            _secrets = new AppSecretsRepository(services.Session);
            _access = new SecretAccessRepository(services.Session);
        }

        /// <summary>For hiding the Beheer menu; a failing lookup counts as "no".</summary>
        public static bool HasAccess(ProcurementServices services)
        {
            try
            {
                return MaxRights.HasAccess(services.Session, AccessRights.MgrUserSecurity, SecLevel.USERSEC_READWRITE);
            }
            catch
            {
                return false;
            }
        }

        public IReadOnlyList<string> SecretNames => SecretSettings.Definitions.Select(d => d.Name).ToList();

        public IReadOnlyList<SecretStatusRow> GetStatuses()
        {
            var namesWithValue = new HashSet<string>(_secrets.GetAllNames());
            return SecretSettings.Definitions.Select(d => new SecretStatusRow
            {
                Name = d.Name,
                Label = d.Label,
                Editable = d.Editable,
                IsSet = namesWithValue.Contains(d.Name)
            }).ToList();
        }

        /// <summary>Only editable secrets, only filled-in values; returns how many were written.</summary>
        public int SaveSecrets(IEnumerable<SecretValue> newValues)
        {
            var editable = new HashSet<string>(SecretSettings.Definitions.Where(d => d.Editable).Select(d => d.Name));
            var provided = new Dictionary<string, string>();
            foreach (var v in newValues ?? Enumerable.Empty<SecretValue>())
                if (v != null && v.Name != null && editable.Contains(v.Name) && !string.IsNullOrEmpty(v.Value))
                    provided[v.Name] = v.Value;
            _secrets.UpdateIfProvided(provided);
            return provided.Count;
        }

        public IReadOnlyList<SecretAccessEntry> GetAccess() => _access.GetAll();

        public void SaveAccess(IEnumerable<SecretAccessEntry> entries) =>
            _access.Save((entries ?? Enumerable.Empty<SecretAccessEntry>())
                .Where(x => !string.IsNullOrWhiteSpace(x.SecretName) && !string.IsNullOrWhiteSpace(x.UserName)));

        public void DeleteAccess(string secretName, string userName) => _access.Delete(secretName, userName);

        /// <summary>Copies all rights of one user to one or more users (comma- or semicolon-separated); returns how many got rights.</summary>
        public int CopyAccess(string fromUser, string toUsers)
        {
            if (string.IsNullOrWhiteSpace(fromUser) || string.IsNullOrWhiteSpace(toUsers))
                throw new InvalidOperationException("Vul zowel 'From' als 'To' in.");
            return toUsers
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(u => u.Trim())
                .Where(u => u.Length > 0)
                .Count(u => _access.CopyAccess(fromUser.Trim(), u, _services.Session.UserName));
        }
    }
}
