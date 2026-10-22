using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Interfaces;

namespace Procurement.Suppliers.Farnell.Http
{
    /// <summary>
    /// Real Farnell/element14 REST client. The Product Search API (api.element14.com) authenticates
    /// via an API key passed as a query-string parameter (no OAuth), so unlike DigiKey there is no
    /// token to acquire/cache for search/pricing — every call just needs
    /// "callInfo.apiKey"/"callInfo.responseDataFormat" appended. Only exercised when
    /// FarnellOptions.UseMockData is explicitly set to false — not yet tried against a real account
    /// (no API key available at the time this was written).
    ///
    /// The separate Order API (api.farnell.com, spec pasted by the user 21 okt 2026 — see README,
    /// since partner.element14.com is blocked for this session) is a different host with a
    /// different auth scheme entirely: an "api_key" header plus a per-call JWT minted via
    /// /auth/token. Unlike DigiKey's long-lived, rotating OAuth refresh token (persisted to
    /// Supplier.RefreshTokenEncrypted, managed internally by DigiKeyHttpClientWrapper), Farnell's
    /// JWT has no documented refresh/expiry story at all — so this deliberately does NOT cache or
    /// manage it internally the way DigiKeyHttpClientWrapper does. FarnellAdapter mints one fresh
    /// JWT at the start of each order-placement attempt (AuthenticateOrderingAsync) and threads it
    /// explicitly through GetOrderingAsync/PostOrderingAsync — simpler to reason about than an
    /// implicit cache with unknown expiry semantics, at the cost of one extra call per order vs.
    /// DigiKey's reuse-until-it-fails approach.
    /// </summary>
    public class FarnellHttpClientWrapper : IFarnellOrderingHttpClient, IDisposable
    {
        private readonly FarnellOptions _options;
        private readonly HttpClient _httpClient;
        private readonly HttpClient _orderingHttpClient;

        public FarnellHttpClientWrapper(FarnellOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.element14.com/")
            };
            _orderingHttpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.farnell.com/")
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

        /// <summary>
        /// POST /auth/token with the "api_key" header and a {"tradeAccount": "..."} body; the spec's
        /// documented success response is {"Authorization": "string"} — that string is the JWT to
        /// pass as a bearer token on every subsequent ordering call.
        ///
        /// Assumes the Order API's "api_key" header is the same FarnellOptions.ApiKey already used
        /// for the Product Search API — unconfirmed, since the docs repeatedly point at a separate
        /// "Order API Registration" page without saying whether that issues a new key or reuses the
        /// existing one. Cheap to fix later (one line) once that's confirmed against a real account;
        /// a wrong key here just fails fast with a 401, not a silent/wrong order.
        /// </summary>
        public async Task<string> AuthenticateOrderingAsync(string tradeAccount)
        {
            var body = Newtonsoft.Json.JsonConvert.SerializeObject(new { tradeAccount });
            var responseJson = await SendOrderingAsync(HttpMethod.Post, "auth/token", body, jwt: null).ConfigureAwait(false);
            var dto = Deserialize<FarnellAuthTokenResponse>(responseJson);
            if (string.IsNullOrEmpty(dto?.Authorization))
            {
                throw new SupplierException(FarnellAdapter.Code, SupplierErrorCode.AuthenticationError,
                    "Farnell /auth/token did not return an Authorization token.");
            }
            return dto.Authorization;
        }

        public Task<string> GetOrderingAsync(string relativeUrl, string jwt) =>
            SendOrderingAsync(HttpMethod.Get, relativeUrl, null, jwt);

        public Task<string> PostOrderingAsync(string relativeUrl, string jsonBody, string jwt) =>
            SendOrderingAsync(HttpMethod.Post, relativeUrl, jsonBody, jwt);

        private async Task<string> SendOrderingAsync(HttpMethod method, string relativeUrl, string jsonBody, string jwt)
        {
            using (var request = new HttpRequestMessage(method, relativeUrl))
            {
                request.Headers.Add("api_key", _options.ApiKey ?? string.Empty);
                if (jwt != null)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
                if (jsonBody != null)
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                try
                {
                    response = await _orderingHttpClient.SendAsync(request).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    throw new SupplierException(FarnellAdapter.Code, SupplierErrorCode.Timeout,
                        "Could not reach Farnell Order API.", ex);
                }

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new SupplierException(FarnellAdapter.Code, SupplierErrorCode.AuthenticationError,
                        $"Farnell Order API returned {(int)response.StatusCode}: {body}");
                }
                if (response.StatusCode == (System.Net.HttpStatusCode)429)
                {
                    throw new SupplierException(FarnellAdapter.Code, SupplierErrorCode.RateLimit,
                        "Farnell Order API rate limit exceeded.");
                }
                if (!response.IsSuccessStatusCode)
                {
                    throw new SupplierException(FarnellAdapter.Code, SupplierErrorCode.UnknownError,
                        $"Farnell Order API returned {(int)response.StatusCode}: {body}");
                }

                return body;
            }
        }

        private static T Deserialize<T>(string json) where T : class
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                throw new SupplierException(FarnellAdapter.Code, SupplierErrorCode.UnknownError,
                    "Could not parse Farnell Order API response.", ex);
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _orderingHttpClient?.Dispose();
        }
    }
}
