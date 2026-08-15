using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Interfaces;

namespace Procurement.Suppliers.Mouser.Http
{
    /// <summary>
    /// Real Mouser Search API v1 client. Mouser authenticates with a single API key passed as a
    /// query-string parameter (no OAuth) — simpler than DigiKey, similar to Farnell. Only
    /// exercised when MouserOptions.UseMockData is explicitly set to false — not yet tried against
    /// a real account (no API key available at the time this was written).
    /// </summary>
    public class MouserHttpClientWrapper : ISupplierHttpClient, IDisposable
    {
        private readonly MouserOptions _options;
        private readonly HttpClient _httpClient;

        public MouserHttpClientWrapper(MouserOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.mouser.com/")
            };
        }

        public Task<string> GetAsync(string relativeUrl)
        {
            // Mouser's Search API is POST-only for every operation used here.
            throw new NotSupportedException("MouserHttpClientWrapper only supports POST — use PostAsync.");
        }

        public async Task<string> PostAsync(string relativeUrl, string jsonBody)
        {
            var url = AppendApiKey(relativeUrl);
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new StringContent(jsonBody ?? string.Empty, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    throw new SupplierException(MouserAdapter.Code, SupplierErrorCode.Timeout,
                        "Could not reach Mouser API.", ex);
                }

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new SupplierException(MouserAdapter.Code, SupplierErrorCode.AuthenticationError,
                        $"Mouser API returned {(int)response.StatusCode}: {body}");
                }
                if (response.StatusCode == (System.Net.HttpStatusCode)429)
                {
                    throw new SupplierException(MouserAdapter.Code, SupplierErrorCode.RateLimit,
                        "Mouser API rate limit exceeded.");
                }
                if (!response.IsSuccessStatusCode)
                {
                    throw new SupplierException(MouserAdapter.Code, SupplierErrorCode.UnknownError,
                        $"Mouser API returned {(int)response.StatusCode}: {body}");
                }

                return body;
            }
        }

        private string AppendApiKey(string relativeUrl)
        {
            var separator = relativeUrl.Contains("?") ? "&" : "?";
            return $"{relativeUrl}{separator}apiKey={Uri.EscapeDataString(_options.ApiKey ?? string.Empty)}";
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
