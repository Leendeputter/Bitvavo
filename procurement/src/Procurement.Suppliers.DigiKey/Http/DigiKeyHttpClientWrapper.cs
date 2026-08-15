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

namespace Procurement.Suppliers.DigiKey.Http
{
    /// <summary>
    /// Real DigiKey V4 REST client. Handles OAuth2 client-credentials token acquisition/caching
    /// and the required X-DIGIKEY-* headers; DigiKeyAdapter only calls GetAsync/PostAsync with a
    /// relative path and a JSON body, unaware of any of this. Only exercised when
    /// DigiKeyOptions.UseMockData is explicitly set to false — not yet tried against DigiKey's
    /// real API (no credentials available at the time this was written), so treat the token
    /// endpoint path and header names as "per public docs, unverified" until a real call succeeds.
    /// </summary>
    public class DigiKeyHttpClientWrapper : ISupplierHttpClient, IDisposable
    {
        private readonly DigiKeyOptions _options;
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _tokenLock = new SemaphoreSlim(1, 1);

        private string _accessToken;
        private DateTime _accessTokenExpiresAtUtc = DateTime.MinValue;

        public DigiKeyHttpClientWrapper(DigiKeyOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.IsSandbox
                    ? "https://sandbox-api.digikey.com/"
                    : "https://api.digikey.com/")
            };
        }

        public async Task<string> GetAsync(string relativeUrl)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl))
            {
                return await SendAsync(request).ConfigureAwait(false);
            }
        }

        public async Task<string> PostAsync(string relativeUrl, string jsonBody)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl))
            {
                request.Content = new StringContent(jsonBody ?? string.Empty, Encoding.UTF8, "application/json");
                return await SendAsync(request).ConfigureAwait(false);
            }
        }

        private async Task<string> SendAsync(HttpRequestMessage request)
        {
            var token = await GetAccessTokenAsync().ConfigureAwait(false);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("X-DIGIKEY-Client-Id", _options.ClientId);
            request.Headers.Add("X-DIGIKEY-Locale-Site", _options.LocaleSite);
            request.Headers.Add("X-DIGIKEY-Locale-Language", _options.LocaleLanguage);
            request.Headers.Add("X-DIGIKEY-Locale-Currency", _options.LocaleCurrency);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.Timeout,
                    "Could not reach DigiKey API.", ex);
            }

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                    $"DigiKey API returned {(int)response.StatusCode}: {body}");
            }
            if (response.StatusCode == (System.Net.HttpStatusCode)429)
            {
                throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.RateLimit,
                    "DigiKey API rate limit exceeded.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.UnknownError,
                    $"DigiKey API returned {(int)response.StatusCode}: {body}");
            }

            return body;
        }

        // DigiKey's OAuth2 client-credentials token, cached in memory and refreshed a minute
        // before it actually expires so a request never races the expiry.
        private async Task<string> GetAccessTokenAsync()
        {
            if (_accessToken != null && DateTime.UtcNow < _accessTokenExpiresAtUtc)
                return _accessToken;

            await _tokenLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_accessToken != null && DateTime.UtcNow < _accessTokenExpiresAtUtc)
                    return _accessToken;

                var tokenUrl = _options.IsSandbox
                    ? "https://sandbox-api.digikey.com/v1/oauth2/token"
                    : "https://api.digikey.com/v1/oauth2/token";

                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", _options.ClientId),
                    new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                });

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.PostAsync(tokenUrl, form).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.Timeout,
                        "Could not reach DigiKey OAuth2 token endpoint.", ex);
                }

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                        $"DigiKey OAuth2 token request failed ({(int)response.StatusCode}): {body}");
                }

                var token = JsonConvert.DeserializeObject<DigiKeyTokenResponse>(body);
                if (string.IsNullOrEmpty(token?.AccessToken))
                {
                    throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                        "DigiKey OAuth2 token response did not contain an access_token.");
                }

                _accessToken = token.AccessToken;
                _accessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(0, token.ExpiresInSeconds - 60));
                return _accessToken;
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
