using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using BitvavoBot.Domain.Interfaces;

namespace BitvavoBot.Infrastructure;

/// <summary>
/// Encrypts secrets (API key/secret, fee overrides) at rest using Windows DPAPI, scoped to the
/// current Windows user, so nothing sensitive is ever written to disk in plain text (spec 11).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiCredentialProtector : ICredentialProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BitvavoBot.CredentialProtector.v1");

    public string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedText)
    {
        if (string.IsNullOrEmpty(protectedText)) return string.Empty;
        var protectedBytes = Convert.FromBase64String(protectedText);
        var plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
