using System.Collections.Generic;

namespace Procurement.Data.Secrets
{
    /// <summary>
    /// Reads Procurement's supplier credentials — same rule as UniPro2026's Core/Configuration/SecretSettings.cs
    /// since 18 nov 2026: SharedSecrets.dbo.AppSecrets is the only source (plain-text [Value] column,
    /// ApplicationName "Procurement", see <see cref="AppSecretsRepository"/>), which also enforces the
    /// per-user SecretAccess check. No environment-variable overrides anymore (user, 18 nov 2026:
    /// "Enkel de sharedSecrets gebruiken"). New/changed values are entered via Beheer > App Secrets.
    ///
    /// Replaces the Procurement_Supplier.ClientIdEncrypted/ClientSecretEncrypted/ApiKeyEncrypted/
    /// RefreshTokenEncrypted columns (13 nov 2026). Those columns still exist but are no longer read,
    /// except once by LegacySupplierCredentialMigration.
    /// </summary>
    public static class SecretSettings
    {
        private static readonly AppSecretsRepository _repo = new AppSecretsRepository();

        // Secret names (dbo.AppSecrets.[Name]). Kept as constants so the App Secrets screen, the
        // legacy migration and the getters below can't drift apart.
        public const string DigiKeyClientIdName = "DigiKeyClientId";
        public const string DigiKeyClientSecretName = "DigiKeyClientSecret";
        public const string DigiKeyRefreshTokenName = "DigiKeyRefreshToken";
        public const string FarnellApiKeyName = "FarnellApiKey";
        public const string MouserApiKeyName = "MouserApiKey";
        public const string TmeTokenName = "TmeToken";
        public const string TmeApiKeyName = "TmeApiKey";
        public const string ArrowClientIdName = "ArrowClientId";
        public const string ArrowClientSecretName = "ArrowClientSecret";
        public const string RutronikApiKeyName = "RutronikApiKey";

        /// <summary>
        /// Every secret this app knows, with its label on the App Secrets screen — the one place to
        /// add a new one (same role as AppSecretsForm.SecretDefinitions in UniPro). Editable marks
        /// the ones a person types in; DigiKey's refresh token is written only by "Authorize
        /// Ordering..." and by automatic rotation, so the screen shows its status but no input box.
        /// </summary>
        public static readonly IReadOnlyList<(string Name, string Label, bool Editable)> Definitions = new[]
        {
            (DigiKeyClientIdName, "DigiKey Client ID", true),
            (DigiKeyClientSecretName, "DigiKey Client Secret", true),
            (DigiKeyRefreshTokenName, "DigiKey Ordering refresh token", false),
            (FarnellApiKeyName, "Farnell API Key", true),
            (MouserApiKeyName, "Mouser API Key", true),
            (TmeTokenName, "TME Token", true),
            (TmeApiKeyName, "TME API Key (signing secret)", true),
            (ArrowClientIdName, "Arrow Client ID", true),
            (ArrowClientSecretName, "Arrow Client Secret", true),
            (RutronikApiKeyName, "Rutronik API Key", true),
        };

        public static string DigiKeyClientId => GetSecret(DigiKeyClientIdName);
        public static string DigiKeyClientSecret => GetSecret(DigiKeyClientSecretName);

        // Rotates on (almost) every use and is written back to the database automatically.
        public static string DigiKeyRefreshToken => GetSecret(DigiKeyRefreshTokenName);

        public static string FarnellApiKey => GetSecret(FarnellApiKeyName);
        public static string MouserApiKey => GetSecret(MouserApiKeyName);
        public static string TmeToken => GetSecret(TmeTokenName);
        public static string TmeApiKey => GetSecret(TmeApiKeyName);
        public static string ArrowClientId => GetSecret(ArrowClientIdName);
        public static string ArrowClientSecret => GetSecret(ArrowClientSecretName);
        public static string RutronikApiKey => GetSecret(RutronikApiKeyName);

        /// <summary>Called by DigiKeyHttpClientWrapper.OnRefreshTokenRotated and by "Authorize Ordering...".</summary>
        public static void SaveDigiKeyRefreshToken(string refreshToken) => _repo.Set(DigiKeyRefreshTokenName, refreshToken);

        private static string GetSecret(string secretName) => _repo.GetValue(secretName);
    }
}
