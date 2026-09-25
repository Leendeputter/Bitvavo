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

namespace Procurement.Suppliers.DigiKey.Http
{
    /// <summary>
    /// Real DigiKey REST client, covering two unrelated OAuth2 flows: 2-legged client-credentials
    /// for Product Information V4 (GetAsync/PostAsync, confirmed working against the sandbox) and
    /// 3-legged Authorization Code for Ordering v3 (PostOrderAsync) — DigiKey's own Ordering spec
    /// requires the latter explicitly ("This API uses 3 legged OAuth"), so it needs its own token
    /// acquisition (via a stored refresh token, never a fresh interactive consent — that's
    /// DigiKeyOrderingAuthorizer's job, run once from Instellingen) entirely separate from the
    /// client-credentials token the 2-legged calls use.
    /// </summary>
    public class DigiKeyHttpClientWrapper : IDigiKeyOrderingHttpClient, IDisposable
    {
        private readonly DigiKeyOptions _options;
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _tokenLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _orderingTokenLock = new SemaphoreSlim(1, 1);

        private string _accessToken;
        private DateTime _accessTokenExpiresAtUtc = DateTime.MinValue;

        private string _orderingAccessToken;
        private DateTime _orderingAccessTokenExpiresAtUtc = DateTime.MinValue;

