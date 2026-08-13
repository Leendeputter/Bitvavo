using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Interfaces;

namespace Procurement.Erp
{
    /// <summary>
    /// Reads open orders straight from MAX's own Order_Master/Part_Master tables (the
    /// AdminConnectionString resolved at login — a real per-company MAX database, not this app's
    /// own "Unitron" tables). Query and part-type filter ('B','D','Y') as supplied by the user;
    /// the order-status filter is parameterized so the UI can pick which statuses to include.
    /// </summary>
    public class MaxOrderRepository
    {
        private readonly ISessionContext _session;

        public MaxOrderRepository(ISessionContext session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public async Task<IReadOnlyList<MaxOrder>> GetOpenOrdersAsync(IReadOnlyCollection<string> statuses)
        {
            if (statuses == null || statuses.Count == 0)
                return new List<MaxOrder>();

            var results = new List<MaxOrder>();
            var statusList = statuses.ToList();
            var paramNames = statusList.Select((s, i) => $"@status{i}").ToList();

            var sql = $@"
SELECT
    Order_Master.ORDNUM_10 AS OrderNumber,
    Order_Master.ORDER_10 AS OrderLong,
    Order_Master.PRTNUM_10 AS PartId,
    Order_Master.CURQTY_10 AS CurrentQty,
    Order_Master.STATUS_10 AS Status,
    Order_Master.CURDUE_10 AS DueDate,
    Order_Master.ORDREF_10 AS Reference,
    Part_Master.PMDES1_01 AS Desc1,
    Part_Master.PMDES2_01 AS Desc2,
    Part_Master.VIEWER_01 AS ManufacturingPart,
    Part_Master.TYPE_01 AS PartType
FROM Order_Master
INNER JOIN Part_Master ON Order_Master.PRTNUM_10 = Part_Master.PRTNUM_01
WHERE Part_Master.TYPE_01 IN ('B','D','Y')
  AND Order_Master.STATUS_10 IN ({string.Join(",", paramNames)})
ORDER BY Order_Master.ORDNUM_10";

            using (var conn = new SqlConnection(_session.AdminConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                for (var i = 0; i < statusList.Count; i++)
                    cmd.Parameters.AddWithValue(paramNames[i], statusList[i]);

                await conn.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var desc1 = reader["Desc1"] as string;
                        var desc2 = reader["Desc2"] as string;

                        results.Add(new MaxOrder
                        {
                            OrderNumber = reader["OrderNumber"].ToString(),
                            OrderLong = reader["OrderLong"] as string,
                            PartId = reader["PartId"].ToString(),
                            CurrentQty = Convert.ToInt32(reader["CurrentQty"]),
                            Status = reader["Status"].ToString(),
                            DueDate = reader["DueDate"] as DateTime?,
                            Reference = reader["Reference"] as string,
                            Description = string.Join(" ", new[] { desc1, desc2 }.Where(s => !string.IsNullOrWhiteSpace(s))),
                            ManufacturerPartNumber = reader["ManufacturingPart"] as string,
                            PartType = reader["PartType"] as string
                        });
                    }
                }
            }

            return results;
        }
    }
}
