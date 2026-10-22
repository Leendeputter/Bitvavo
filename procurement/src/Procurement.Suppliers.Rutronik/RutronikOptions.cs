namespace Procurement.Suppliers.Rutronik
{
    /// <summary>
    /// ApiKey is populated by CompositionRoot from the encrypted Supplier row
    /// (Procurement.Core.Security.SecretProtector, editable via Instellingen's "Credentials
    /// bewerken" dialog) — never from App.config or source control.
    /// </summary>
    public class RutronikOptions
    {
        /// <summary>
        /// Rutronik's whole API is a single query-string "apikey" — no OAuth, no separate
        /// secret. Obtained personally from a Rutronik contact person after requesting it via
        /// their website contact form (confirmed by both documents the user pasted, 22 okt 2026).
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>Rutronik documents no separate sandbox environment — kept only for UI/Supplier-row consistency with the other adapters, unused by RutronikHttpClientWrapper.</summary>
        public bool IsSandbox { get; set; } = true;

        /// <summary>When true, the adapter never calls the HTTP layer and returns fixed/deterministic mock data instead. Set to false only once ApiKey is real.</summary>
        public bool UseMockData { get; set; } = true;
    }
}
