using System.Threading.Tasks;
using Procurement.Core.Interfaces;

namespace Procurement.Suppliers.Farnell.Http
{
    /// <summary>
    /// Extends the shared ISupplierHttpClient (api.element14.com, "callInfo.apiKey" query-string
    /// auth, used for search/pricing) with what Farnell's separate Order API needs — a different
    /// host (api.farnell.com) and a different auth scheme entirely: an "api_key" header plus a
    /// per-call JWT bearer token minted via /auth/token (spec pasted by the user, 21 okt 2026,
    /// since partner.element14.com is blocked for this session — see README). Kept out of
    /// ISupplierHttpClient itself for the same reason DigiKey's IDigiKeyOrderingHttpClient is
    /// separate: no other supplier's HTTP wrapper has any reason to know about this.
    /// </summary>
    public interface IFarnellOrderingHttpClient : ISupplierHttpClient
    {
        /// <summary>POST https://api.farnell.com/auth/token with the given tradeAccount — returns the JWT to pass as "jwt" on every call below.</summary>
        Task<string> AuthenticateOrderingAsync(string tradeAccount);

        Task<string> GetOrderingAsync(string relativeUrl, string jwt);
        Task<string> PostOrderingAsync(string relativeUrl, string jsonBody, string jwt);
    }
}
