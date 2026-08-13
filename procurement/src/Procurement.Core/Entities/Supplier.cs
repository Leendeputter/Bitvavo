using System.Collections.Generic;
using Procurement.Core.Enums;

namespace Procurement.Core.Entities
{
    /// <summary>Master record for a configured supplier (DigiKey, Farnell, ...).</summary>
    public class Supplier
    {
        public int Id { get; set; }
        public string SupplierCode { get; set; }
        public string Name { get; set; }
        public bool IsSandbox { get; set; }
        public bool UseMockData { get; set; }

        public virtual List<SupplierCapabilityRecord> Capabilities { get; set; } = new List<SupplierCapabilityRecord>();
    }

    /// <summary>Persisted row of the capability matrix from spec §5, one row per (Supplier, Capability).</summary>
    public class SupplierCapabilityRecord
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        /// <summary>Name of the capability, e.g. "ProductSearch", "Pricing", "Ordering", "Shipment", "Tracking", "Invoice".</summary>
        public string Capability { get; set; }
        public CapabilityStatus Status { get; set; }
    }
}
