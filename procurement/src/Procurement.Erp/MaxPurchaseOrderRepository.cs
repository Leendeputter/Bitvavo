using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MAX50;
using MaxOrder;
using Procurement.Core.Entities;
using Procurement.Core.Interfaces;
using Procurement.Core.Models;
using Procurement.Data.Repositories;

namespace Procurement.Erp
{
    /// <summary>
    /// Creates actual MAX Purchase Orders via MaxOrderNET's MaxOrderModule — the write-side
    /// counterpart to MaxOrderRepository's read-side Order_Master/Part_Master queries. Uses MAX's
    /// own module rather than raw SQL, on purpose: that keeps MAX's own validations/business
    /// rules/triggers intact instead of bypassing them, the same way LoginForm already goes through
    /// MaxSQL rather than querying ExactRMCompanies by hand.
    ///
    /// What a row in our requests grid actually is in MAX is a Purchase Requisition (PR) — an
    /// Order_Master row with TYPE_10 for that kind of order — which needs to be converted into an
    /// actual PO line once ordered, and then removed so it stops reappearing as an open request on
    /// the next Query. This class does that as two explicit steps per line, using MaxOrderModule's
    /// own confirmed methods (decompiled source for both was supplied directly):
    ///  1. AddPODetail(Order_Master, IncOrdRev, CreateHeader, FixVar, RoundType) creates the PO
    ///     line. Called with CreateHeader:true only for the first line of a new PO — that makes
    ///     AddPODetail build the Purchase_Order_Code header itself (reading Vendor_Master for
    ///     TERMS_08/FOBPT_08/SHPVIA_08/SHPINS_08/GSHIP_08/GTERM_08/CURR_08 — confirmed by reading
    ///     AddPODetail's own source, so no header fields are hand-guessed here anymore) and assigns
    ///     the newly-minted PO number onto that line's own ORDNUM_10. Every subsequent line for the
    ///     same PO is passed CreateHeader:false with ORDNUM_10 set to that same PO number and its
    ///     own sequential LINNUM_10 ("02", "03", ...).
    ///  2. DeletePurchaseRequisitionLineItem(ordnum, linnum, delnum, out errMsg) removes the
    ///     original PR row (Order_Master.ORDNUM_10/LINNUM_10/DELNUM_10 — the composite key synced
    ///     onto PurchaseRequest.ErpRequestNumber/PurchaseRequestLine.MaxLineNumber/MaxDeliveryNumber
    ///     by MaxOrderRepository) once its PO line exists, so it stops showing up as an open request.
    ///     Best-effort: a failure here is logged, not thrown — the PO line from step 1 is already
    ///     correctly placed at that point, and aborting the whole batch over a leftover PR row would
    ///     be a worse outcome than leaving that one row for manual cleanup.
    ///
    /// Confirmed against MaxOrderModule's full source (supplied directly): GetErrors() is a public
    /// method (used below the same way AddPOHeading/DeletePurchaseRequisitionLineItem use it
    /// internally for their own out-parameters), and PR rows use "00" for both LINNUM_10 and
    /// DELNUM_10 (not "01" — confirmed throughout, e.g. AddPurReq), which is what
    /// RemoveOriginalOrder's fallback now matches. Also confirmed: inside AddPODetail's
    /// CreateHeader:true branch, right after minting the PO number, it sets
    /// OM.LINNUM_10 = "01"/OM.DELNUM_10 = "01" and ORDER_10 = ORDNUM_10+LINNUM_10+DELNUM_10 itself
    /// — matches leaving those blank on the first line here and letting AddPODetail fill them in.
    /// For every later line (CreateHeader:false) that same ORDER_10 assignment is NOT reached
    /// (it's inside the CreateHeader branch only), so BuildPurchaseOrderLine computes it the same
    /// way itself for those lines instead of risking a blank ORDER_10 on the inserted row.
    ///
    /// Still open / worth confirming before relying on this in production:
    ///  1. MaxOrderModule.AssignPRsToPO(int, string TargetOrder, List&lt;OrderAssign&gt;, bool, bool,
    ///     bool, bool, bool, string) — and its sibling AssignPONumber, which wraps a bulk
    ///     "auto-assign every pending/approved PR to POs" operation rather than a caller-chosen
    ///     batch — look like they may be the more "correct", atomic way to do this: converting an
    ///     existing PR row in place (re-keying it onto the target PO, folding step 2 into step 1 as
    ///     a single MAX-native operation) rather than inserting a brand new row and separately
    ///     deleting the old one. Not used here because OrderAssign's full field list isn't known,
    ///     TargetOrder's exact semantics (must it already exist, or can it be created inline — the
    ///     AddPOHeading call visible inside AssignPRsToPO's decompiled body sits behind a condition
    ///     that looks permanently false) aren't confirmed, and it's unclear whether either lets the
    ///     caller override quantity/price (vs. always carrying over the PR's own CURQTY_10) — which
    ///     matters here, since sourcing can compute an OfferedQuantity that differs from the PR's
    ///     originally requested quantity (order multiples/MOQ rounding). ChangePurReq(Order_Master,
    ///     bool AssignPO, bool ApprovePR) also showed up and looks closer to MAX's own PR-approval
    ///     workflow (quantity/status adjustments on the PR itself) than to PR-to-PO conversion.
    ///  2. FixVar ("F") and RoundType (3) — passed through unchanged from the supplied working
    ///     example; still don't know precisely what they control.
    ///  3. IncOrdRev on ChangePODetail (used by ApplyConfirmationAsync below) — passed through as
    ///     true, same value AddPODetail already uses, but its exact effect isn't confirmed either.
    /// </summary>
    public class MaxPurchaseOrderRepository
    {
        private readonly ISessionContext _session;
        private readonly SupplierRepository _supplierRepository;
        private readonly PurchaseRequestRepository _purchaseRequestRepository;

