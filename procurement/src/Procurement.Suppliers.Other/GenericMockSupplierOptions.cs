namespace Procurement.Suppliers.Other
{
    /// <summary>
    /// Shared options shape for the distributors in this project (Arrow, Rutronik, Avnet/Silica,
    /// Karl Kruse, RS Components, Distrelec, Conrad Business Supplies). Unlike DigiKey/Farnell/
    /// Mouser/TME, none of these have a confirmed, self-service-documented public API — placing an
    /// order or even reading live pricing typically requires a separate account/EDI agreement with
    /// the distributor first. So there is deliberately no real HTTP layer behind this options
    /// class yet: ClientId/ClientSecret/ApiKey are stored (so credentials can already be entered
    /// via Instellingen once known) but GenericMockSupplierAdapter never reads them — it only ever
    /// returns mock data, and throws a clear SupplierException if UseMockData is set to false
    /// without a real adapter having been built for that supplier first.
    /// </summary>
    public class GenericMockSupplierOptions
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string ApiKey { get; set; }
        public bool IsSandbox { get; set; } = true;
        public bool UseMockData { get; set; } = true;
    }
}
