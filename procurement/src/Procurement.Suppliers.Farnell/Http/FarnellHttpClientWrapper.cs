using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Interfaces;

namespace Procurement.Suppliers.Farnell.Http
{
    /// <summary>
    /// Real Farnell/element14 REST client. element14's public API authenticates via an API key
    /// passed as a query-string parameter (no OAuth), so unlike DigiKey there is no token to
    /// acquire/cache — every call just needs "callInfo.apiKey"/"callInfo.responseDataFormat"
    /// appended. Only exercised when FarnellOptions.UseMockData is explicitly set to false — not
    /// yet tried against a real account (no API key available at the time this was written).
    /// </summary>
    public class FarnellHttpClientWrapper : ISupplierHttpClient, IDisposable
    {
        private readonly FarnellOptions _options;
        private readonly HttpClient _httpClient;

        public FarnellHttpClientWrapper(FarnellOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.element14.com/")
            };
        }

        public async Task<string> GetAsync(string relativeUrl)
        {
            return await SendAsync(HttpMethod.Get, relativeUrl, null).ConfigureAwait(false);
        }

        public async Task<string> PostAsync(string relativeUrl, string jsonBody)
        {
            return await SendAsync(HttpMethod.Post, relativeUrl, jsonBody).ConfigureAwait(false);
        }

        private async Task<string> SendAsync(HttpMethod method, string relativeUrl, string jsonBody)
        {
            var url = AppendAuthQuery(relativeUrl);
            using (var request = new HttpRequestMessage(method, url))
            {
                if (jsonBody != null)
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    throw new SupplierException(FarnellAdapter.Code, SupplierErrorCode.Timeout,
                        "Could not reach Farnell/element14 API.", ex);
                }

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new SupplierException(FarnellAdapter.Code, SupplierErrorCode.AuthenticationError,
                        $"Farnell API returned {(int)response.StatusCode}: {body}");
                }
                if (response.StatusCode == (System.Net.HttpStatusCode)429)
                {
                    throw new SupplierException(FarnellAdapter.Code, SupplierErrorCode.RateLimit,
                        "Farnell API rate limit exceeded.");
                }
                if (!response.IsSuccessStatusCode)
                {
                    throw new SupplierException(FarnellAdapter.Code, SupplierErrorCode.UnknownError,
                        $"Farnell API returned {(int)response.StatusCode}: {body}");
                }

                return body;
            }
        }

        private string AppendAuthQuery(string relativeUrl)
        {
            var separator = relativeUrl.Contains("?") ? "&" : "?";
            return $"{relativeUrl}{separator}callInfo.apiKey={Uri.EscapeDataString(_options.ApiKey ?? string.Empty)}" +
                   "&callInfo.responseDataFormat=JSON" +
                   $"&storeInfo.id={Uri.EscapeDataString(_options.StoreId ?? string.Empty)}";
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