        public MaxPurchaseOrderRepository(
            ISessionContext session,
            SupplierRepository supplierRepository,
            PurchaseRequestRepository purchaseRequestRepository)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
            _purchaseRequestRepository = purchaseRequestRepository ?? throw new ArgumentNullException(nameof(purchaseRequestRepository));
        }

        /// <summary>Returns MAX's own PO number (minted by AddPODetail on the first line, CreateHeader:true) plus each line's MAX LINNUM_10/DELNUM_10, so the caller can persist them for a later confirmation update (see ApplyConfirmationAsync).</summary>
        public async Task<PurchaseOrderCreationResult> CreatePurchaseOrderAsync(PurchaseOrderDraft draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (draft.Lines == null || draft.Lines.Count == 0)
                throw new ArgumentException("PurchaseOrderDraft has no lines.", nameof(draft));

            var supplier = await _supplierRepository.GetByCodeAsync(draft.SupplierCode);
            if (supplier == null || string.IsNullOrEmpty(supplier.VendorId))
                throw new InvalidOperationException(
                    $"Supplier '{draft.SupplierCode}' heeft geen VendorId (MAX Part_Vendor.VENID_07) ingesteld — nodig om een PO in MAX aan te maken. Stel dit in via Instellingen -> Suppliers.");

            var result = new PurchaseOrderCreationResult();

            using (var primary = new SqlConnection(_session.PrimaryConnectionString))
            using (var admin = new SqlConnection(_session.AdminConnectionString))
            {
                primary.Open();
                admin.Open();

                var log = MyLogManager.Create(_session.LogFile, _session.LogPath, true).GetCurrentClassLogger();

                // MaxOrderModule implements IDisposable (confirmed in the decompiled source) —
                // disposed here even though the SqlConnections it wraps are already closed by the
                // outer using blocks, since it's unclear whether it holds any other unmanaged state.
                using (var maxOrderModule = new MaxOrderModule(primary, admin, _session.CompanyId, log, _session.LicensePath, _session.UserName))
                {
                    string erpPoNumber = null;
                    var lineNumber = 1;

                    foreach (var draftLine in draft.Lines)
                    {
                        var requestLine = await _purchaseRequestRepository.GetLineByIdAsync(draftLine.PurchaseRequestLineId);
                        if (requestLine == null)
                            throw new InvalidOperationException($"PurchaseRequestLine {draftLine.PurchaseRequestLineId} niet gevonden — kan geen MAX PO-regel aanmaken.");

                        var isFirstLine = erpPoNumber == null;
                        var poLine = BuildPurchaseOrderLine(supplier.VendorId, requestLine, draftLine, erpPoNumber, lineNumber, isFirstLine, _session.UserName);

                        var added = maxOrderModule.AddPODetail(poLine, IncOrdRev: true, CreateHeader: isFirstLine, FixVar: "F", RoundType: 3);
                        if (!added)
                        {
                            var errorMessage = maxOrderModule.GetErrors();
                            throw new InvalidOperationException(
                                $"MAX AddPODetail is mislukt voor regel {draftLine.PurchaseRequestLineId}" +
                                (erpPoNumber != null ? $" (PO {erpPoNumber})" : "") + $": {errorMessage}");
                        }

                        if (isFirstLine)
                            erpPoNumber = poLine.ORDNUM_10;
                        lineNumber++;

                        result.Lines.Add(new PurchaseOrderCreationResultLine
                        {
                            PurchaseRequestLineId = draftLine.PurchaseRequestLineId,
                            MaxLineNumber = poLine.LINNUM_10,
                            MaxDeliveryNumber = poLine.DELNUM_10
                        });

                        RemoveOriginalOrder(maxOrderModule, log, requestLine);
                    }

                    result.ErpPoNumber = erpPoNumber;
                    return result;
                }
            }
        }

