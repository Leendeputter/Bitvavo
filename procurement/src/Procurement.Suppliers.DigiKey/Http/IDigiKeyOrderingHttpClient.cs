using System.Threading.Tasks;
using Procurement.Core.Interfaces;

namespace Procurement.Suppliers.DigiKey.Http
{
    /// <summary>
    /// Extends the shared ISupplierHttpClient (2-legged client-credentials, used for search/pricing)
    /// with the one call Ordering v3 needs — kept out of ISupplierHttpClient itself since Ordering's
    /// 3-legged (Authorization Code) token is a DigiKey-specific concept no other supplier's HTTP
    /// wrapper has any reason to know about. DigiKeyAdapter checks for this interface at the point it
    /// actually needs to place a real order, rather than widening its own constructor's httpClient
    /// parameter type (which stays ISupplierHttpClient so existing tests/mocks are unaffected).
    /// </summary>
    public interface IDigiKeyOrderingHttpClient : ISupplierHttpClient
    {
        Task<string> PostOrderAsync(string relativeUrl, string jsonBody);
    }
}
