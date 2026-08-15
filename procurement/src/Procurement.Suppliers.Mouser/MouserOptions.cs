namespace Procurement.Suppliers.Mouser
{
    /// <summary>
    /// ApiKey is populated by CompositionRoot from the encrypted Supplier row
    /// (Procurement.Core.Security.SecretProtector, editable via Instellingen's "Credentials
    /// bewerken" dialog) — never from App.config or source control.
    /// </summary>
    public class MouserOptions
    {
        /// <summary>Mouser's "Search API" key — self-service, obtained from mouser.com/api-hub/.</summary>
        public string ApiKey { get; set; }

        /// <summary>Mouser has no separate sandbox environment — kept only for UI/Supplier-row consistency with the other adapters, unused by MouserHttpClientWrapper.</summary>
        public bool IsSandbox { get; set; } = true;

        /// <summary>When true, the adapter never calls the HTTP layer and returns fixed/deterministic mock data instead. Set to false only once ApiKey is real and the Order API contract (see MouserAdapter) has been confirmed — Mouser issues a *separate* Order API key from the Search API key used here.</summary>
        public bool UseMockData { get; set; } = true;
    }
}
