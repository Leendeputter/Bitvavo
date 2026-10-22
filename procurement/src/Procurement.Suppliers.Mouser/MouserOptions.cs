namespace Procurement.Suppliers.Mouser
{
    /// <summary>
    /// ApiKey is populated by CompositionRoot from the encrypted Supplier row
    /// (Procurement.Core.Security.SecretProtector, editable via Instellingen's "Credentials
    /// bewerken" dialog) — never from App.config or source control.
    /// </summary>
    public class MouserOptions
    {
        /// <summary>
        /// A single API key covering Search, Cart and Order — confirmed via Mouser's own API guide
        /// (pasted by the user, 21 okt 2026): generated from My Mouser account's "APIs" panel under
        /// Personal Information, explicitly described there as usable "with the Mouser Cart and
        /// Order APIs" too, not just Search. Supersedes an earlier assumption in this codebase that
        /// ordering would need a second, separately-approved key.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>Mouser has no separate sandbox environment — kept only for UI/Supplier-row consistency with the other adapters, unused by MouserHttpClientWrapper.</summary>
        public bool IsSandbox { get; set; } = true;

        /// <summary>
        /// When true, the adapter never calls the HTTP layer and returns fixed/deterministic mock
        /// data instead. Set to false only once ApiKey is real — and only when actually ready to
        /// place a real order: Mouser's own API guide states plainly that there is no sandbox for
        /// Cart/Order calls, real-mode here always hits the live account.
        /// </summary>
        public bool UseMockData { get; set; } = true;
    }
}
