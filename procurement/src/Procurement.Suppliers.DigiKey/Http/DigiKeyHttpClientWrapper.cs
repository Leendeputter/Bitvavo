using System;
using System.Net.Http;
using System.Threading.Tasks;
using Procurement.Core.Interfaces;

namespace Procurement.Suppliers.DigiKey.Http
{
    /// <summary>
    /// Real DigiKey REST client. Not exercised by this prototype (DigiKeyAdapter runs in mock
    /// mode) — fill in BaseAddress, OAuth token handling and endpoint paths here once real
    /// credentials exist; DigiKeyAdapter itself will not need to change.
    /// </summary>
    public class DigiKeyHttpClientWrapper : ISupplierHttpClient, IDisposable
    {
        private readonly HttpClient _httpClient;

        public DigiKeyHttpClientWrapper(DigiKeyOptions options)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.IsSandbox
                    ? "https://sandbox-api.digikey.com/"
                    : "https://api.digikey.com/")
            };
        }

        public Task<string> GetAsync(string relativeUrl)
        {
            // TODO: implement once DigiKey API credentials are available.
            throw new NotImplementedException("DigiKey HTTP layer is not implemented in this prototype; adapter should run with UseMockData=true.");
        }

        public Task<string> PostAsync(string relativeUrl, string jsonBody)
        {
            // TODO: implement once DigiKey API credentials are available.
            throw new NotImplementedException("DigiKey HTTP layer is not implemented in this prototype; adapter should run with UseMockData=true.");
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
