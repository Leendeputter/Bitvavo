using System;
using Procurement.Core.Enums;

namespace Procurement.Core.Entities
{
    /// <summary>Learned/confirmed link between an internal ERP article and a supplier part number.</summary>
    public class SupplierProductMapping
    {
        public int Id { get; set; }
        public string ErpArticleId { get; set; }
        public string SupplierCode { get; set; }
        public string SupplierPartNumber { get; set; }
        public string Manufacturer { get; set; }
        public string ManufacturerPartNumber { get; set; }
        public MatchConfidence MatchConfidence { get; set; }
        public DateTime CreatedAt { get; set; }

        public SupplierProductMapping()
        {
            CreatedAt = DateTime.UtcNow;
            MatchConfidence = MatchConfidence.Unknown;
        }
    }
}