        /// <summary>
        /// Best-effort — see the class comment for why a failure here doesn't abort PO creation.
        /// Logger typed via NLog.Logger explicitly (not a bare "Logger") since MyLogManager.Create
        /// returns an NLog.LogFactory per ProcurementSession's own comment — avoids guessing whether
        /// a same-named type also exists in the MAX50 namespace already imported into this file.
        /// </summary>
        private void RemoveOriginalOrder(MaxOrderModule maxOrderModule, NLog.Logger log, PurchaseRequestLine requestLine)
        {
            var ordnum = requestLine.PurchaseRequest?.ErpRequestNumber;
            if (string.IsNullOrEmpty(ordnum) || string.IsNullOrEmpty(requestLine.MaxLineNumber))
            {
                log.Warn($"Kan originele MAX PR niet verwijderen voor PurchaseRequestLine {requestLine.Id} — ordnum/linnum ontbreekt (mogelijk gesynct vóór MaxLineNumber/MaxDeliveryNumber bestonden).");
                return;
            }

            // "01" as a fallback would have been wrong: PR rows use "00" for both LINNUM_10 and
            // DELNUM_10 (confirmed throughout MaxOrderModule's own source, e.g. AddPurReq), unlike
            // PO lines which start at "01" — this fallback only matters for rows synced before
            // MaxDeliveryNumber existed anyway, since it's otherwise always the real synced value.
            var deleted = maxOrderModule.DeletePurchaseRequisitionLineItem(ordnum, requestLine.MaxLineNumber, requestLine.MaxDeliveryNumber ?? "00", out var deleteErrorMessage);
            if (!deleted)
                log.Warn($"MAX DeletePurchaseRequisitionLineItem is mislukt voor {ordnum}-{requestLine.MaxLineNumber}-{requestLine.MaxDeliveryNumber}: {deleteErrorMessage}");
        }

        /// <summary>
        /// No longer used for a plain status-text update — MAX has no confirmed field for that, and
        /// this app's own PurchaseOrder.Status already tracks it locally. Kept as a safe no-op
        /// (rather than the NotImplementedException it used to throw) purely so
        /// ProcurementEngine.PlaceSupplierPurchaseOrderAsync's automatic post-placement call doesn't
        /// crash real-mode order placement right after MAX has already accepted the real PO — the
        /// actual MAX-visible confirmation write is ApplyConfirmationAsync below, run later/separately
        /// once the supplier has actually responded.
        /// </summary>
        public Task UpdateStatusAsync(string erpPoNumber, string status) => Task.CompletedTask;

