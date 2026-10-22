namespace Procurement.Suppliers.Arrow
{
    /// <summary>
    /// ClientId/ClientSecret are populated by CompositionRoot from the encrypted Supplier row
    /// (Procurement.Core.Security.SecretProtector, editable via Instellingen's "Credentials
    /// bewerken" dialog) — never from App.config or source control.
    /// </summary>
    public class ArrowOptions
    {
        /// <summary>OAuth2 client_credentials client id — sent both as the Basic-auth username on the token request and as a repeated "client_id" header on every API call, per MyArrow's own spec.</summary>
        public string ClientId { get; set; }

        /// <summary>OAuth2 client_credentials client secret — Basic-auth password on the token request only, never sent again afterwards.</summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// Optional Bill-To account id for the priceandavail/orders calls — left null to use the
        /// account's own default (MyArrow's spec: "a default billTo/shipTo is assigned" per API
        /// user), same simplification already used for Mouser's shipping address.
        /// </summary>
        public string BillToId { get; set; }

        /// <summary>Optional Ship-To account id — see BillToId.</summary>
        public string ShipToId { get; set; }

        /// <summary>Currency code for priceandavail search/orders (e.g. "EUR").</summary>
        public string Currency { get; set; } = "EUR";

        /// <summary>MyArrow's spec documents no separate sandbox host — kept only for UI/Supplier-row consistency with the other adapters, unused by ArrowHttpClientWrapper.</summary>
        public bool IsSandbox { get; set; } = true;

        /// <summary>When true, the adapter never calls the HTTP layer and returns fixed/deterministic mock data instead. Set to false only once ClientId/ClientSecret are real — and see ArrowAdapter's own doc comment before doing that: real-mode search/pricing/ordering still has an unconfirmed gap, flagged there.</summary>
        public bool UseMockData { get; set; } = true;
    }
}
