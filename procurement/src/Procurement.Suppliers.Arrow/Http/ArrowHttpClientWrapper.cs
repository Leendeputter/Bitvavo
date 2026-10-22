using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Interfaces;

namespace Procurement.Suppliers.Arrow.Http
{
    /// <summary>
    /// Real MyArrow API client (my.arrow.com/api/) — OAuth2 client_credentials, same shape as
    /// DigiKey's 2-legged flow (see DigiKeyHttpClientWrapper.GetAccessTokenAsync, the model this
    /// mirrors): POST security/oauth/token with Basic auth (username=client_id,
    /// password=client_secret) plus a repeated "client_id" header, returning an access_token used
    /// as a Bearer token on every subsequent call, alongside the same "client_id" header again and
    /// "accept: application/json" (all confirmed by the spec pasted by the user, 22 okt 2026).
    /// Token is cached in memory and refreshed a minute before it actually expires, same as
    /// DigiKey's. Only exercised when ArrowOptions.UseMockData is explicitly set to false — and
    /// even then, see ArrowAdapter's own doc comment: this class handles auth correctly, but the
    /// adapter's real-mode search/pricing/order-placement methods still refuse to guess the
    /// undocumented per-part/per-line response field names, so flipping UseMockData off alone does
    /// not make Arrow fully operational yet.
    /// </summary>
    public class ArrowHttpClientWrapper : ISupplierHttpClient, IDisposable
    {
        private readonly ArrowOptions _options;
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _tokenLock = new SemaphoreSlim(1, 1);

        private string _accessToken;
        private DateTime _accessTokenExpiresAtUtc = DateTime.MinValue;

        public ArrowHttpClientWrapper(ArrowOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://my.arrow.com/api/")
            };
        }

        public async Task<string> GetAsync(string relativeUrl)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl))
            {
                await AttachHeadersAsync(request).ConfigureAwait(false);
                return await SendAsync(request).ConfigureAwait(false);
            }
        }

        public async Task<string> PostAsync(string relativeUrl, string jsonBody)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl))
            {
                request.Content = new StringContent(jsonBody ?? string.Empty, Encoding.UTF8, "application/json");
                await AttachHeadersAsync(request).ConfigureAwait(false);
                return await SendAsync(request).ConfigureAwait(false);
            }
        }

        private async Task AttachHeadersAsync(HttpRequestMessage request)
        {
            var token = await GetAccessTokenAsync().ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("client_id", _options.ClientId);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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
                throw new SupplierException("ARROW", SupplierErrorCode.Timeout,
                    "Could not reach MyArrow API.", ex);
            }

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new SupplierException("ARROW", SupplierErrorCode.AuthenticationError,
                    $"MyArrow API returned {(int)response.StatusCode}: {body}");
            }
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                // MyArrow's own rate-limit doc: "a 403 will be returned" when the 3 req/s or
                // 120-per-3-minute limit is exceeded — same status code as a plain auth failure,
                // and nothing in the spec's error body distinguishes the two, so this is reported
                // as RateLimit (the more actionable of the two to show a user) rather than
                // AuthenticationError; worth revisiting once a real 403 body has actually been seen.
                throw new SupplierException("ARROW", SupplierErrorCode.RateLimit,
                    $"MyArrow API returned 403 (auth failure or rate limit exceeded — indistinguishable per Arrow's own docs): {body}");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new SupplierException("ARROW", SupplierErrorCode.UnknownError,
                    $"MyArrow API returned {(int)response.StatusCode}: {body}");
            }

            return body;
        }

        // OAuth2 client-credentials token, cached in memory and refreshed a minute before it
        // actually expires — same pattern as DigiKeyHttpClientWrapper.GetAccessTokenAsync.
        private async Task<string> GetAccessTokenAsync()
        {
            if (_accessToken != null && DateTime.UtcNow < _accessTokenExpiresAtUtc)
                return _accessToken;

            await _tokenLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_accessToken != null && DateTime.UtcNow < _accessTokenExpiresAtUtc)
                    return _accessToken;

                using (var request = new HttpRequestMessage(HttpMethod.Post, "security/oauth/token"))
                {
                    var credentials = Convert.ToBase64String(
                        Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                    request.Headers.Add("client_id", _options.ClientId);
                    request.Content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "client_credentials")
                    });

                    HttpResponseMessage response;
                    try
                    {
                        response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                    }
                    catch (HttpRequestException ex)
                    {
                        throw new SupplierException("ARROW", SupplierErrorCode.Timeout,
                            "Could not reach MyArrow OAuth2 token endpoint.", ex);
                    }

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new SupplierException("ARROW", SupplierErrorCode.AuthenticationError,
                            $"MyArrow OAuth2 token request failed ({(int)response.StatusCode}): {body}");
                    }

                    var token = JsonConvert.DeserializeObject<ArrowTokenResponse>(body);
                    if (string.IsNullOrEmpty(token?.AccessToken))
                    {
                        throw new SupplierException("ARROW", SupplierErrorCode.AuthenticationError,
                            "MyArrow OAuth2 token response did not contain an access_token.");
                    }

                    _accessToken = token.AccessToken;
                    _accessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(0, token.ExpiresInSeconds - 60));
                    return _accessToken;
                }
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _tokenLock?.Dispose();
        }
    }
}
