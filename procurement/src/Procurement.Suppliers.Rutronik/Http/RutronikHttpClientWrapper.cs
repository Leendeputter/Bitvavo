using System;
using System.Net.Http;
using System.Threading.Tasks;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Interfaces;

namespace Procurement.Suppliers.Rutronik.Http
{
    /// <summary>
    /// Real Rutronik24 Shop API client (www.rutronik24.com/api/) — a pure read-only, GET-only
    /// webshop search API, auth via a single "apikey" query-string parameter (no OAuth, no
    /// secret). Confirmed by both documents the user pasted (22 okt 2026): a plain-text overview
    /// plus the official PDF manual (v1.3) — no order/cart/status endpoint exists anywhere in
    /// either document, matching the user's own conclusion ("het lijkt er op dat we daar ook
    /// alleen kunnen zoeken"). Only exercised when RutronikOptions.UseMockData is false.
    /// </summary>
    public class RutronikHttpClientWrapper : ISupplierHttpClient, IDisposable
    {
        private readonly RutronikOptions _options;
        private readonly HttpClient _httpClient;

        public RutronikHttpClientWrapper(RutronikOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://www.rutronik24.com/")
            };
        }

        public async Task<string> GetAsync(string relativeUrl)
        {
            var url = AppendApiKey(relativeUrl);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(url).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new SupplierException("RUTRONIK", SupplierErrorCode.Timeout,
                    "Could not reach Rutronik24 API.", ex);
            }

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new SupplierException("RUTRONIK", SupplierErrorCode.AuthenticationError,
                    $"Rutronik24 API returned {(int)response.StatusCode}: {body}");
            }
            if (response.StatusCode == (System.Net.HttpStatusCode)429)
            {
                throw new SupplierException("RUTRONIK", SupplierErrorCode.RateLimit,
                    "Rutronik24 API rate limit exceeded.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new SupplierException("RUTRONIK", SupplierErrorCode.UnknownError,
                    $"Rutronik24 API returned {(int)response.StatusCode}: {body}");
            }

            return body;
        }

        /// <summary>Neither document describes a single POST endpoint — the whole API is GET/query-string based, so this is never expected to be called.</summary>
        public Task<string> PostAsync(string relativeUrl, string jsonBody)
        {
            throw new NotSupportedException("The Rutronik24 Shop API has no POST endpoints — it is read-only search/pricing over GET.");
        }

        private string AppendApiKey(string relativeUrl)
        {
            var separator = relativeUrl.Contains("?") ? "&" : "?";
            return $"{relativeUrl}{separator}apikey={Uri.EscapeDataString(_options.ApiKey ?? string.Empty)}";
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
