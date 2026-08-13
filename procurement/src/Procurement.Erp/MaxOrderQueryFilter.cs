using System;
using System.Collections.Generic;

namespace Procurement.Erp
{
    /// <summary>Column a "Select By" range filter (MainForm's Filter group box) applies to.</summary>
    public enum MaxOrderRangeField
    {
        OrderNumber,
        Customer,
        Part
    }

    /// <summary>
    /// Filter options for MaxOrderRepository.GetOpenOrdersAsync, set from MainForm's filter
    /// controls (status checkboxes, Due Date Range group box, Filter/"Select By" group box) right
    /// before the user clicks "Query" — nothing here is applied automatically.
    /// </summary>
    public class MaxOrderQueryFilter
    {
        public IReadOnlyCollection<string> Statuses { get; set; }

        public DateTime? DueDateStart { get; set; }
        public DateTime? DueDateEnd { get; set; }

        public MaxOrderRangeField? RangeField { get; set; }
        public string RangeStart { get; set; }
        public string RangeEnd { get; set; }
    }
}
