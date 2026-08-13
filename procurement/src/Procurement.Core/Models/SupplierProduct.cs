using Procurement.Core.Enums;

namespace Procurement.Core.Models
{
    /// <summary>
    /// DTO returned by <see cref="Procurement.Core.Interfaces.ISupplierAdapter"/>. Not persisted
    /// directly; the engine turns confirmed results into <see cref="Entities.SupplierProductCache"/>
    /// / <see cref="Entities.SupplierProductMapping"/> rows.
    /// </summary>
    public class SupplierProduct
    {
        public string SupplierPartNumber { get; set; }
        public string Manufacturer { get; set; }
        public string ManufacturerPartNumber { get; set; }
        public string Description { get; set; }
        public MatchConfidence MatchConfidence { get; set; }
        public string DatasheetUrl { get; set; }
    }
}
