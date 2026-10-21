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
    dbo.Order_Master.LINNUM_10 AS LineNumber,
    dbo.Order_Master.DELNUM_10 AS DeliveryNumber,
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
                            LineNumber = ToDisplayString(reader["LineNumber"]),
                            DeliveryNumber = ToDisplayString(reader["DeliveryNumber"]),
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

        /// <summary>
        /// Looks up specific PR rows by MAX order number (Order_Master.ORDNUM_10) with no
        /// STATUS_10/CURDUE_10/range filtering at all — used by MaxErpConnector to reconcile
        /// already-synced local requests against MAX on every Query, regardless of which
        /// status/date filter the user currently has selected. An existence check must not depend
        /// on that filter, or narrowing it (e.g. unchecking "1 - Planned") would look exactly like
        /// every excluded order having been deleted in MAX. Returns only Quantity/DueDate/Status —
        /// enough to detect "still exists, drifted" vs. "gone"; the full row (Desc1/Desc2/etc.) is
        /// still only refreshed for orders present in the current filtered GetOpenOrdersAsync batch.
        /// </summary>
        // Same SQL Server 2100-parameter-per-command ceiling as CalculatePoDueDatesAsync (see that
        // field's comment) — one parameter per order number here, so this has twice the headroom,
        // but ReconcileAgainstMaxAsync passes every open local request on every Query, the same
        // data volume that already tripped the other query. Chunked pre-emptively rather than
        // waiting for this one to fail the same way.
        private const int GetByOrderNumbersBatchSize = 2000;

        public async Task<IReadOnlyDictionary<string, MaxOrder>> GetByOrderNumbersAsync(IReadOnlyCollection<string> orderNumbers)
        {
            var result = new Dictionary<string, MaxOrder>(StringComparer.OrdinalIgnoreCase);
            if (orderNumbers == null || orderNumbers.Count == 0) return result;

            var numbers = orderNumbers.Distinct().ToList();
            for (var offset = 0; offset < numbers.Count; offset += GetByOrderNumbersBatchSize)
            {
                var batch = numbers.Skip(offset).Take(GetByOrderNumbersBatchSize).ToList();
                await GetByOrderNumbersBatchAsync(batch, result);
            }

            return result;
        }

        private async Task GetByOrderNumbersBatchAsync(IReadOnlyList<string> batch, Dictionary<string, MaxOrder> result)
        {
            var paramNames = batch.Select((_, i) => $"@o{i}").ToList();

            var sql = $@"
SELECT
    dbo.Order_Master.ORDNUM_10 AS [Order],
    dbo.Order_Master.CURQTY_10 AS CurrentQty,
    dbo.Order_Master.CURDUE_10 AS DueDate,
    dbo.Order_Master.STATUS_10 AS Status
FROM dbo.Order_Master
WHERE dbo.Order_Master.ORDNUM_10 IN ({string.Join(",", paramNames)})";

            using (var conn = new SqlConnection(_session.AdminConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                for (var i = 0; i < batch.Count; i++)
                    cmd.Parameters.AddWithValue(paramNames[i], batch[i]);

                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var orderNumber = reader["Order"].ToString().Trim();
                        result[orderNumber] = new MaxOrder
                        {
                            OrderNumber = orderNumber,
                            CurrentQty = Convert.ToInt32(reader["CurrentQty"]),
                            DueDate = reader["DueDate"] as DateTime?,
                            Status = reader["Status"].ToString().Trim()
                        };
                    }
                }
            }
        }

        // Same SQL Server 2100-parameter-per-command ceiling as the other batched lookups above —
        // 1 parameter per PartId here, same headroom as GetByOrderNumbersAsync.
        private const int GetPreferredVendorIdsBatchSize = 2000;

        /// <summary>
        /// Part_Master.RECVEN_01 — the article's preferred/preferential vendor ID, confirmed by the
        /// user (okt 2026) for display in MainForm's requests grid ("Vendor ID van de
        /// voorkeursleverancier"). Deliberately a live, batched lookup rather than a new stored column
        /// on PurchaseRequestLine, the same choice already made for PO DueDate (MaxOrderRepository.
        /// CalculatePoDueDatesAsync) — a new EF-mapped column triggers the schema-regen script, which
        /// also wipes Supplier credentials (see README's "Les uit deze sessie" note), so this avoids
        /// that risk entirely for what's purely a display convenience.
        /// </summary>
        public async Task<IReadOnlyDictionary<string, string>> GetPreferredVendorIdsByPartIdsAsync(IReadOnlyCollection<string> partIds)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (partIds == null || partIds.Count == 0) return result;

            var distinct = partIds.Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
            for (var offset = 0; offset < distinct.Count; offset += GetPreferredVendorIdsBatchSize)
            {
                var batch = distinct.Skip(offset).Take(GetPreferredVendorIdsBatchSize).ToList();
                await GetPreferredVendorIdsBatchAsync(batch, result);
            }

            return result;
        }

        private async Task GetPreferredVendorIdsBatchAsync(IReadOnlyList<string> batch, Dictionary<string, string> result)
        {
            var paramNames = batch.Select((_, i) => $"@p{i}").ToList();

            var sql = $@"
SELECT PRTNUM_01 AS PartID, RECVEN_01 AS PreferredVendorId
FROM dbo.Part_Master
WHERE PRTNUM_01 IN ({string.Join(",", paramNames)})";

            using (var conn = new SqlConnection(_session.AdminConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                for (var i = 0; i < batch.Count; i++)
                    cmd.Parameters.AddWithValue(paramNames[i], batch[i]);

                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var partId = CleanCodeField(reader["PartID"]);
                        if (string.IsNullOrEmpty(partId)) continue;
                        result[partId] = CleanCodeField(reader["PreferredVendorId"]);
                    }
                }
            }
        }

        // 3 parameters per key here (order+line+delivery, matching Order_Master's own composite key
        // — see MaxPurchaseOrderRepository.ApplyLineConfirmationAsync's identical WHERE) instead of 1
        // per key like the other batched lookups above, so a third of GetByOrderNumbersBatchSize for
        // the same SQL Server 2100-parameter-per-command ceiling.
        private const int GetLineStatusesBatchSize = 600;

        /// <summary>
        /// Order_Master.STATUS_10 per PO line, keyed by the exact (ORDNUM_10, LINNUM_10, DELNUM_10)
        /// triple MaxPurchaseOrderRepository.ApplyLineConfirmationAsync already matches Order_Master
        /// rows on — used by OrdersForm to hide already-Completed (Status 4) lines by default (user
        /// request, 20 okt 2026, "PO Schedule Inquiry" screenshot: "Status 4 Orders (Completed)").
        /// Deliberately a live, batched lookup rather than a new stored column on PurchaseOrderLine,
        /// the same choice already made for PO DueDate/preferred vendor above — a new EF-mapped
        /// column triggers the schema-regen script, which also wipes Supplier credentials.
        /// </summary>
        public async Task<IReadOnlyDictionary<(string OrderNumber, string LineNumber, string DeliveryNumber), string>> GetLineStatusesAsync(
            IReadOnlyCollection<(string OrderNumber, string LineNumber, string DeliveryNumber)> keys)
        {
            var result = new Dictionary<(string, string, string), string>();
            if (keys == null || keys.Count == 0) return result;

            var distinct = keys
                .Where(k => !string.IsNullOrEmpty(k.OrderNumber) && !string.IsNullOrEmpty(k.LineNumber) && !string.IsNullOrEmpty(k.DeliveryNumber))
                .Distinct()
                .ToList();
            for (var offset = 0; offset < distinct.Count; offset += GetLineStatusesBatchSize)
            {
                var batch = distinct.Skip(offset).Take(GetLineStatusesBatchSize).ToList();
                await GetLineStatusesBatchAsync(batch, result);
            }

            return result;
        }

        private async Task GetLineStatusesBatchAsync(
            IReadOnlyList<(string OrderNumber, string LineNumber, string DeliveryNumber)> batch,
            Dictionary<(string, string, string), string> result)
        {
            var where = new StringBuilder();
            var parameters = new List<SqlParameter>();
            for (var i = 0; i < batch.Count; i++)
            {
                if (i > 0) where.Append(" OR ");
                where.Append($"(ORDNUM_10 = @o{i} AND LINNUM_10 = @l{i} AND DELNUM_10 = @d{i})");
                parameters.Add(new SqlParameter($"@o{i}", batch[i].OrderNumber));
                parameters.Add(new SqlParameter($"@l{i}", batch[i].LineNumber));
                parameters.Add(new SqlParameter($"@d{i}", batch[i].DeliveryNumber));
            }

            var sql = $@"
SELECT ORDNUM_10 AS [Order], LINNUM_10 AS LineNumber, DELNUM_10 AS DeliveryNumber, STATUS_10 AS Status
FROM dbo.Order_Master
WHERE {where}";

            using (var conn = new SqlConnection(_session.AdminConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddRange(parameters.ToArray());

                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var key = (
                            reader["Order"].ToString().Trim(),
                            reader["LineNumber"].ToString().Trim(),
                            reader["DeliveryNumber"].ToString().Trim());
                        result[key] = reader["Status"].ToString().Trim();
                    }
                }
            }
        }

        /// <summary>
        /// Part_Master-only lookup (no Order_Master join — there's no order yet), used by two
        /// unrelated callers: "Nieuwe testaanvraag toevoegen" (Desc1/Desc2/PartType/Customer, the
        /// same MAX fields a real synced order already gets via GetOpenOrdersAsync) and
        /// MaxPurchaseOrderRepository.BuildPurchaseOrderLine (Stock/Buyer/Planner, confirmed by the
        /// user against a real created PO — okt 2026). Returns null if the Part_Master row doesn't
        /// exist (e.g. a made-up test artikelnummer) rather than throwing — an expected, common case
        /// for the test-request form, not an error.
        /// </summary>
        public async Task<MaxPartInfo> GetPartByIdAsync(string partId)
        {
            if (string.IsNullOrWhiteSpace(partId)) return null;

            const string sql = @"
SELECT
    dbo.Part_Master.PMDES1_01 AS Desc1,
    dbo.Part_Master.PMDES2_01 AS Desc2,
    dbo.Part_Master.COMCDE_01 AS Customer,
    dbo.Part_Master.TYPE_01 AS PartType,
    dbo.Part_Master.DELSTK_01 AS Stock,
    dbo.Part_Master.BUYER_01 AS Buyer,
    dbo.Part_Master.PLANID_01 AS Planner
FROM dbo.Part_Master
WHERE dbo.Part_Master.PRTNUM_01 = @partId";

            using (var conn = new SqlConnection(_session.AdminConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@partId", partId.Trim());

                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync()) return null;

                    return new MaxPartInfo
                    {
                        Desc1 = ToDisplayString(reader["Desc1"]),
                        Desc2 = ToDisplayString(reader["Desc2"]),
                        Customer = ToDisplayString(reader["Customer"]),
                        PartType = ToDisplayString(reader["PartType"]),
                        Stock = ToDisplayString(reader["Stock"]),
                        Buyer = ToDisplayString(reader["Buyer"]),
                        Planner = ToDisplayString(reader["Planner"])
                    };
                }
            }
        }

        /// <summary>
        /// Looks up dbo.Buyers.BUYID_95 by matching the app's resolved Windows username (ISessionContext.
        /// UserName — already the bare username, domain prefix stripped, see LoginForm.
        /// ResolveWindowsUserName) against Buyers.UDFKEY_95, where the user themselves has started
        /// storing it (okt 2026) — not a MAX-native link, a manual convention the user set up
        /// specifically so this app could find it. Returns null if nothing matches (a buyer who
        /// hasn't been given a UDFKEY_95 value yet, or any other mismatch) rather than throwing — the
        /// caller falls back to "000", the same documented fallback as a missing article-level Buyer.
        /// </summary>
        public async Task<string> GetBuyerIdByWindowsUsernameAsync(string windowsUsername)
        {
            if (string.IsNullOrWhiteSpace(windowsUsername)) return null;

            const string sql = "SELECT TOP 1 BUYID_95 FROM dbo.Buyers WHERE UDFKEY_95 = @username";

            using (var conn = new SqlConnection(_session.AdminConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@username", windowsUsername.Trim());

                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync()) return null;
                    return ToDisplayString(reader["BUYID_95"]);
                }
            }
        }

        /// <summary>
        /// Calls MAX's own SQLCLR scalar function dbo.CalculateMRPDueDateFromPODueDate
        /// (ExactMAXSQLUtilities.DateFunctions). Despite the name (and despite this method's own
        /// name), this is the function that belongs here, not CalculatePODueDateFromMRPDueDate —
        /// the first native-function attempt (okt 2026) used that one and came back a full lead
        /// time *too late* (one week after the MRP due date instead of before it), the exact same
        /// symptom as this app's very first hand-rolled attempt (add-calendar-days). The user's
        /// own confirmed business rule has always been that a PO line's due date must be the MRP
        /// date *minus* the lead time (see the two hand-rolled attempts below) — CalculateMRPDue-
        /// DateFromPODueDate, called with the MRP-synced RequiredDate in the @PODate slot, is what
        /// actually produces that. Still unconfirmed against a real MAX test as of this change —
        /// ask the user to verify against a newly created PO before relying on this.
        ///
        /// Supersedes this app's own two earlier hand-rolled attempts to replicate this by hand with
        /// Part_Master.PURSTK_01 + working-day arithmetic: MAX's own function already accounts for
        /// whatever MAX itself considers (working days, any holiday calendar, etc.), so there's
        /// nothing left to get wrong by reimplementing it — once the right function is confirmed.
        /// Falls back to the given MRP date unchanged if the function returns NULL (nothing in the
        /// user-supplied definition suggests it would, but a date this method can't compute is a
        /// worse outcome than falling back to the un-adjusted one it was given).
        /// </summary>
        public async Task<DateTime> CalculatePoDueDateAsync(DateTime mrpDate, string partNumber)
        {
            const string sql = "SELECT dbo.CalculateMRPDueDateFromPODueDate(@MrpDate, @PartNum)";

            using (var conn = new SqlConnection(_session.AdminConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@MrpDate", mrpDate);
                cmd.Parameters.AddWithValue("@PartNum", partNumber ?? (object)DBNull.Value);

                await conn.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? mrpDate : (DateTime)result;
            }
        }

        // SQL Server rejects a command with more than 2100 parameters ("The incoming request has too
        // many parameters") — hit for real (20 okt 2026) once the requests grid had enough open PRs
        // that CalculatePoDueDatesAsync's one-query-for-everything VALUES table, at 2 parameters per
        // distinct (date, article) pair, crossed that line. 900 rows (1800 parameters) leaves
        // comfortable headroom under the limit while still keeping this to a handful of round-trips
        // even for a large grid.
        private const int CalculatePoDueDatesBatchSize = 900;

        /// <summary>
        /// Batched form of CalculatePoDueDateAsync, for displaying many lines at once (MainForm's
        /// requests/lines grids) without one round-trip per row — each batch evaluates the function
        /// once per distinct (date, article) pair via a VALUES table, the same batching idiom
        /// GetByOrderNumbersAsync already uses for an IN-list, chunked (CalculatePoDueDatesBatchSize)
        /// to stay under SQL Server's 2100-parameter-per-command limit. Pairs with no RequiredDate are
        /// the caller's responsibility to filter out before calling this (a function call needs both
        /// arguments).
        /// </summary>
        public async Task<IReadOnlyDictionary<(DateTime MrpDate, string PartNumber), DateTime>> CalculatePoDueDatesAsync(
            IReadOnlyCollection<(DateTime MrpDate, string PartNumber)> items)
        {
            var result = new Dictionary<(DateTime, string), DateTime>();
            if (items == null || items.Count == 0) return result;

            var distinct = items.Distinct().ToList();
            for (var offset = 0; offset < distinct.Count; offset += CalculatePoDueDatesBatchSize)
            {
                var batch = distinct.Skip(offset).Take(CalculatePoDueDatesBatchSize).ToList();
                await CalculatePoDueDatesBatchAsync(batch, result);
            }

            return result;
        }

        private async Task CalculatePoDueDatesBatchAsync(
            IReadOnlyList<(DateTime MrpDate, string PartNumber)> batch,
            Dictionary<(DateTime, string), DateTime> result)
        {
            var valueRows = batch.Select((_, i) => $"(@d{i}, @p{i})");
            var sql = $@"
SELECT v.MrpDate, v.PartNum, dbo.CalculateMRPDueDateFromPODueDate(v.MrpDate, v.PartNum) AS PoDueDate
FROM (VALUES {string.Join(", ", valueRows)}) AS v(MrpDate, PartNum)";

            using (var conn = new SqlConnection(_session.AdminConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                for (var i = 0; i < batch.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@d{i}", batch[i].MrpDate);
                    cmd.Parameters.AddWithValue($"@p{i}", batch[i].PartNumber);
                }

                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var mrpDate = (DateTime)reader["MrpDate"];
                        var partNum = reader["PartNum"].ToString();
                        var poDueDate = reader["PoDueDate"] as DateTime? ?? mrpDate;
                        result[(mrpDate, partNum)] = poDueDate;
                    }
                }
            }
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
