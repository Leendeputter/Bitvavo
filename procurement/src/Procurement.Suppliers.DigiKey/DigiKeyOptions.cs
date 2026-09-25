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

        /// <summary>When true, the adapter never calls the HTTP layer and returns fixed/deterministic mock data instead. Search/pricing/availability real-mode is confirmed working against the sandbox; real-mode ordering additionally needs RefreshToken (see below) to be set via the one-time "Ordering autoriseren..." consent flow.</summary>
        public bool UseMockData { get; set; } = true;

        // DigiKey's V4 Product Information API requires these on every request
        // (X-DIGIKEY-Locale-*). Defaulted for a Dutch company/EUR pricing — unconfirmed against
        // real API docs, override here if DigiKey's dashboard/support says otherwise.
        public string LocaleSite { get; set; } = "NL";
        public string LocaleLanguage { get; set; } = "en";
        public string LocaleCurrency { get; set; } = "EUR";

        /// <summary>
        /// OAuth2 refresh token from DigiKeyOrderingAuthorizer's one-time interactive consent flow —
        /// Ordering v3 needs 3-legged OAuth (user consent), unlike the 2-legged client-credentials
        /// flow the fields above are for. Null until "Ordering autoriseren..." has been run once in
        /// Instellingen; DigiKeyHttpClientWrapper.PostOrderAsync throws a clear error instead of
        /// guessing if a real order is attempted before that. Populated by CompositionRoot from
        /// Supplier.RefreshToken (SecretProtector-encrypted, same as ClientId/ClientSecret/ApiKey) —
        /// mutated in place (not just read once) since a successful token refresh can rotate it, and
        /// DigiKeyHttpClientWrapper needs OnRefreshTokenRotated wired up to persist that back to the
        /// database or the rotated value would only live for this process's lifetime.
        /// </summary>
        public string RefreshToken { get; set; }

        // Account/shipping-contact fields DigiKey's OrderRequest.BuyerContact/ShippingContact
        // require on every real order — populated by CompositionRoot from the matching Supplier
        // fields (Instellingen's "Verzendgegevens bewerken" dialog). See DigiKeyAdapter.
        public string AccountId { get; set; }
        public string ContactName { get; set; }
        public string ContactEmail { get; set; }
        public string ContactTelephone { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string City { get; set; }
        public string Province { get; set; }
        public string PostalCode { get; set; }
        public string CountryCode { get; set; }
    }
}
