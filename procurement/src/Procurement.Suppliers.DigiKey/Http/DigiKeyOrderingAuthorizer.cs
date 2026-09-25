using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
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
    /// RedirectUri must be registered as this app's exact Callback URL in DigiKey's developer
    /// portal. "http://localhost" with any port needs no admin rights/URL ACL reservation for
    /// HttpListener to bind to (unlike "+"/"*" wildcard hosts) — unlike the "https://localhost/
    /// callback" placeholder registered before a real 3-legged flow existed, which would need a
    /// locally-bound SSL certificate for no real benefit on a native desktop app (see RFC 8252).
    /// </summary>
    public class DigiKeyOrderingAuthorizer
    {
        public const string RedirectUri = "http://localhost:8983/callback/";
        private static readonly TimeSpan ConsentTimeout = TimeSpan.FromMinutes(5);

        /// <summary>Opens the system browser to DigiKey's consent page, waits for the local redirect carrying the authorization code, exchanges it for tokens, and returns the refresh token — the caller (SettingsForm) is the one that persists it (this class has no database access).</summary>
        public async Task<string> AuthorizeAsync(DigiKeyOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.ClientId) || string.IsNullOrEmpty(options.ClientSecret))
            {
                throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                    "Vul eerst Client Id/Client Secret in via \"Credentials bewerken...\" voordat je Ordering autoriseert.");
            }

            var state = Guid.NewGuid().ToString("N");
            var host = options.IsSandbox ? "https://sandbox-api.digikey.com" : "https://api.digikey.com";
            var authorizeUrl = $"{host}/v1/oauth2/authorize" +
                $"?response_type=code&client_id={Uri.EscapeDataString(options.ClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}&state={state}";

            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(RedirectUri);
                try
                {
                    listener.Start();
                }
                catch (HttpListenerException ex)
                {
                    throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.UnknownError,
                        $"Kan niet lokaal luisteren op {RedirectUri} (poort al in gebruik, of Callback URL in het " +
                        $"DigiKey-portal komt niet exact overeen?): {ex.Message}", ex);
                }

                Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true });

                var contextTask = listener.GetContextAsync();
                var completed = await Task.WhenAny(contextTask, Task.Delay(ConsentTimeout)).ConfigureAwait(false);
                if (completed != contextTask)
                {
                    throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.Timeout,
                        "Geen reactie van DigiKey ontvangen binnen 5 minuten — autorisatie geannuleerd.");
                }

                var context = await contextTask.ConfigureAwait(false);
                var query = context.Request.QueryString;
                var code = query["code"];
                var returnedState = query["state"];
                var error = query["error"];

                RespondToBrowser(context, error == null && !string.IsNullOrEmpty(code));

                if (error != null)
                {
                    throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                        $"DigiKey heeft autorisatie geweigerd: {query["error_description"] ?? error}");
                }
                if (string.IsNullOrEmpty(code))
                {
                    throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                        "Geen autorisatiecode ontvangen van DigiKey.");
                }
                if (returnedState != state)
                {
                    throw new SupplierException(DigiKeyAdapter.Code, SupplierErrorCode.AuthenticationError,
                        "State-parameter kwam niet overeen bij de redirect — autorisatie afgebroken (mogelijke CSRF).");
                }

                return await ExchangeAuthorizationCodeAsync(options, code, host).ConfigureAwait(false);
            }
        }

        private static void RespondToBrowser(HttpListenerContext context, bool success)
        {
            var html = success
                ? "<html><body>DigiKey-autorisatie gelukt. Dit venster kan gesloten worden.</body></html>"
                : "<html><body>DigiKey-autorisatie mislukt. Dit venster kan gesloten worden.</body></html>";
            var buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        private static async Task<string> ExchangeAuthorizationCodeAsync(DigiKeyOptions options, string code, string host)
        {
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
