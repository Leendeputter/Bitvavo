using System.Collections.Generic;
using Procurement.Core.Enums;

namespace Procurement.Core.Models
{
    public class SupplierOrderRequest
    {
        public string SupplierCode { get; set; }
        public string ErpPoNumber { get; set; }
        public string Currency { get; set; }
        public List<SupplierOrderRequestLine> Lines { get; set; } = new List<SupplierOrderRequestLine>();
    }

    public class SupplierOrderRequestLine
    {
        public string SupplierPartNumber { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public PackagingType PackagingType { get; set; }
    }
}
