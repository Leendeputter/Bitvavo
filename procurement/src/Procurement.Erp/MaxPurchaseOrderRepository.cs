using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using MAX50;
using MaxOrderNET;
using Procurement.Core.Entities;
using Procurement.Core.Interfaces;
using Procurement.Core.Models;
using Procurement.Data.Repositories;

namespace Procurement.Erp
{
    /// <summary>
    /// Creates actual MAX Purchase Orders via MaxOrderNET's MaxOrderModule (AddPOHeading/
    /// AddPODetail) — the write-side counterpart to MaxOrderRepository's read-side Order_Master/
    /// Part_Master queries. Uses MAX's own module rather than raw SQL, on purpose: that keeps
    /// MAX's own validations/business rules/triggers intact instead of bypassing them, the same
    /// way LoginForm already goes through MaxSQL rather than querying ExactRMCompanies by hand.
    ///
    /// STATUS: header creation (AddPOHeading) is implemented from a working VB.NET example
    /// supplied directly (a WinForms button handler that calls it with real, working parameter
    /// values) — reasonably confident. Line creation (AddPODetail) and the header/line linkage are
    /// still open questions (see the TODOs below and MaxErpConnector.UseMockPurchaseOrders' comment)
    /// — the supplied example creates a header and a line as two independent, unlinked demo
    /// snippets, so it doesn't show how a real caller ties a specific line to the PO header it just
    /// created. Flip MaxErpConnector.UseMockPurchaseOrders to false only once that's confirmed.
    ///
    /// Still open (bring these back from whoever manages MAX):
    ///  1. How does AddPODetail associate a line with a specific PO header? The example sets
    ///     OrderMaster.ORDNUM_10 = "" for the line, same as the header's ORDNUM_16 = "" — if MAX
    ///     auto-assigns both independently there must be some other linking step this snippet
    ///     doesn't show.
    ///  2. What do AddPODetail's trailing parameters mean (called as
    ///     AddPODetail(orderMaster, True, True, "F", 3) in the example)? Guessed here as
    ///     (checkAvailability, autoSchedule, schedulingMethod, leadTimeDays) purely by position —
    ///     unconfirmed.
    ///  3. Which of the header's fixed-looking values (TERMS_16, SHPVIA_16, COLPPD_16, GSHIP_16,
    ///     GTERM_16, CODE_16, ...) should actually come from Vendor_Master per supplier, vs. being
    ///     genuine company-wide constants? The supplied example hardcodes them (including
    ///     CODE_16 = "CAD", Canadian Dollar — corrected to "EUR" below, but that's a guess, not a
    ///     confirmed MAX currency code).
    ///  4. How is the original PurchaseRequest's MAX order (the row MaxOrderRepository read this
    ///     from) removed/closed once a PO exists for it, so it stops reappearing as an open
    ///     request on the next Query? Not implemented yet — see RemoveOriginalOrderAsync below.
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

