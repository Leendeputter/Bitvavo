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

        // MAX Order_Master/Part_Master fields below are display-only (synced from MaxOrder, see
        // MaxErpConnector) — nothing in the sourcing/matching/approval logic reads them. They exist
        // purely so the requests grid (MainForm) can show the same columns as the MAX order query.
        /// <summary>Part_Master.TYPE_01.</summary>
        public string PartType { get; set; }
        /// <summary>Order_Master.REVLEV_10.</summary>
        public string Revision { get; set; }
        /// <summary>Order_Master.FRMPLN_10.</summary>
        public bool Firm { get; set; }
        /// <summary>Order_Master.COST_10.</summary>
        public decimal? Cost { get; set; }
        /// <summary>Order_Master.CSTCNV_10.</summary>
        public decimal? CostConv { get; set; }
        /// <summary>Part_Master.COMCDE_01.</summary>
        public string Customer { get; set; }
        /// <summary>Order_Master.STK_10.</summary>
        public string StockId { get; set; }
        /// <summary>Part_Master.PMDES1_01.</summary>
        public string Desc1 { get; set; }
        /// <summary>Part_Master.PMDES2_01.</summary>
        public string Desc2 { get; set; }

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
