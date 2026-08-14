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

        /// <summary>When true (the only supported mode for this prototype), the adapter never calls the HTTP layer and returns fixed/deterministic mock data instead.</summary>
        public bool UseMockData { get; set; } = true;
    }
}
