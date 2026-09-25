using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;

namespace Procurement.Suppliers.DigiKey.Http
{
    /// <summary>
    /// One-time interactive 3-legged OAuth (Authorization Code) consent flow for DigiKey's Ordering
    /// v3 API — separate from DigiKeyHttpClientWrapper's 2-legged client-credentials flow (search/
    /// pricing), since Ordering's own confirmed spec requires user consent ("This API uses 3 legged
    /// OAuth"). Run once from Instellingen's "Ordering autoriseren..." button; the resulting refresh
    /// token is what DigiKeyHttpClientWrapper.PostOrderAsync uses afterward, refreshed automatically
    /// without this flow running again (until the refresh token itself is revoked or expires).
    ///
    /// DigiKey rejects any "http://" Callback URL outright ("Invalid redirection uri") — it must be
    /// "https://something". For an app with no real, publicly reachable callback endpoint (this
    /// desktop app has none), DigiKey's own documentation says to register the literal value
    /// "https://localhost" and use that same literal value as redirect_uri in both the authorize
    /// request and the token exchange. Nothing ever actually listens on that address: after consent,
    /// DigiKey's browser redirect to "https://localhost/?code=...&state=..." simply fails to connect
    /// (no server there), but the browser still shows the attempted URL — including the query string
    /// — in its address bar. So this flow has no local listener at all; the user copies that URL out
    /// of the address bar and pastes it back into the app (see SettingsForm, which owns the
    /// paste-back dialog — this class stays UI-free and only does the URL-building/parsing/HTTP).
    /// An earlier version of this class tried "http://localhost:8983/callback/" with a local
    /// HttpListener, which DigiKey's portal never actually accepted as a Callback URL value.
    /// </summary>
    public class DigiKeyOrderingAuthorizer
    {
        public const string RedirectUri = "https://localhost";

        /// <summary>Builds the URL to open in the system browser. Caller (SettingsForm) generates and remembers `state` so it can be checked again once the user pastes the result back.</summary>
        public string BuildAuthorizeUrl(DigiKeyOptions options, string state)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.ClientId) || string.IsNullOrEmpty(options.ClientSecret))
            {
                throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                    "Vul eerst Client Id/Client Secret in via \"Credentials bewerken...\" voordat je Ordering autoriseert.");
            }

            var host = options.IsSandbox ? "https://sandbox-api.digikey.com" : "https://api.digikey.com";
            return $"{host}/v1/oauth2/authorize" +
                $"?response_type=code&client_id={Uri.EscapeDataString(options.ClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}&state={state}";
        }

        public void OpenBrowser(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        /// <summary>
        /// Parses whatever the user pasted back — the full address-bar URL the browser tried (and
        /// failed) to load, or just its query string, or (if DigiKey rejected consent) an error
        /// redirect instead. Throws a clear Dutch SupplierException for every way this can go wrong,
        /// rather than a raw parsing exception.
        /// </summary>
        public static string ExtractAuthorizationCode(string pastedText, string expectedState)
        {
            if (string.IsNullOrWhiteSpace(pastedText))
            {
                throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                    "Er is niets geplakt — kopieer de volledige URL uit de adresbalk van de browser en plak die hier.");
            }

            var queryString = pastedText.Trim();
            var queryIndex = queryString.IndexOf('?');
            if (queryIndex >= 0) queryString = queryString.Substring(queryIndex + 1);
            var fragmentIndex = queryString.IndexOf('#');
            if (fragmentIndex >= 0) queryString = queryString.Substring(0, fragmentIndex);

            var parameters = queryString.Split('&')
                .Select(pair => pair.Split(new[] { '=' }, 2))
                .Where(pair => pair.Length == 2 && pair[0].Length > 0)
                .ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]), StringComparer.OrdinalIgnoreCase);

            if (parameters.TryGetValue("error", out var error))
            {
                var description = parameters.TryGetValue("error_description", out var d) ? d : error;
                throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                    $"DigiKey heeft autorisatie geweigerd: {description}");
            }

            if (!parameters.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
            {
                throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                    "Geen \"code\"-parameter gevonden in de geplakte tekst — plak de volledige URL uit de adresbalk (ook als de pagina zelf niet laadt, dat is verwacht).");
            }

            if (!parameters.TryGetValue("state", out var state) || state != expectedState)
            {
                throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                    "State-parameter kwam niet overeen met de verwachte waarde — autorisatie afgebroken (mogelijke CSRF, of een verouderde/opnieuw geplakte URL).");
            }

            return code;
        }

        public async Task<string> ExchangeAuthorizationCodeAsync(DigiKeyOptions options, string code)
        {
            var host = options.IsSandbox ? "https://sandbox-api.digikey.com" : "https://api.digikey.com";
            var tokenUrl = $"{host}/v1/oauth2/token";

            using (var httpClient = new HttpClient())
            {
                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("redirect_uri", RedirectUri),
                    new KeyValuePair<string, string>("client_id", options.ClientId),
                    new KeyValuePair<string, string>("client_secret", options.ClientSecret)
                });

                HttpResponseMessage response;
                try
                {
                    response = await httpClient.PostAsync(tokenUrl, form).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.Timeout,
                        "Kon DigiKey's token-endpoint niet bereiken.", ex);
                }

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                        $"DigiKey token-aanvraag mislukt ({(int)response.StatusCode}): {body}");
                }

                var token = JsonConvert.DeserializeObject<DigiKeyTokenResponse>(body);
                if (string.IsNullOrEmpty(token?.RefreshToken))
                {
                    throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                        "DigiKey's tokenrespons bevatte geen refresh_token.");
                }

                return token.RefreshToken;
            }
        }
    }
}