        /// <summary>
        /// Writes a supplier's order confirmation into the three specific MAX fields the business
        /// actually tracks this in (confirmed directly by the user, not guessed):
        /// Purchase_Order_Code.CONFRM_16 (leverancier's SalesOrder/confirmation nummer, char(15)),
        /// Order_Master.ORDREF_10 per regel (char(25), "O "/"OP " prefix — zie
        /// BuildConfirmedReference), en Order_Master.CURDUE_10 (bijgewerkt naar de bevestigde
        /// leverdatum, alleen bij afwijking). Read-modify-write on an EXISTING row via
        /// ChangePOHeading/ChangePODetail — unlike AddPODetail (always a new row), a wrong field here
        /// touches a real, already-placed PO, so this stays deliberately narrow: only the fields
        /// above are ever changed, everything else on the row is read back unmodified. The user
        /// confirmed that a plain CURDUE_10 update (without chasing whatever else ChangePODetail
        /// might recalculate internally) is acceptable here — any Requirement_Detail side effect
        /// gets corrected by the daily MRP run regardless.
        ///
        /// Best-effort per line/header, same reasoning as RemoveOriginalOrder: a failed write is
        /// logged, not thrown — the confirmation data itself is already safely recorded locally by
        /// the caller (ProcurementEngine.ProcessOrderConfirmationsAsync) before this runs, so a MAX
        /// write failure here shouldn't also lose that.
        ///
        /// The current-row reads below run as plain Dapper queries on the same `admin` connection
        /// MaxOrderModule itself uses — confirmed safe from the decompiled source: MaxOrderModule
        /// only opens its own transaction lazily, inside each top-level call (a nesting counter that
        /// begins the transaction on entry and commits when it returns to zero), not for the
        /// lifetime of the MaxOrderModule instance. As long as a read here never runs concurrently
        /// with an in-flight ChangePOHeading/ChangePODetail call on the same instance (it doesn't —
        /// everything below is sequential await, never parallel), there's no pending transaction on
        /// `admin` when the read executes.
        /// </summary>
        public async Task ApplyConfirmationAsync(PurchaseOrder order, SupplierOrderStatus supplierStatus)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            if (supplierStatus == null) throw new ArgumentNullException(nameof(supplierStatus));
            if (string.IsNullOrEmpty(order.ErpPoNumber)) return;

