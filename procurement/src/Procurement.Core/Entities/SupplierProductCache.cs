using System;
using Procurement.Core.Enums;

namespace Procurement.Core.Entities
{
    /// <summary>
    /// Persisted snapshot of a supplier catalog result (maps to the "SupplierProduct" table from
    /// spec §12). Kept distinct from the <c>Models.SupplierProduct</c> DTO that adapters return,
    /// which is transient and never touches the database directly.
    /// </summary>
    public class SupplierProductCache
    {
        public int Id { get; set; }
        public string SupplierCode { get; set; }
        public string SupplierPartNumber { get; set; }
        public string Manufacturer { get; set; }
        public string ManufacturerPartNumber { get; set; }
        public string Description { get; set; }
        public MatchConfidence MatchConfidence { get; set; }
        public string DatasheetUrl { get; set; }
        public DateTime RetrievedAt { get; set; }

        public SupplierProductCache()
        {
            RetrievedAt = DateTime.UtcNow;
        }
    }
}
