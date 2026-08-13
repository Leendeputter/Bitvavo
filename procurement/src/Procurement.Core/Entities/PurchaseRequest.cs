using System;
using System.Collections.Generic;
using Procurement.Core.Enums;

namespace Procurement.Core.Entities
{
    public class PurchaseRequest
    {
        public int Id { get; set; }

        /// <summary>MAX company (ExactRMCompanies.CompanyID) this request belongs to — set from the login screen's company selection, never editable afterwards.</summary>
        public long CompanyId { get; set; }

        public string ErpRequestNumber { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? RequiredDate { get; set; }
        public string Warehouse { get; set; }
        public string Project { get; set; }
        public int Priority { get; set; }
        public PurchaseRequestStatus Status { get; set; }

        public virtual List<PurchaseRequestLine> Lines { get; set; } = new List<PurchaseRequestLine>();

        public PurchaseRequest()
        {
            RequestDate = DateTime.UtcNow;
            Status = PurchaseRequestStatus.Pending;
        }
    }
}
