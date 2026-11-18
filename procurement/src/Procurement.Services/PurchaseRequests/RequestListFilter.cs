using System;
using System.Collections.Generic;
using Procurement.Core.Enums;
using Procurement.Erp;

namespace Procurement.Services.PurchaseRequests
{
    /// <summary>
    /// The filter boxes of the Purchase Requests screen as plain data, so the desktop (checkboxes,
    /// date pickers) and the web app (a form) feed the same logic. Moved out of MainForm on 18 nov 2026.
    /// </summary>
    public class RequestListFilter
    {
        /// <summary>"Display" group: which workflow statuses to show. Local only, never sent to MAX.</summary>
        public ISet<PurchaseRequestStatus> Statuses { get; set; } = new HashSet<PurchaseRequestStatus>();

        /// <summary>MAX Order_Master.STATUS_10 filter for "Query": 1 = Planned, 2 = Approved (default only Approved).</summary>
        public bool IncludePlanned { get; set; }
        public bool IncludeApproved { get; set; } = true;

        /// <summary>Due Date Range; both null = no date filter.</summary>
        public DateTime? DueDateStart { get; set; }
        public DateTime? DueDateEnd { get; set; }

        /// <summary>"Select By" range on order number, part or customer; empty start and end = no range.</summary>
        public MaxOrderRangeField RangeField { get; set; } = MaxOrderRangeField.OrderNumber;
        public string RangeStart { get; set; }
        public string RangeEnd { get; set; }

        /// <summary>Show the manual "TEST-" requests.</summary>
        public bool ShowTestRequests { get; set; }

        internal bool HasDueDateFilter => DueDateStart.HasValue && DueDateEnd.HasValue;
        internal string TrimmedRangeStart => string.IsNullOrWhiteSpace(RangeStart) ? null : RangeStart.Trim();
        internal string TrimmedRangeEnd => string.IsNullOrWhiteSpace(RangeEnd) ? null : RangeEnd.Trim();
        internal bool HasRange => TrimmedRangeStart != null || TrimmedRangeEnd != null;
    }
}
