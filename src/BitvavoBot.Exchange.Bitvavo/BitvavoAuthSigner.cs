using System.Security.Cryptography;
using System.Text;

namespace BitvavoBot.Exchange.Bitvavo;

/// <summary>
/// Implements Bitvavo's request signing scheme: HMAC-SHA256 over
/// "{timestamp}{method}{path}{body}" using the API secret, hex-encoded.
/// See https://docs.bitvavo.com/ (Authentication).
/// </summary>
public sealed class BitvavoAuthSigner
{
    private readonly BitvavoOptions _options;

    public BitvavoAuthSigner(BitvavoOptions options)
    {
        _options = options;
    }

    public IReadOnlyDictionary<string, string> CreateAuthHeaders(long timestampMs, string method, string pathWithQuery, string body)
    {
        var message = $"{timestampMs}{method}/v2{pathWithQuery}{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.ApiSecret));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var signature = Convert.ToHexString(signatureBytes).ToLowerInvariant();

        return new Dictionary<string, string>
        {
            ["Bitvavo-Access-Key"] = _options.ApiKey,
            ["Bitvavo-Access-Signature"] = signature,
            ["Bitvavo-Access-Timestamp"] = timestampMs.ToString(),
            ["Bitvavo-Access-Window"] = _options.AccessWindowMs.ToString()
        };
    }
}
