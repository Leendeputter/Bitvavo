using System;
using System.Security.Cryptography;
using System.Text;

namespace Procurement.Core.Security
{
    /// <summary>
    /// Encrypts/decrypts application secrets (supplier API keys, OAuth client secrets, DigiKey's
    /// refresh token) before they touch the SharedSecrets database. AES-256-CBC with a random IV per
    /// value (prepended to the ciphertext, both base64-encoded together) — the encryption key itself
    /// is never stored in the database or in source control, only read from an environment variable
    /// at the moment it's actually needed.
    ///
    /// Identical to UniPro2026's Core/Security/SecretProtector.cs, including the key: since
    /// 13 nov 2026 (user request: "secret keys op dezelfde manier opslaan als in UniPro", with the
    /// stated goal of merging Procurement into UniPro later) this reads <see cref="KeyEnvironmentVariable"/>
    /// = UNIPRO_SECRET_KEY, the same variable UniPro uses. One key for both apps means the merge
    /// later only has to change AppSecretsRepository's ApplicationName, not re-encrypt anything.
    ///
    /// Before that, Procurement used its own PROCUREMENT_SECRET_KEY and stored values directly on
    /// Procurement_Supplier. <see cref="DecryptLegacy"/> still reads those, for the one-time move
    /// into SharedSecrets (LegacySupplierCredentialMigration) only.
    ///
    /// Note this key is for encrypting the *secrets*, not something to rotate casually: changing
    /// the environment variable's value makes every previously-encrypted value undecryptable (they'd
    /// all need re-entering via Beheer > App Secrets afterward).
    /// </summary>
    public static class SecretProtector
    {
        private const string KeyEnvironmentVariable = "UNIPRO_SECRET_KEY";

        /// <summary>Procurement's pre-13-nov-2026 key — only for reading old Procurement_Supplier.*Encrypted values once.</summary>
        private const string LegacyKeyEnvironmentVariable = "PROCUREMENT_SECRET_KEY";

        /// <summary>Null/empty input round-trips to null without needing the key at all — a secret that was never set stays encryption-free.</summary>
        public static string Encrypt(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return null;

            using (var aes = Aes.Create())
            {
                aes.Key = GetKey(KeyEnvironmentVariable);
                aes.GenerateIV();

                using (var encryptor = aes.CreateEncryptor())
                {
                    var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                    var cipherBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

                    // The IV doesn't need to be secret, only unique per encryption — prepending it to
                    // the ciphertext means Decrypt doesn't need it passed in separately.
                    var combined = new byte[aes.IV.Length + cipherBytes.Length];
                    Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
                    Buffer.BlockCopy(cipherBytes, 0, combined, aes.IV.Length, cipherBytes.Length);
                    return Convert.ToBase64String(combined);
                }
            }
        }

        /// <summary>Null/empty input (never encrypted) round-trips to null — see Encrypt.</summary>
        public static string Decrypt(string encrypted) => Decrypt(encrypted, KeyEnvironmentVariable);

        /// <summary>
        /// Decrypts a value encrypted with Procurement's old PROCUREMENT_SECRET_KEY (the
        /// Procurement_Supplier.*Encrypted columns). Only used by the one-time migration into
        /// SharedSecrets; returns null if that variable isn't set anymore, so the migration can
        /// skip instead of crash.
        /// </summary>
        public static string DecryptLegacy(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return null;
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(LegacyKeyEnvironmentVariable))) return null;
            return Decrypt(encrypted, LegacyKeyEnvironmentVariable);
        }

        public static bool IsLegacyKeyAvailable =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(LegacyKeyEnvironmentVariable));

        private static string Decrypt(string encrypted, string keyEnvironmentVariable)
        {
            if (string.IsNullOrEmpty(encrypted)) return null;

            var combined = Convert.FromBase64String(encrypted);

            using (var aes = Aes.Create())
            {
                aes.Key = GetKey(keyEnvironmentVariable);

                var iv = new byte[aes.BlockSize / 8];
                var cipherBytes = new byte[combined.Length - iv.Length];
                Buffer.BlockCopy(combined, 0, iv, 0, iv.Length);
                Buffer.BlockCopy(combined, iv.Length, cipherBytes, 0, cipherBytes.Length);
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                {
                    var plaintextBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                    return Encoding.UTF8.GetString(plaintextBytes);
                }
            }
        }

        private static byte[] GetKey(string keyEnvironmentVariable)
        {
            var raw = Environment.GetEnvironmentVariable(keyEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new InvalidOperationException(
                    $"Omgevingsvariabele '{keyEnvironmentVariable}' is niet ingesteld. Deze bevat de sleutel " +
                    "waarmee applicatie-secrets in de database versleuteld/ontsleuteld worden — zonder deze " +
                    "variabele kunnen opgeslagen secrets niet gelezen worden. Zet 'm op elke machine die deze " +
                    "app draait (overal dezelfde waarde als voor UniPro), bv. als System Environment Variable " +
                    "of via GPO — nooit in App.config of in de database zelf.");
            }

            // Any passphrase-length string is accepted (not required to be exactly 32 bytes) —
            // hashing it down to a fixed 256-bit key keeps setup simple: any random string works.
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(raw));
            }
        }
    }
}
