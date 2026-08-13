using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Procurement.Core.Enums;

namespace Procurement.Core.Entities
{
    public class PurchaseRequestLine
    {
        public int Id { get; set; }
        public int PurchaseRequestId { get; set; }
        public virtual PurchaseRequest PurchaseRequest { get; set; }

        public string ErpArticleId { get; set; }
        public string Manufacturer { get; set; }
        public string ManufacturerPartNumber { get; set; }
        public string Description { get; set; }
        public int RequestedQuantity { get; set; }
        public DateTime? RequiredDate { get; set; }
        public PackagingRequirement PackagingRequirement { get; set; }
        public bool ReelRequirement { get; set; }

        /// <summary>Persisted as a comma-separated list; use <see cref="PreferredSuppliers"/> in code.</summary>
        public string PreferredSuppliersCsv { get; set; }

        [NotMapped]
        public List<string> PreferredSuppliers
        {
            get => string.IsNullOrWhiteSpace(PreferredSuppliersCsv)
                ? new List<string>()
                : PreferredSuppliersCsv.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            set => PreferredSuppliersCsv = value == null ? null : string.Join(",", value);
        }

        public PurchaseRequestLine()
        {
            PackagingRequirement = PackagingRequirement.NoPreference;
        }
    }
}
