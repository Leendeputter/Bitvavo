using System;
using System.Security.Cryptography;
using System.Text;

namespace Procurement.Core.Security
{
    /// <summary>
    /// Only what's left of Procurement's old encryption: reading the pre-13-nov-2026
    /// Procurement_Supplier.*Encrypted columns (encrypted with PROCUREMENT_SECRET_KEY) once, for
    /// LegacySupplierCredentialMigration. Since 18 nov 2026 SharedSecrets stores values as plain text
    /// in [Value] (same as UniPro, which removed its SecretProtector and UNIPRO_SECRET_KEY that day),
    /// so nothing is encrypted anymore.
    /// </summary>
    public static class SecretProtector
    {
        /// <summary>Procurement's pre-13-nov-2026 key — only for reading old Procurement_Supplier.*Encrypted values once.</summary>
        private const string LegacyKeyEnvironmentVariable = "PROCUREMENT_SECRET_KEY";

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
                    $"Omgevingsvariabele '{keyEnvironmentVariable}' is niet ingesteld. Alleen nodig voor de " +
                    "eenmalige overzetting van oude leverancierscredentials naar SharedSecrets.");
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
