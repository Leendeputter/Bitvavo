namespace Procurement.Suppliers.Farnell
{
    /// <summary>
    /// TODO: once real Farnell/element14 API credentials are available, move ApiKey out of
    /// App.config into a proper secrets manager. Never write it to the audit log.
    /// </summary>
    public class FarnellOptions
    {
        public string ApiKey { get; set; }
        public bool IsSandbox { get; set; } = true;

        /// <summary>When true (the only supported mode for this prototype), the adapter never calls the HTTP layer and returns fixed/deterministic mock data instead.</summary>
        public bool UseMockData { get; set; } = true;
    }
}
