namespace Procurement.Suppliers.Farnell
{
    /// <summary>
    /// ApiKey is populated by CompositionRoot from the encrypted Supplier row
    /// (Procurement.Core.Security.SecretProtector, editable via Instellingen's "Credentials
    /// bewerken" dialog) — never from App.config or source control. Never write it to the audit
    /// log either.
    /// </summary>
    public class FarnellOptions
    {
        public string ApiKey { get; set; }
        public bool IsSandbox { get; set; } = true;

        /// <summary>When true, the adapter never calls the HTTP layer and returns fixed/deterministic mock data instead. Set to false only once ApiKey is real and the Ordering API contract (see FarnellAdapter) has been confirmed.</summary>
        public bool UseMockData { get; set; } = true;

        /// <summary>
        /// element14/Farnell's "storeInfo.id" parameter, which selects both the national store
        /// (pricing/stock/currency) and language. "nl.farnell.com" is Farnell's Dutch storefront —
        /// unconfirmed against real API docs/account setup, override here if Farnell's onboarding
        /// says otherwise.
        /// </summary>
        public string StoreId { get; set; } = "nl.farnell.com";
    }
}