        /// <summary>
        /// Invoked whenever refreshing the Ordering access token comes back with a different
        /// refresh_token than the one just used — some OAuth providers rotate it on every use, and
        /// without persisting the new value the old (by then invalid) one would still be in the
        /// database next time the app runs. Wired up by CompositionRoot to
        /// SupplierRepository.UpdateRefreshTokenAsync; left null this never fires and rotation is
        /// silently lost at process exit (acceptable for tests, not for the real app).
        /// </summary>
        public Action<string> OnRefreshTokenRotated { get; set; }

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
                await AttachSearchHeadersAsync(request).ConfigureAwait(false);
                return await SendCoreAsync(request).ConfigureAwait(false);
            }
        }

        public async Task<string> PostAsync(string relativeUrl, string jsonBody)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl))
            {
                request.Content = new StringContent(jsonBody ?? string.Empty, Encoding.UTF8, "application/json");
                await AttachSearchHeadersAsync(request).ConfigureAwait(false);
                return await SendCoreAsync(request).ConfigureAwait(false);
            }
        }

        /// <summary>POST /Ordering/v3/Orders — Ordering's own parameter list (per its Swagger spec) only ever names Authorization + X-DIGIKEY-Client-Id, unlike Product Information's extra X-DIGIKEY-Locale-* headers, so those aren't sent here.</summary>
        public async Task<string> PostOrderAsync(string relativeUrl, string jsonBody)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl))
            {
                request.Content = new StringContent(jsonBody ?? string.Empty, Encoding.UTF8, "application/json");
                var token = await GetOrderingAccessTokenAsync().ConfigureAwait(false);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("X-DIGIKEY-Client-Id", _options.ClientId);
                return await SendCoreAsync(request).ConfigureAwait(false);
            }
        }

        private async Task AttachSearchHeadersAsync(HttpRequestMessage request)
        {
            var token = await GetAccessTokenAsync().ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("X-DIGIKEY-Client-Id", _options.ClientId);
            request.Headers.Add("X-DIGIKEY-Locale-Site", _options.LocaleSite);
            request.Headers.Add("X-DIGIKEY-Locale-Language", _options.LocaleLanguage);
            request.Headers.Add("X-DIGIKEY-Locale-Currency", _options.LocaleCurrency);
        }

        private async Task<string> SendCoreAsync(HttpRequestMessage request)
        {
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
                    $"DigiKey API returned {(int)response.StatusCode}: {DescribeError(body)}");
            }
            if (response.StatusCode == (System.Net.HttpStatusCode)429)
            {
                throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.RateLimit,
                    "DigiKey API rate limit exceeded.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.UnknownError,
                    $"DigiKey API returned {(int)response.StatusCode}: {DescribeError(body)}");
            }

            return body;
        }

        /// <summary>
        /// Ordering's error responses follow a confirmed shape (ApiErrorResponse: ErrorMessage/
        /// ErrorDetails/RequestId/ValidationErrors — from the real Ordering v3 spec, unlike the rest
        /// of this class) worth surfacing directly instead of dumping the raw JSON body; falls back
        /// to the raw body untouched if it doesn't parse as that shape (e.g. Product Information's
        /// own errors, never confirmed against this exact structure).
        /// </summary>
        private static string DescribeError(string body)
        {
            try
            {
                var error = JsonConvert.DeserializeObject<DigiKeyApiErrorResponseDto>(body);
                if (error == null || string.IsNullOrEmpty(error.ErrorMessage)) return body;

                var description = error.ErrorMessage;
                if (!string.IsNullOrEmpty(error.ErrorDetails)) description += $" ({error.ErrorDetails})";
                if (error.ValidationErrors != null && error.ValidationErrors.Count > 0)
                {
                    var fields = string.Join("; ", error.ValidationErrors.ConvertAll(v => $"{v.Field}: {v.Message}"));
                    description += $" — {fields}";
                }
                if (!string.IsNullOrEmpty(error.RequestId)) description += $" [RequestId {error.RequestId}]";
                return description;
            }
            catch (JsonException)
            {
                return body;
            }
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

                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", _options.ClientId),
                    new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                });

                var token = await RequestTokenAsync(form).ConfigureAwait(false);
                _accessToken = token.AccessToken;
                _accessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(0, token.ExpiresInSeconds - 60));
                return _accessToken;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        /// <summary>Ordering's own 3-legged access token, refreshed from the stored RefreshToken — never acquired interactively here (see DigiKeyOrderingAuthorizer for the one-time consent flow that produces the first RefreshToken).</summary>
        private async Task<string> GetOrderingAccessTokenAsync()
        {
            if (_orderingAccessToken != null && DateTime.UtcNow < _orderingAccessTokenExpiresAtUtc)
                return _orderingAccessToken;

            await _orderingTokenLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_orderingAccessToken != null && DateTime.UtcNow < _orderingAccessTokenExpiresAtUtc)
                    return _orderingAccessToken;

                if (string.IsNullOrEmpty(_options.RefreshToken))
                {
                    throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                        "DigiKey Ordering is nog niet geautoriseerd — gebruik \"Ordering autoriseren...\" in Instellingen voordat je een order plaatst.");
                }

                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", _options.ClientId),
                    new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", _options.RefreshToken)
                });

                var token = await RequestTokenAsync(form).ConfigureAwait(false);
                _orderingAccessToken = token.AccessToken;
                _orderingAccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(0, token.ExpiresInSeconds - 60));

                if (!string.IsNullOrEmpty(token.RefreshToken) && token.RefreshToken != _options.RefreshToken)
                {
                    _options.RefreshToken = token.RefreshToken;
                    OnRefreshTokenRotated?.Invoke(token.RefreshToken);
                }

                return _orderingAccessToken;
            }
            finally
            {
                _orderingTokenLock.Release();
            }
        }

        private async Task<DigiKeyTokenResponse> RequestTokenAsync(FormUrlEncodedContent form)
        {
            var tokenUrl = _options.IsSandbox
                ? "https://sandbox-api.digikey.com/v1/oauth2/token"
                : "https://api.digikey.com/v1/oauth2/token";

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
                    $"DigiKey OAuth2 token request failed ({(int)response.StatusCode}): {DescribeError(body)}");
            }

            var token = JsonConvert.DeserializeObject<DigiKeyTokenResponse>(body);
            if (string.IsNullOrEmpty(token?.AccessToken))
            {
                throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                    "DigiKey OAuth2 token response did not contain an access_token.");
            }

            return token;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _tokenLock?.Dispose();
            _orderingTokenLock?.Dispose();
        }
    }
}
