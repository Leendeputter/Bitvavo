using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Interfaces;

namespace Procurement.Erp
{
    /// <summary>
    /// Reads MAX's own Part_Vendor cross-reference table (the AdminConnectionString, same as
    /// MaxOrderRepository) — the user-supplied query, scoped to a specific set of PartIds so a
    /// sync only asks MAX about the articles it actually needs (the current open-orders batch)
    /// instead of pulling the whole table every time.
    /// </summary>
    public class MaxVendorPartRepository
    {
        private readonly ISessionContext _session;

        public MaxVendorPartRepository(ISessionContext session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public async Task<IReadOnlyList<MaxVendorPart>> GetForPartsAsync(IReadOnlyCollection<string> partIds)
        {
            if (partIds == null || partIds.Count == 0)
                return new List<MaxVendorPart>();

            var results = new List<MaxVendorPart>();
            var partList = partIds.ToList();
            var paramNames = partList.Select((p, i) => $"@part{i}").ToList();

            var sql = $@"
SELECT
    PRTNUM_07 AS PartID,
    VENID_07 AS VendorID,
    VENPRT_07 AS VendorPart
FROM dbo.Part_Vendor
WHERE PRTNUM_07 IN ({string.Join(",", paramNames)})";

            using (var conn = new SqlConnection(_session.AdminConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                for (var i = 0; i < partList.Count; i++)
                    cmd.Parameters.AddWithValue(paramNames[i], partList[i]);

                await conn.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new MaxVendorPart
                        {
                            PartId = reader["PartID"].ToString(),
                            VendorId = reader["VendorID"] as string,
                            VendorPart = reader["VendorPart"] as string
                        });
                    }
                }
            }

            return results;
        }
    }
}
