using System;
using System.Collections.Generic;
using Procurement.Core.Enums;

namespace Procurement.Core.Models
{
    /// <summary>DTO returned by ISupplierAdapter.GetOrderStatusAsync.</summary>
    public class SupplierOrderStatus
    {
        public string SupplierOrderNumber { get; set; }
        public PurchaseOrderStatus Status { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public List<SupplierOrderStatusLine> Lines { get; set; } = new List<SupplierOrderStatusLine>();
    }

    public class SupplierOrderStatusLine
    {
        public string SupplierPartNumber { get; set; }
        public int ConfirmedQuantity { get; set; }
        public DateTime? EstimatedShipDate { get; set; }
    }
}
