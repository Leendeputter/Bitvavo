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

        /// <summary>When true (the only supported mode for this prototype), the adapter never calls the HTTP layer and returns fixed/deterministic mock data instead.</summary>
        public bool UseMockData { get; set; } = true;
    }
}
