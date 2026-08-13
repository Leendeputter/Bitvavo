using System;
using Procurement.Core.Enums;

namespace Procurement.Core.Models
{
    public class SupplierOrderResult
    {
        public bool Success { get; set; }
        public string SupplierOrderNumber { get; set; }
        public SupplierOrderStatusEnum Status { get; set; }
        public decimal OrderTotal { get; set; }
        public string Currency { get; set; }
        public DateTime SubmittedAt { get; set; }
        public SupplierErrorCode? ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
    }
}
