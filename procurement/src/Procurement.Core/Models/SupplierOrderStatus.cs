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

        /// <summary>
        /// Confirmed unit price, if the supplier's order-status response includes one — needed to
        /// flag a price-deviation exception during confirmation processing (spec: only price/
        /// quantity/late-delivery deviations need manual review, everything else is applied
        /// automatically). Null when an adapter's mock data or a real order-status response doesn't
        /// carry price information back.
        /// </summary>
        public decimal? ConfirmedUnitPrice { get; set; }
    }
}
