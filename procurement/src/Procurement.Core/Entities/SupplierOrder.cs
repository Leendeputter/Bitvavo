using System;
using System.Collections.Generic;
using Procurement.Core.Enums;

namespace Procurement.Core.Entities
{
    public class SupplierOrder
    {
        public int Id { get; set; }
        public int ErpPoId { get; set; }
        public virtual PurchaseOrder ErpPo { get; set; }
        public string ErpPoNumber { get; set; }

        public string SupplierCode { get; set; }
        public string SupplierOrderNumber { get; set; }

        /// <summary>Format: PROC-{jaar}-{volgnummer}-{SUPPLIERCODE} (spec §10).</summary>
        public string IdempotencyKey { get; set; }

        /// <summary>Incremented on every (re)submission attempt for the same PO+supplier; part of the unique constraint from spec §10.</summary>
        public int OrderVersion { get; set; }

        public DateTime OrderDate { get; set; }
        public string Currency { get; set; }
        public decimal OrderTotal { get; set; }
        public SupplierOrderStatusEnum Status { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }

        public virtual List<SupplierOrderLine> Lines { get; set; } = new List<SupplierOrderLine>();

        public SupplierOrder()
        {
            Currency = "EUR";
            OrderVersion = 1;
            Status = SupplierOrderStatusEnum.Created;
        }
    }

    public class SupplierOrderLine
    {
        public int Id { get; set; }
        public int SupplierOrderId { get; set; }
        public virtual SupplierOrder SupplierOrder { get; set; }

        public string SupplierPartNumber { get; set; }
        public int Quantity { get; set; }
        public int? ConfirmedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public DateTime? EstimatedShipDate { get; set; }
    }
}
