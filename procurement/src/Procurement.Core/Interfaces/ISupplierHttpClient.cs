using System.Threading.Tasks;

namespace Procurement.Core.Interfaces
{
    /// <summary>
    /// Thin abstraction over the eventual real DigiKey/Farnell REST calls. While no API
    /// credentials exist, adapters run in mock mode and never call this; once credentials are
    /// available, only a concrete implementation of this interface needs to be filled in
    /// (spec §0 point 2) — the adapters themselves don't change.
    /// </summary>
    public interface ISupplierHttpClient
    {
        Task<string> GetAsync(string relativeUrl);
        Task<string> PostAsync(string relativeUrl, string jsonBody);
    }
}
