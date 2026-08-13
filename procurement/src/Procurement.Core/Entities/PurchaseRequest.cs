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

        /// <summary>Raw MAX Order_Master.STATUS_10 value ("1" = Planned, "2" = Approved) at the time of sync — distinct from <see cref="Status"/>, which tracks this prototype's own workflow. Display-only (MainForm's requests grid), never used for filtering logic.</summary>
        public string MaxOrderStatus { get; set; }

        public virtual List<PurchaseRequestLine> Lines { get; set; } = new List<PurchaseRequestLine>();

        public PurchaseRequest()
        {
            RequestDate = DateTime.UtcNow;
            Status = PurchaseRequestStatus.Pending;
        }
    }
}