            using (var primary = new SqlConnection(_session.PrimaryConnectionString))
            using (var admin = new SqlConnection(_session.AdminConnectionString))
            {
                primary.Open();
                admin.Open();

                var log = MyLogManager.Create(_session.LogFile, _session.LogPath, true).GetCurrentClassLogger();

                using (var maxOrderModule = new MaxOrderModule(primary, admin, _session.CompanyId, log, _session.LicensePath, _session.UserName))
                {
                    await ApplyHeaderConfirmationAsync(admin, maxOrderModule, order, log);

                    foreach (var line in order.Lines)
                    {
                        // Placed while UseMockPurchaseOrders=true (or synced before this field
                        // existed) — no real MAX row to point at.
                        if (string.IsNullOrEmpty(line.MaxLineNumber)) continue;

                        var statusLine = supplierStatus.Lines?.FirstOrDefault(l => l.SupplierPartNumber == line.SupplierPartNumber);
                        await ApplyLineConfirmationAsync(admin, maxOrderModule, order, line, statusLine, log);
                    }
                }
            }
        }

        private static async Task ApplyHeaderConfirmationAsync(SqlConnection admin, MaxOrderModule maxOrderModule, PurchaseOrder order, NLog.Logger log)
        {
            if (string.IsNullOrEmpty(order.SupplierOrderNumber)) return;

            // QueryAsync + FirstOrDefault rather than QuerySingleOrDefaultAsync: that convenience
            // method doesn't exist in Dapper 1.40.0 (the version pinned to match UniPro2026's own
            // packages.config) — QueryAsync has been in Dapper since its earliest versions.
            var headerRows = await admin.QueryAsync<Purchase_Order_Code>(
                "SELECT * FROM Purchase_Order_Code WHERE ORDNUM_16 = @Ordnum", new { Ordnum = order.ErpPoNumber });
            var header = headerRows.FirstOrDefault();
            if (header == null)
            {
                log.Warn($"Kan Purchase_Order_Code niet vinden voor PO {order.ErpPoNumber} — CONFRM_16 niet bijgewerkt.");
                return;
            }

            header.CONFRM_16 = Truncate(order.SupplierOrderNumber, 15);

            var changed = maxOrderModule.ChangePOHeading(header, out var errMsg);
            if (!changed)
                log.Warn($"MAX ChangePOHeading (CONFRM_16) is mislukt voor PO {order.ErpPoNumber}: {errMsg}");
        }

        private static async Task ApplyLineConfirmationAsync(
            SqlConnection admin, MaxOrderModule maxOrderModule, PurchaseOrder order,
            PurchaseOrderLine line, SupplierOrderStatusLine statusLine, NLog.Logger log)
        {
            var currentRows = await admin.QueryAsync<Order_Master>(
                "SELECT * FROM Order_Master WHERE ORDNUM_10 = @Ordnum AND LINNUM_10 = @Linnum AND DELNUM_10 = @Delnum",
                new { Ordnum = order.ErpPoNumber, Linnum = line.MaxLineNumber, Delnum = line.MaxDeliveryNumber });
            var current = currentRows.FirstOrDefault();
            if (current == null)
            {
                log.Warn($"Kan Order_Master niet vinden voor {order.ErpPoNumber}-{line.MaxLineNumber}-{line.MaxDeliveryNumber} — regel niet bijgewerkt.");
                return;
            }

            var confirmedShipDate = statusLine?.EstimatedShipDate;
            var dateChanged = confirmedShipDate.HasValue && confirmedShipDate.Value.Date != current.CURDUE_10.Date;

            current.ORDREF_10 = BuildConfirmedReference(current.ORDREF_10, dateChanged);
            if (dateChanged)
                current.CURDUE_10 = confirmedShipDate.Value;

            var changed = maxOrderModule.ChangePODetail(current, IncOrdRev: true);
            if (!changed)
            {
                var errMsg = maxOrderModule.GetErrors();
                log.Warn($"MAX ChangePODetail is mislukt voor {order.ErpPoNumber}-{line.MaxLineNumber}-{line.MaxDeliveryNumber}: {errMsg}");
            }
        }

        /// <summary>
        /// ORDREF_10 is char(25). Confirmed convention (user, not guessed): "O " prefix when the
        /// order is confirmed as originally requested, "OP " when the confirmed leverdatum differs
        /// — goes in front of whatever reference text is already there, truncated from the end if it
        /// no longer fits. Idempotent: re-processing a confirmation (e.g. a later re-check that flips
        /// the date verdict) strips any existing "O "/"OP " prefix first instead of stacking a new
        /// one on top of the last run's marker.
        /// </summary>
        internal static string BuildConfirmedReference(string currentReference, bool dateChanged)
        {
            var existing = currentReference ?? "";
            if (existing.StartsWith("OP ", StringComparison.Ordinal)) existing = existing.Substring(3);
            else if (existing.StartsWith("O ", StringComparison.Ordinal)) existing = existing.Substring(2);

            var marker = dateChanged ? "OP " : "O ";
            var combined = marker + existing;
            return combined.Length > 25 ? combined.Substring(0, 25) : combined;
        }

        private static string Truncate(string value, int maxLength) =>
            string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value.Substring(0, maxLength);

        /// <summary>
        /// Field values map to already-known data where possible (PRTNUM_10 from the originating
        /// PurchaseRequestLine.ErpArticleId, CURQTY_10 from the draft line's ordered quantity,
        /// VENID_10 from the supplier); everything else is copied from the originally supplied
        /// example verbatim (STATUS_10 = "3" and STK_10 = "FGI" fallback are unconfirmed for a real
        /// PO line rather than that example's own test data). ORDNUM_10/LINNUM_10/DELNUM_10 are left
        /// blank for the first line of a PO (AddPODetail assigns "01"/"01" and the new PO number
        /// itself when CreateHeader:true) and set explicitly for every line after that.
        ///
        /// Internal + static (userName passed explicitly instead of reading _session.UserName) so
        /// MaxPurchaseOrderRepositoryTests can exercise this field-by-field mapping directly —
        /// without that, this repository's own live SqlConnection/SupplierRepository/
        /// PurchaseRequestRepository dependencies would make it untestable in isolation. This is
        /// exactly the class of bug two rebuild cycles already caught here (FORCUR_10's decimal-to-
        /// double cast, ORDER_10 left blank on non-first lines) — worth pinning down with a test.
        /// </summary>
        internal static Order_Master BuildPurchaseOrderLine(
            string vendorId, PurchaseRequestLine requestLine, PurchaseOrderDraftLine draftLine,
            string erpPoNumber, int lineNumber, bool isFirstLine, string userName)
        {
            var now = DateTime.Now;
            return new Order_Master
            {
                ORDNUM_10 = isFirstLine ? "" : erpPoNumber,
                LINNUM_10 = isFirstLine ? "" : $"{lineNumber:D2}",
                DELNUM_10 = isFirstLine ? "" : "01",
                // AddPODetail computes ORDER_10 = ORDNUM_10 + LINNUM_10 + DELNUM_10 itself when
                // CreateHeader:true (confirmed in the decompiled source, right after it mints the
                // new PO number) — but that assignment sits inside the CreateHeader-only branch, so
                // for every later line of the same PO (CreateHeader:false, ORDNUM_10/LINNUM_10/
                // DELNUM_10 already known here) it's built the same way ourselves rather than risk
                // an inserted row with this field left blank.
                ORDER_10 = isFirstLine ? "" : $"{erpPoNumber}{lineNumber:D2}01",
                PRTNUM_10 = requestLine.ErpArticleId,
                CURDUE_10 = requestLine.RequiredDate ?? now,
                RECFLG_10 = "N",
                TAXABLE_10 = "N",
                TYPE_10 = "PO",
                VENID_10 = vendorId,
                CURQTY_10 = draftLine.Quantity,
                FRMPLN_10 = "Y",
                STATUS_10 = "3",
                STK_10 = requestLine.StockId ?? "FGI",
                CUSORD_10 = "",
                PLANID_10 = "",
                ORDREF_10 = "",
                SCHFLG_10 = "",
                LOTNUM_10 = "",
                BEGSER_10 = "",
                REWORK_10 = "N",
                // FORCUR_10 is double (confirmed in the decompiled MaxOrderModule source: compared
                // directly against the literal 0.0, divided by/multiplied with other double values
                // with no cast anywhere) — draftLine.UnitPrice is decimal, which has no implicit
                // conversion to double, hence the explicit cast.
                FORCUR_10 = (double)draftLine.UnitPrice,
                CRTSNS_10 = "N",
                CREDTE_10 = now,
                UDFKEY_10 = " ",
                UDFREF_10 = " ",
                INSREQ_10 = "N",
                RTEDTE_10 = now,
                PLSTPRNT_10 = "N",
                ROUTPRNT_10 = "N",
                ALTBOM_10 = " ",
                ALTRTG_10 = " ",
                FILL03_10 = "",
                FILL04_10 = "",
                FILL05_10 = "",
                FILLER_10 = "",
                XDFINT_10 = 0,
                XDFFLT_10 = 0,
                XDFBOL_10 = "",
                XDFTXT_10 = "",
                CreatedBy = userName,
                ModifiedBy = userName,
                SUBSHP_10 = 0
            };
        }
    }
}
