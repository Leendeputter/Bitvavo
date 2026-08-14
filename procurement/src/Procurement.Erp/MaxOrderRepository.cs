using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Procurement.Core.Interfaces;

namespace Procurement.Erp
{
    /// <summary>
    /// Reads open orders straight from MAX's own Order_Master/Part_Master tables (the
    /// AdminConnectionString resolved at login — a real per-company MAX database, not this app's
    /// own "Unitron" tables). Query and part-type filter ('B','D','Y') as supplied by the user;
    /// the order-status filter, Due Date range and "Select By" range are all optional and set from
    /// MainForm's filter controls just before the user clicks "Query" — nothing here runs on a
    /// timer or on form load.
    /// </summary>
    public class MaxOrderRepository
    {
        private readonly ISessionContext _session;

        public MaxOrderRepository(ISessionContext session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public async Task<IReadOnlyList<MaxOrder>> GetOpenOrdersAsync(MaxOrderQueryFilter filter)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            if (filter.Statuses == null || filter.Statuses.Count == 0)
                return new List<MaxOrder>();

            var results = new List<MaxOrder>();
            var statusList = filter.Statuses.ToList();
            var statusParamNames = statusList.Select((s, i) => $"@status{i}").ToList();

            var where = new StringBuilder();
            where.Append("dbo.Part_Master.TYPE_01 IN ('B','D','Y')");
            where.Append($" AND dbo.Order_Master.STATUS_10 IN ({string.Join(",", statusParamNames)})");

            if (filter.DueDateStart.HasValue)
                where.Append(" AND dbo.Order_Master.CURDUE_10 >= @dueDateStart");
            if (filter.DueDateEnd.HasValue)
                where.Append(" AND dbo.Order_Master.CURDUE_10 <= @dueDateEnd");

            var rangeColumn = RangeFieldColumn(filter.RangeField);
            var hasRangeStart = !string.IsNullOrWhiteSpace(filter.RangeStart);
            var hasRangeEnd = !string.IsNullOrWhiteSpace(filter.RangeEnd);
            if (rangeColumn != null && hasRangeStart)
                where.Append($" AND {rangeColumn} >= @rangeStart");
            if (rangeColumn != null && hasRangeEnd)
                where.Append($" AND {rangeColumn} <= @rangeEnd");

            var sql = $@"
SELECT
    dbo.Order_Master.ORDNUM_10 AS [Order],
    dbo.Order_Master.ORDER_10 AS OrderLong,
    dbo.Order_Master.PRTNUM_10 AS PartID,
    dbo.Order_Master.CURQTY_10 AS CurrentQty,
    dbo.Order_Master.FRMPLN_10 AS Firm,
    dbo.Order_Master.STATUS_10 AS Status,
    dbo.Order_Master.CURDUE_10 AS DueDate,
    dbo.Order_Master.STK_10 AS StockID,
    dbo.Order_Master.REVLEV_10 AS Revision,
    dbo.Order_Master.COST_10 AS Cost,
    dbo.Order_Master.CSTCNV_10 AS CostConv,
    dbo.Order_Master.ORDREF_10 AS Reference,
    dbo.Part_Master.PMDES1_01 AS Desc1,
    dbo.Part_Master.PMDES2_01 AS Desc2,
    dbo.Part_Master.VIEWER_01 AS ManufacturingPart,
    dbo.Part_Master.COMCDE_01 AS Customer,
    dbo.Part_Master.TYPE_01 AS PartType
FROM dbo.Order_Master
INNER JOIN dbo.Part_Master ON dbo.Order_Master.PRTNUM_10 = dbo.Part_Master.PRTNUM_01
WHERE {where}
ORDER BY dbo.Order_Master.ORDNUM_10";

            using (var conn = new SqlConnection(_session.AdminConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                for (var i = 0; i < statusList.Count; i++)
                    cmd.Parameters.AddWithValue(statusParamNames[i], statusList[i]);
                if (filter.DueDateStart.HasValue)
                    cmd.Parameters.AddWithValue("@dueDateStart", filter.DueDateStart.Value.Date);
                if (filter.DueDateEnd.HasValue)
                    cmd.Parameters.AddWithValue("@dueDateEnd", filter.DueDateEnd.Value.Date);
                if (rangeColumn != null && hasRangeStart)
                    cmd.Parameters.AddWithValue("@rangeStart", filter.RangeStart.Trim());
                if (rangeColumn != null && hasRangeEnd)
                    cmd.Parameters.AddWithValue("@rangeEnd", filter.RangeEnd.Trim());

                await conn.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new MaxOrder
                        {
                            OrderNumber = reader["Order"].ToString().Trim(),
                            OrderLong = ToDisplayString(reader["OrderLong"]),
                            PartId = CleanCodeField(reader["PartID"]),
                            CurrentQty = Convert.ToInt32(reader["CurrentQty"]),
                            Firm = ToBool(reader["Firm"]),
                            Status = reader["Status"].ToString().Trim(),
                            DueDate = reader["DueDate"] as DateTime?,
                            StockId = ToDisplayString(reader["StockID"]),
                            Revision = ToDisplayString(reader["Revision"]),
                            Cost = ToNullableDecimal(reader["Cost"]),
                            CostConv = ToNullableDecimal(reader["CostConv"]),
                            Reference = ToDisplayString(reader["Reference"]),
                            Desc1 = ToDisplayString(reader["Desc1"]),
                            Desc2 = ToDisplayString(reader["Desc2"]),
                            ManufacturerPartNumber = CleanCodeField(reader["ManufacturingPart"]),
                            Customer = ToDisplayString(reader["Customer"]),
                            PartType = ToDisplayString(reader["PartType"])
                        });
                    }
                }
            }

            return results;
        }

        private static string RangeFieldColumn(MaxOrderRangeField? field)
        {
            if (!field.HasValue) return null;
            switch (field.Value)
            {
                case MaxOrderRangeField.OrderNumber: return "dbo.Order_Master.ORDNUM_10";
                case MaxOrderRangeField.Customer: return "dbo.Part_Master.COMCDE_01";
                case MaxOrderRangeField.Part: return "dbo.Order_Master.PRTNUM_10";
                default: return null;
            }
        }

        // FRMPLN_10's exact column type on the buildmachine's MAX database isn't confirmed —
        // could be bit, a Y/N char, or a numeric flag — so this reads defensively instead of an
        // unconditional Convert.ToBoolean that would throw for a non-bit type.
        private static bool ToBool(object value)
        {
            if (value == null || value == DBNull.Value) return false;
            if (value is bool b) return b;
            if (value is string s) return s.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase) || s.Trim() == "1";
            try { return Convert.ToInt32(value) != 0; } catch { return false; }
        }

        private static string ToDisplayString(object value) =>
            value == null || value == DBNull.Value ? null : value.ToString().Trim();

        // PRTNUM_10/VIEWER_01 turned out to be padded with trailing periods (not just spaces) in
        // this MAX environment — plain .Trim() only strips whitespace, so "5100565..." survived it
        // and showed up as-is in the grid (which looked like leftover DataGridView truncation, but
        // wasn't). Scoped to just these two short "code" fields rather than every string field,
        // since a free-text field like Desc1/Reference could legitimately end in a real period.
        private static string CleanCodeField(object value)
        {
            var text = ToDisplayString(value);
            return text?.TrimEnd('.', ' ');
        }

        private static decimal? ToNullableDecimal(object value) =>
            value == null || value == DBNull.Value ? (decimal?)null : Convert.ToDecimal(value);
    }
}