        /// <summary>Returns MAX's own PO number (assigned by AddPOHeading, same as the supplied example's ORDNUM_16 = "" going in, sOrder coming out).</summary>
        public async Task<string> CreatePurchaseOrderAsync(PurchaseOrderDraft draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));

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
                var maxOrderModule = new MaxOrderModule(primary, admin, _session.CompanyId, log, _session.LicensePath, _session.UserName);

                var poHeader = BuildPurchaseOrderHeader(supplier.VendorId);
                var errorMessage = string.Empty;
                // The supplied VB example reads sErrMsg's content *after* this call, which only
                // makes sense if AddPOHeading's second parameter is declared ByRef — VB.NET compiles
                // ByRef to a C# `ref` parameter, so that's assumed here; if the real signature turns
                // out to be ByVal instead, this is a one-word compile fix (drop `ref`), not a
                // behavior risk.
                var erpPoNumber = maxOrderModule.AddPOHeading(poHeader, ref errorMessage);

                if (string.IsNullOrEmpty(erpPoNumber))
                    throw new InvalidOperationException($"MAX AddPOHeading is mislukt: {errorMessage}");

                // TODO (open question 1 above): nothing here ties erpPoNumber to the lines added
                // below yet — AddPODetail's example call doesn't show the linking mechanism.
                foreach (var draftLine in draft.Lines)
                {
                    var requestLine = await _purchaseRequestRepository.GetLineByIdAsync(draftLine.PurchaseRequestLineId);
                    if (requestLine == null)
                        throw new InvalidOperationException($"PurchaseRequestLine {draftLine.PurchaseRequestLineId} niet gevonden — kan geen MAX PO-regel aanmaken.");

                    var poLine = BuildPurchaseOrderLine(erpPoNumber, supplier.VendorId, requestLine, draftLine);

                    // TODO (open question 2 above): trailing parameters guessed by position from
                    // the supplied example (True, True, "F", 3) — unconfirmed meaning.
                    var added = maxOrderModule.AddPODetail(poLine, true, true, "F", 3);
                    if (!added)
                        throw new InvalidOperationException($"MAX AddPODetail is mislukt voor regel {draftLine.PurchaseRequestLineId} (PO {erpPoNumber}).");
                }

                return erpPoNumber;
            }
        }

        public Task UpdateStatusAsync(string erpPoNumber, string status)
        {
            // Not part of the supplied example — MaxOrderModule likely has its own method for
            // this (mirroring AddPOHeading/AddPODetail), not yet confirmed.
            throw new NotImplementedException(
                "MAX PO-statusupdate is nog niet geïmplementeerd — welke MaxOrderModule-methode hiervoor is, is nog niet bevestigd.");
        }

        /// <summary>
        /// Not implemented — MAX needs the original PurchaseRequest's order (the Order_Master row
        /// MaxOrderRepository read this from) removed or closed once a PO exists for it, otherwise
        /// it keeps reappearing as an open request on the next Query. No MaxOrderModule method for
        /// this has been confirmed yet — bring this back along with the other open questions.
        /// </summary>
        public Task RemoveOriginalOrderAsync(string erpRequestNumber)
        {
            throw new NotImplementedException(
                "Verwijderen/afsluiten van de oorspronkelijke MAX-order na PO-aanmaak is nog niet " +
                "geïmplementeerd — welke aanpak MAX hiervoor verwacht (een MaxOrderModule-methode, of " +
                "iets anders) is nog niet bevestigd.");
        }

        /// <summary>
        /// Field values copied from the supplied working example almost verbatim — only VENID_16
        /// (from our own Supplier.VendorId, was hardcoded "V400" in the example) and CODE_16
        /// (guessed "EUR", was hardcoded "CAD" — Canadian Dollar, clearly example/demo data) were
        /// changed. Everything else (TERMS_16, SHPVIA_16, COLPPD_16, GSHIP_16, GTERM_16, ...) is a
        /// fixed value straight from the example and is open question 3 above: several of these
        /// plausibly should come from Vendor_Master per supplier instead of being constant for
        /// every PO.
        /// </summary>
        private Purchase_Order_Code BuildPurchaseOrderHeader(string vendorId)
        {
            var now = DateTime.Now;
            return new Purchase_Order_Code
            {
                ORDNUM_16 = "",
                VENID_16 = vendorId,
                TERMS_16 = "Nett 30",
                TAXBL_16 = "N",
                SHPVIA_16 = "UPS Ground",
                COLPPD_16 = "P",
                ORDDTE_16 = now,
                FILL01_16 = "",
                ORDREV_16 = "000",
                PRTFLG_16 = "N",
                XODE_16 = "PO",
                GSHIP_16 = "12",
                GTERM_16 = "02",
                FIXVAR_16 = "F",
                CODE_16 = "EUR",
                TAXABL_16 = "N",
                CREDTE_16 = now,
                LNETAX_16 = "N",
                EXCESS_16 = 0,
                XDFINT_16 = 0,
                XDFFLT_16 = 0,
                XDFBOL_16 = "",
                XDFTXT_16 = "",
                FILLER_16 = "",
                CreatedBy = _session.UserName,
                ModifiedBy = _session.UserName
            };
        }

        /// <summary>
        /// Field values map to already-known data where possible (PRTNUM_10 from the originating
        /// PurchaseRequestLine.ErpArticleId, CURQTY_10 from the draft line's ordered quantity,
        /// VENID_10 from the supplier); everything else is copied from the supplied example
        /// verbatim (open question 3 applies here too, e.g. STATUS_10 = "3" and STK_10 = "FGI" are
        /// unconfirmed for a real PO line rather than the example's own test data).
        /// </summary>
        private Order_Master BuildPurchaseOrderLine(string erpPoNumber, string vendorId, PurchaseRequestLine requestLine, PurchaseOrderDraftLine draftLine)
        {
            var now = DateTime.Now;
            return new Order_Master
            {
                // TODO (open question 1): the supplied example leaves ORDNUM_10 blank here too —
                // if that's not how a line gets attached to erpPoNumber, this needs to change.
                ORDNUM_10 = "",
                LINNUM_10 = "",
                DELNUM_10 = "",
                ORDER_10 = "",
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
                // FORCUR_10's numeric type is unconfirmed (example: FORCUR_10 = 127.345, a VB
                // literal that could bind to Double/Single/Decimal) — draftLine.UnitPrice is
                // decimal; if this doesn't compile as-is, wrap it in the matching numeric cast.
                FORCUR_10 = draftLine.UnitPrice,
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
