using System;
using System.Collections.Generic;
using Procurement.Core.Enums;

namespace Procurement.Core.Entities
{
    public class PurchaseOrder
    {
        public int Id { get; set; }
        public string ErpPoNumber { get; set; }
        public int PurchaseRequestId { get; set; }
        public virtual PurchaseRequest PurchaseRequest { get; set; }
        public PurchaseOrderStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual List<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();

        public PurchaseOrder()
        {
            CreatedAt = DateTime.UtcNow;
            Status = PurchaseOrderStatus.Draft;
        }
    }

    public class PurchaseOrderLine
    {
        public int Id { get; set; }
        public int PurchaseOrderId { get; set; }
        public virtual PurchaseOrder PurchaseOrder { get; set; }

        public int PurchaseRequestLineId { get; set; }
        public string SupplierCode { get; set; }
        public string SupplierPartNumber { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}
