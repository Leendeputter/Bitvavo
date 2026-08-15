using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Interfaces;

namespace Procurement.Suppliers.TME.Http
{
    /// <summary>
    /// Real TME REST client. TME signs every request with HMAC-SHA1 over a canonical
    /// "POST&amp;{url}&amp;{sorted params}" string (an OAuth1-style scheme), rather than a bearer
    /// token or simple API-key query parameter like the other suppliers in this project — this is
    /// reproduced here from memory of TME's documented signing approach, NOT verified against a
    /// real call (no TME account available at the time this was written). If real calls come back
    /// with an authentication/signature error, re-check this against TME's actual API docs before
    /// assuming anything else is wrong.
    ///
    /// TmeAdapter calls PostAsync(relativeUrl, jsonBody) where jsonBody is a JSON-encoded
    /// Dictionary&lt;string,string&gt; of just the action-specific parameters (e.g. "SearchPlain",
    /// "SymbolList[0]") — this wrapper adds Token/Country/Language/Currency, signs, and posts the
    /// combined form.
    /// </summary>
    public class TmeHttpClientWrapper : ISupplierHttpClient, IDisposable
    {
        private readonly TmeOptions _options;
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public TmeHttpClientWrapper(TmeOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _baseUrl = options.IsSandbox ? "https://apitest.tme.eu/" : "https://api.tme.eu/";
            _httpClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        }

        public Task<string> GetAsync(string relativeUrl)
        {
            throw new NotSupportedException("TmeHttpClientWrapper only supports POST (TME's API is POST-only) — use PostAsync.");
        }

        public async Task<string> PostAsync(string relativeUrl, string jsonBody)
        {
            var actionParams = string.IsNullOrEmpty(jsonBody)
                ? new Dictionary<string, string>()
                : JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonBody);

            var allParams = new Dictionary<string, string>(actionParams)
            {
                ["Token"] = _options.Token,
                ["Country"] = _options.Country,
                ["Language"] = _options.Language,
                ["Currency"] = _options.Currency
            };

            var apiUrl = _baseUrl + relativeUrl;
            var signature = ComputeSignature(apiUrl, allParams);
            allParams["ApiSignature"] = signature;

            using (var content = new FormUrlEncodedContent(allParams))
            {
                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.PostAsync(relativeUrl, content).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    throw new SupplierException(TmeAdapter.Code, SupplierErrorCode.Timeout,
                        "Could not reach TME API.", ex);
                }

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new SupplierException(TmeAdapter.Code, SupplierErrorCode.AuthenticationError,
                        $"TME API returned {(int)response.StatusCode}: {body}");
                }
                if (response.StatusCode == (System.Net.HttpStatusCode)429)
                {
                    throw new SupplierException(TmeAdapter.Code, SupplierErrorCode.RateLimit,
                        "TME API rate limit exceeded.");
                }
                if (!response.IsSuccessStatusCode)
                {
                    throw new SupplierException(TmeAdapter.Code, SupplierErrorCode.UnknownError,
                        $"TME API returned {(int)response.StatusCode}: {body}");
                }

                return body;
            }
        }

        // TME's documented signing scheme (OAuth1-style): sort all parameters by key (ordinal),
        // percent-encode key=value pairs per RFC3986, join with '&', then sign
        // "POST&{urlEncode(apiUrl)}&{urlEncode(paramString)}" with HMAC-SHA1 using the private
        // ApiKey as the secret, base64-encoding the result. Internal (not private) so
        // TmeSignatureTests can verify this against an independently-computed (Python) reference
        // vector — this is the least-verified mechanism in the whole supplier layer, worth pinning
        // down with a cross-language test even though it can't prove TME's server accepts it.
        internal string ComputeSignature(string apiUrl, Dictionary<string, string> parameters)
        {
            var paramString = string.Join("&", parameters
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{TmeEncode(kv.Key)}={TmeEncode(kv.Value ?? string.Empty)}"));

            var signatureBase = $"POST&{TmeEncode(apiUrl)}&{TmeEncode(paramString)}";

            using (var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_options.ApiKey ?? string.Empty)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureBase));
                return Convert.ToBase64String(hash);
            }
        }

        // Uri.EscapeDataString is RFC3986-compliant (unlike WebUtility.UrlEncode, which is
        // form-encoding/RFC1738 and would produce '+' for spaces instead of '%20').
        private static string TmeEncode(string value) => Uri.EscapeDataString(value ?? string.Empty);

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
