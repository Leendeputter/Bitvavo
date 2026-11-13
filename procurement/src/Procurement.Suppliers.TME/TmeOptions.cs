namespace Procurement.Suppliers.TME
{
    /// <summary>
    /// Token/ApiKey are populated by CompositionRoot from the SharedSecrets database
    /// (Procurement.Data.Secrets.SecretSettings, same as UniPro; edited via Beheer > App Secrets) — never from App.config or source control.
    /// </summary>
    public class TmeOptions
    {
        /// <summary>TME's public API token, sent as the "Token" request parameter on every call.</summary>
        public string Token { get; set; }

        /// <summary>TME's private API key, used only as the HMAC-SHA1 signing secret — never sent in a request itself.</summary>
        public string ApiKey { get; set; }

        /// <summary>TME's country/language/currency parameters, required on every request. Defaulted for a Dutch company/EUR pricing — unconfirmed against real API docs.</summary>
        public string Country { get; set; } = "NL";
        public string Language { get; set; } = "EN";
        public string Currency { get; set; } = "EUR";

        /// <summary>TME has a documented sandbox ("apitest.tme.eu") separate from production ("api.tme.eu").</summary>
        public bool IsSandbox { get; set; } = true;

        /// <summary>When true, the adapter never calls the HTTP layer and returns fixed/deterministic mock data instead. Set to false only once Token/ApiKey are real and the Order API contract (see TmeAdapter) has been confirmed.</summary>
        public bool UseMockData { get; set; } = true;
    }
}
