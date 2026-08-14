using System;
using System.Security.Cryptography;
using System.Text;

namespace Procurement.Core.Security
{
    /// <summary>
    /// Encrypts/decrypts supplier API credentials (DigiKey ClientId/ClientSecret, Farnell ApiKey)
    /// before they touch the database. AES-256-CBC with a random IV per value (prepended to the
    /// ciphertext, both base64-encoded together) — the encryption key itself is never stored in the
    /// database or in source control, only read from the <see cref="KeyEnvironmentVariable"/>
    /// environment variable at the moment it's actually needed.
    ///
    /// This is deliberately NOT a full secrets manager (no rotation tooling, no per-identity access
    /// auditing the way Azure Key Vault would give) — it solves the concrete problem asked for
    /// (credentials must not sit in the database, App.config, or source control in plaintext) at
    /// near-zero extra cost over a full secrets manager. Swapping this out for Key Vault later only
    /// means changing <see cref="GetKey"/>; every caller (Supplier.ClientId/ClientSecret/ApiKey)
    /// stays the same.
    ///
    /// Note this key is for encrypting the *credentials*, not something to rotate casually: changing
    /// the environment variable's value makes every previously-encrypted value undecryptable (they'd
    /// all need re-entering via Instellingen afterward). What's meant to be easy to change often is
    /// the credentials themselves — that's the whole point of storing them here instead of in a
    /// build-time config file: updating a rotated DigiKey/Farnell key is a few clicks in
    /// Instellingen, no redeploy needed.
    /// </summary>
    public static class SecretProtector
    {
        private const string KeyEnvironmentVariable = "PROCUREMENT_SECRET_KEY";

        /// <summary>Null/empty input round-trips to null without needing the key at all — a credential that was never set stays encryption-free, so PROCUREMENT_SECRET_KEY only has to exist once something has actually been saved via Instellingen.</summary>
        public static string Encrypt(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return null;

            using (var aes = Aes.Create())
            {
                aes.Key = GetKey();
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
        public static string Decrypt(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return null;

            var combined = Convert.FromBase64String(encrypted);

            using (var aes = Aes.Create())
            {
                aes.Key = GetKey();

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

        private static byte[] GetKey()
        {
            var raw = Environment.GetEnvironmentVariable(KeyEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new InvalidOperationException(
                    $"Omgevingsvariabele '{KeyEnvironmentVariable}' is niet ingesteld. Deze bevat de sleutel " +
                    "waarmee supplier-API-credentials in de database versleuteld/ontsleuteld worden — zonder " +
                    "deze variabele kunnen opgeslagen credentials niet gelezen worden. Zet 'm op elke machine " +
                    "die deze app draait (overal dezelfde waarde), bv. als System Environment Variable of via " +
                    "GPO — nooit in App.config of in de database zelf. Zie README, sectie \"Supplier-credentials\".");
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
