using System;
using System.Net.Http;
using System.Threading.Tasks;
using Procurement.Core.Interfaces;

namespace Procurement.Suppliers.Farnell.Http
{
    /// <summary>
    /// Real Farnell/element14 REST client. Not exercised by this prototype (FarnellAdapter runs
    /// in mock mode) — fill in BaseAddress, API key handling and endpoint paths here once real
    /// credentials exist; FarnellAdapter itself will not need to change.
    /// </summary>
    public class FarnellHttpClientWrapper : ISupplierHttpClient, IDisposable
    {
        private readonly HttpClient _httpClient;

        public FarnellHttpClientWrapper(FarnellOptions options)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.element14.com/")
            };
        }

        public Task<string> GetAsync(string relativeUrl)
        {
            // TODO: implement once Farnell API credentials are available.
            throw new NotImplementedException("Farnell HTTP layer is not implemented in this prototype; adapter should run with UseMockData=true.");
        }

        public Task<string> PostAsync(string relativeUrl, string jsonBody)
        {
            // TODO: implement once Farnell API credentials are available.
            throw new NotImplementedException("Farnell HTTP layer is not implemented in this prototype; adapter should run with UseMockData=true.");
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
