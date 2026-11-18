using System;
using System.Collections.Generic;
using System.Linq;
using Procurement.Core.Enums;
using Procurement.Erp;
using Procurement.Services.PurchaseRequests;

namespace Procurement.Web.Models
{
    /// <summary>The filter boxes as posted by the page; mapped to the shared <see cref="RequestListFilter"/>.</summary>
    public class RequestFilterModel
    {
        public List<string> Statuses { get; set; } = new List<string>();
        public bool IncludePlanned { get; set; }
        public bool IncludeApproved { get; set; } = true;
        public bool DueDateEnabled { get; set; }
        public DateTime? DueDateStart { get; set; }
        public DateTime? DueDateEnd { get; set; }
        public string RangeField { get; set; }
        public string RangeStart { get; set; }
        public string RangeEnd { get; set; }
        public bool ShowTestRequests { get; set; }

        public RequestListFilter ToFilter() => new RequestListFilter
        {
            Statuses = new HashSet<PurchaseRequestStatus>((Statuses ?? new List<string>())
                .Select(s => Enum.TryParse<PurchaseRequestStatus>(s, out var status) ? status : (PurchaseRequestStatus?)null)
                .Where(s => s.HasValue).Select(s => s.Value)),
            IncludePlanned = IncludePlanned,
            IncludeApproved = IncludeApproved,
            DueDateStart = DueDateEnabled ? DueDateStart : null,
            DueDateEnd = DueDateEnabled ? DueDateEnd : null,
            RangeField = RangeField == "Customer" ? MaxOrderRangeField.Customer
                : RangeField == "Part" ? MaxOrderRangeField.Part
                : MaxOrderRangeField.OrderNumber,
            RangeStart = RangeStart,
            RangeEnd = RangeEnd,
            ShowTestRequests = ShowTestRequests
        };
    }
}
