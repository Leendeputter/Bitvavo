namespace Procurement.Suppliers.DigiKey
{
    /// <summary>
    /// ClientId/ClientSecret are populated by CompositionRoot from the encrypted Supplier row
    /// (Procurement.Core.Security.SecretProtector, editable via Instellingen's "Credentials
    /// bewerken" dialog) — never from App.config or source control. Never write them to the audit
    /// log either (DbAuditLogger already masks common secret key names, but keep this in mind for
    /// any new fields added here).
    /// </summary>
    public class DigiKeyOptions
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public bool IsSandbox { get; set; } = true;

        /// <summary>When true, the adapter never calls the HTTP layer and returns fixed/deterministic mock data instead. Set to false only once ClientId/ClientSecret are real and the Ordering API contract (see DigiKeyAdapter) has been confirmed.</summary>
        public bool UseMockData { get; set; } = true;

        // DigiKey's V4 Product Information API requires these on every request
        // (X-DIGIKEY-Locale-*). Defaulted for a Dutch company/EUR pricing — unconfirmed against
        // real API docs, override here if DigiKey's dashboard/support says otherwise.
        public string LocaleSite { get; set; } = "NL";
        public string LocaleLanguage { get; set; } = "en";
        public string LocaleCurrency { get; set; } = "EUR";
    }
}
