using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
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
    ///  3. No status-update method has been found yet — UpdateStatusAsync stays unimplemented.
    ///     ChangePOHeading(Purchase_Order_Code, out errMsg)/ChangePODetail(Order_Master, bool) exist
    ///     and look plausible for this, but aren't confirmed.
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

        /// <summary>Returns MAX's own PO number, minted by AddPODetail on the first line (CreateHeader:true).</summary>
        public async Task<string> CreatePurchaseOrderAsync(PurchaseOrderDraft draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (draft.Lines == null || draft.Lines.Count == 0)
                throw new ArgumentException("PurchaseOrderDraft has no lines.", nameof(draft));

            var supplier = await _supplierRepository.GetByCodeAsync(draft.SupplierCode);
            if (supplier == null || string.IsNullOrEmpty(supplier.VendorId))
                throw new InvalidOperationException(
                    $"Supplier '{draft.SupplierCode}' heeft geen VendorId (MAX Part_Vendor.VENID_07) ingesteld — nodig om een PO in MAX aan te maken. Stel dit in via Instellingen -> Suppliers.");

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
                        var poLine = BuildPurchaseOrderLine(supplier.VendorId, requestLine, draftLine, erpPoNumber, lineNumber, isFirstLine);

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

                        RemoveOriginalOrder(maxOrderModule, log, requestLine);
                    }

                    return erpPoNumber;
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

        public Task UpdateStatusAsync(string erpPoNumber, string status)
        {
            // No MaxOrderModule method for this has been identified yet in the source supplied so far.
            throw new NotImplementedException(
                "MAX PO-statusupdate is nog niet geïmplementeerd — welke MaxOrderModule-methode hiervoor is, is nog niet bevestigd.");
        }

        /// <summary>
        /// Field values map to already-known data where possible (PRTNUM_10 from the originating
        /// PurchaseRequestLine.ErpArticleId, CURQTY_10 from the draft line's ordered quantity,
        /// VENID_10 from the supplier); everything else is copied from the originally supplied
        /// example verbatim (STATUS_10 = "3" and STK_10 = "FGI" fallback are unconfirmed for a real
        /// PO line rather than that example's own test data). ORDNUM_10/LINNUM_10/DELNUM_10 are left
        /// blank for the first line of a PO (AddPODetail assigns "01"/"01" and the new PO number
        /// itself when CreateHeader:true) and set explicitly for every line after that.
        /// </summary>
        private Order_Master BuildPurchaseOrderLine(
            string vendorId, PurchaseRequestLine requestLine, PurchaseOrderDraftLine draftLine,
            string erpPoNumber, int lineNumber, bool isFirstLine)
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
                CreatedBy = _session.UserName,
                ModifiedBy = _session.UserName,
                SUBSHP_10 = 0
            };
        }
    }
}
