using System;
using System.Threading.Tasks;
using Procurement.Core.Interfaces;
using Procurement.Core.Models;

namespace Procurement.Erp
{
    /// <summary>
    /// Creates/updates actual MAX Purchase Orders — the write-side counterpart to
    /// MaxOrderRepository's read-side Order_Master/Part_Master queries.
    ///
    /// NOT YET IMPLEMENTED. MaxOrderRepository could be built directly because its exact
    /// Order_Master/Part_Master query (table names, column names, joins) was supplied up front —
    /// nothing here has that yet for MAX's PO-creation side. Guessing at table/column names and
    /// writing them into a live, shared ERP database is a real risk: a wrong guess doesn't
    /// necessarily fail loudly, it can silently write incomplete or malformed data into a system
    /// other people rely on for real purchasing. So this deliberately throws instead of attempting
    /// anything until it's been checked against MAX's actual schema.
    ///
    /// What's needed before this can be filled in (bring this to whoever manages MAX/the ERP setup):
    ///  1. Does MAX expose its own API or stored procedure for creating a PO — mirroring how login
    ///     already goes through the MaxSQL/MaxSecurity library rather than raw table access? That's
    ///     strongly preferred over raw INSERTs: it keeps MAX's own validations/business rules/
    ///     triggers intact instead of bypassing them.
    ///  2. If raw table writes really are the only option: which table(s) hold the PO header and
    ///     lines, and which columns are required for a valid row? Ideally a SELECT TOP 5 dump of an
    ///     existing, manually-created PO — the same way Order_Master's exact columns were supplied
    ///     for the read side.
    ///  3. How are MAX PO numbers generated — auto-numbered by MAX itself on insert, or does the
    ///     caller need to reserve/supply one? Matters for the idempotency story (spec §10): a retry
    ///     after a timeout must not create a second PO for the same submission.
    ///  4. What status values/transitions does MAX expect over a PO's lifecycle — mirrors
    ///     Order_Master.STATUS_10 ("1"=Planned/"2"=Approved) on the read side; is there an
    ///     equivalent set for POs (submitted/confirmed/shipped/...)?
    ///  5. Is there a MAX test/sandbox company to validate against before writing to a live one
    ///     (distinct from this app's own Unitron_test, which only covers this app's own tables)?
    /// </summary>
    public class MaxPurchaseOrderRepository
    {
        private readonly ISessionContext _session;

        public MaxPurchaseOrderRepository(ISessionContext session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>Returns MAX's own PO number once implemented.</summary>
        public Task<string> CreatePurchaseOrderAsync(PurchaseOrderDraft draft)
        {
            throw new NotImplementedException(
                "MAX PO-aanmaak is nog niet geïmplementeerd — MAX's eigen tabel-/kolomstructuur voor " +
                "Purchase Orders is nog niet geverifieerd. Zie de class-comment van " +
                "MaxPurchaseOrderRepository voor precies welke informatie daarvoor nodig is. Zolang dit " +
                "leeg blijft, staat MaxErpConnector.UseMockPurchaseOrders op true (de standaard) en komt " +
                "deze methode niet aan bod.");
        }

        public Task UpdateStatusAsync(string erpPoNumber, string status)
        {
            throw new NotImplementedException(
                "MAX PO-statusupdate is nog niet geïmplementeerd — zie CreatePurchaseOrderAsync.");
        }
    }
}
