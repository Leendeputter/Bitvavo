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
    /// Real Mouser API client — Search, Cart and Order all live under the same host
    /// (api.mouser.com) and the same single apiKey-query-string auth (no OAuth), confirmed by the
    /// user-supplied swagger spec and official API guide (21 okt 2026): "your new API Key will
    /// show up for you to use with the Mouser Cart and Order APIs" — superseding this class's
    /// earlier assumption of a separate Order API key. Only exercised when
    /// MouserOptions.UseMockData is explicitly set to false. One important caveat from that same
    /// guide, worth repeating everywhere this matters: Mouser's API Explorer (and, by the same
    /// logic, this real-mode path) is *not* a sandbox — every order actually submitted here lands
    /// on the real account.
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

        public async Task<string> GetAsync(string relativeUrl)
        {
            // The Search API (SearchAsync in MouserAdapter) never calls this — every one of its
            // operations is POST-only. The Cart/Order API does need GET (e.g. order/currencies,
            // orderhistory/salesOrderNumber), confirmed from the Mouser swagger spec the user
            // pasted (21 okt 2026) — same host, same apiKey-query-string auth as POST, so no
            // separate client/interface needed the way DigiKey/Farnell's ordering host required.
            var url = AppendApiKey(relativeUrl);
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                return await SendAsync(request).ConfigureAwait(false);
            }
        }

        public async Task<string> PostAsync(string relativeUrl, string jsonBody)
        {
            var url = AppendApiKey(relativeUrl);
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new StringContent(jsonBody ?? string.Empty, Encoding.UTF8, "application/json");
                return await SendAsync(request).ConfigureAwait(false);
            }
        }

        private async Task<string> SendAsync(HttpRequestMessage request)
        {
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
