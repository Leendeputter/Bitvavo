using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Entities;
using Procurement.Core.Models;
using Procurement.Data.Repositories;
using Procurement.Erp;
using static Procurement.UI.Support.GridFormatting;
using Procurement.UI.Support;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Global purchase-order register — every PO for this MAX company, independent of any one
    /// purchase request. Used to complement OrderDetailForm (which only showed the PO(s) touching a
    /// single selected request) rather than replace it — that form is gone now (20 okt 2026): it only
    /// ever worked from a requests-grid row, and an Ordered row can no longer appear in that grid at
    /// all, so it had become unreachable. This screen is the only way to look at placed orders now.
    /// A PO can bundle lines from several requests (grouped by supplier, not by request — see
    /// PurchaseOrder.cs), and a PO's own lifecycle
    /// (Created/Submitted/Acknowledged/Confirmed/.../Shipped/Completed) is a different axis from a
    /// PurchaseRequest's Workflow status, so cramming both into one PR-shaped grid never worked well
    /// (user request, sep 2026, after repeatedly having to open Order details per row just to see an
    /// order's own status/confirmed quantity/price).
    /// Built with the Visual Studio Designer (user request, 16 nov 2026); the two grids sit in a
    /// SplitContainer so their heights can be dragged.
    /// </summary>
    public partial class OrdersForm : Form
    {
        private readonly PurchaseOrderRepository _purchaseOrderRepository;
        private readonly PurchaseRequestRepository _purchaseRequestRepository;
        private readonly MaxOrderRepository _maxOrderRepository;

        private IReadOnlyList<PurchaseOrder> _loadedOrders = new List<PurchaseOrder>();

        /// <summary>
        /// MAX Order_Master.STATUS_10 per PO line (ErpPoNumber+MaxLineNumber+MaxDeliveryNumber),
        /// refreshed once per RefreshAsync — backs the "Completed Orders" filter below without
        /// re-querying MAX on every checkbox toggle/order selection.
        /// </summary>
        private IReadOnlyDictionary<(string OrderNumber, string LineNumber, string DeliveryNumber), string> _lineStatusesByKey =
            new Dictionary<(string, string, string), string>();

        public OrdersForm(PurchaseOrderRepository purchaseOrderRepository, PurchaseRequestRepository purchaseRequestRepository, MaxOrderRepository maxOrderRepository)
        {
            _purchaseOrderRepository = purchaseOrderRepository ?? throw new ArgumentNullException(nameof(purchaseOrderRepository));
            _purchaseRequestRepository = purchaseRequestRepository ?? throw new ArgumentNullException(nameof(purchaseRequestRepository));
            _maxOrderRepository = maxOrderRepository ?? throw new ArgumentNullException(nameof(maxOrderRepository));
            InitializeComponent();
            ClassicStyle.ApplyClassicStyle(this);
            this.BackColor = ClassicStyle.ClassicScreenBackColor;
            EnableRowNumberHeaders(dgv_orders);
            EnableRowNumberHeaders(dgv_lines);
            GridColumnSettingsHelper.Attach(this); // remembers column widths per user, like UniPro
            Load += OrdersForm_Load;
        }

        private async void OrdersForm_Load(object sender, EventArgs e) => await RefreshAsync();

        private async void bt_refresh_Click(object sender, EventArgs e) => await RefreshAsync();

        // "Status 4 Orders (Completed)": label text matches MAX's own "PO Schedule Inquiry 1" filter
        // screen (user request, 20 okt 2026). Unchecked: only still-open lines; re-filters from the
        // cached _lineStatusesByKey without re-querying MAX.
        private async void chk_includeCompleted_CheckedChanged(object sender, EventArgs e) => await LoadLinesForSelectedOrderAsync();

        // Orders deleted straight in MAX are hidden by default but never deleted locally (user
        // feedback, 20 okt 2026); this checkbox reveals them again.
        private void chk_includeDeleted_CheckedChanged(object sender, EventArgs e) => PopulateOrdersGrid();

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            lbl_status.Text = "Orders laden...";
            _loadedOrders = await _purchaseOrderRepository.GetAllAsync();

            // One batched MAX round-trip for every line's Order_Master.STATUS_10 across every loaded
            // order — backs the "Completed Orders" filter in LoadLinesForSelectedOrderAsync. Lines
            // placed while UseMockPurchaseOrders=true (or synced before MaxLineNumber existed) have no
            // real MAX row to look up and are simply absent from the key set (treated as "still open"
            // by the filter below, the same fail-open default used elsewhere).
            var lineKeys = _loadedOrders
                .SelectMany(po => po.Lines.Select(l => (po.ErpPoNumber, l.MaxLineNumber, l.MaxDeliveryNumber)))
                .ToList();
            _lineStatusesByKey = await _maxOrderRepository.GetLineStatusesAsync(lineKeys);

            PopulateOrdersGrid();
            await LoadLinesForSelectedOrderAsync();
        }

        /// <summary>
        /// Rebuilds dgv_orders's rows from the already-loaded/already-fetched _loadedOrders and
        /// _lineStatusesByKey — called from RefreshAsync, and again from chk_includeDeleted's
        /// CheckedChanged so toggling it re-filters instantly without a MAX round-trip.
        /// </summary>
        private void PopulateOrdersGrid()
        {
            var visibleOrders = _loadedOrders
                .Where(po => chk_includeDeleted.Checked || !IsNotFoundInMax(po))
                .ToList();

            // Assigning DataSource can itself raise SelectionChanged; detach first so that doesn't
            // race with LoadLinesForSelectedOrder below against the same in-memory list (same
            // reasoning as MainForm's own grids).
            dgv_orders.SelectionChanged -= dgv_orders_SelectionChanged;
            dgv_orders.DataSource = visibleOrders.Select(po => new
            {
                po.Id,
                po.ErpPoNumber,
                Supplier = po.SupplierCode,
                po.SupplierOrderNumber,
                Status = po.Status.ToString(),
                po.CreatedAt,
                po.SubmittedAt,
                po.ConfirmedAt,
                po.OrderTotal,
                po.Currency,
                Lines = po.Lines.Count
            }).ToList();
            dgv_orders.SelectionChanged += dgv_orders_SelectionChanged;

            if (dgv_orders.Columns["Id"] != null)
                dgv_orders.Columns["Id"].Visible = false;
            ApplyCurrencyColumns(dgv_orders, "OrderTotal");
            ApplyDateTimeColumns(dgv_orders, "CreatedAt", "SubmittedAt", "ConfirmedAt");
            // Explicit widths for every column (user request, 20 okt 2026) — none of these are
            // genuinely free-text, so none need Fill's stretch-to-fill behavior.
            SetCharacterBasedColumnWidth(dgv_orders, "ErpPoNumber", 12);
            SetCharacterBasedColumnWidth(dgv_orders, "Supplier", 12);
            SetCharacterBasedColumnWidth(dgv_orders, "SupplierOrderNumber", 16);
            SetCharacterBasedColumnWidth(dgv_orders, "Status", 14);
            SetCharacterBasedColumnWidth(dgv_orders, "CreatedAt", 19);
            SetCharacterBasedColumnWidth(dgv_orders, "SubmittedAt", 19);
            SetCharacterBasedColumnWidth(dgv_orders, "ConfirmedAt", 19);
            SetCharacterBasedColumnWidth(dgv_orders, "OrderTotal", 14);
            SetCharacterBasedColumnWidth(dgv_orders, "Currency", 8);
            SetCharacterBasedColumnWidth(dgv_orders, "Lines", 8);

            var hiddenDeletedCount = _loadedOrders.Count - visibleOrders.Count;
            lbl_status.Text = $"{visibleOrders.Count} order(s)"
                + (hiddenDeletedCount > 0 ? $", {hiddenDeletedCount} verwijderd in MAX verborgen." : ".");
        }

        private async void dgv_orders_SelectionChanged(object sender, EventArgs e) => await LoadLinesForSelectedOrderAsync();

        private async System.Threading.Tasks.Task LoadLinesForSelectedOrderAsync()
        {
            if (dgv_orders.CurrentRow == null)
            {
                dgv_lines.DataSource = null;
                return;
            }

            var poId = (int)dgv_orders.CurrentRow.Cells["Id"].Value;
            var order = _loadedOrders.FirstOrDefault(o => o.Id == poId);
            if (order == null)
            {
                dgv_lines.DataSource = null;
                return;
            }

            // Traces each PO line back to the request/article it originated from — not shown
            // anywhere else, and exactly the kind of thing this screen exists for now that it's the
            // only way to look at a placed order at all (see this file's own class comment). One
            // lookup per line is fine at prototype scale (same reasoning as
            // MainForm.GetSelectionsByLineIdAsync).
            var requestLines = new Dictionary<int, PurchaseRequestLine>();
            foreach (var line in order.Lines)
            {
                if (requestLines.ContainsKey(line.PurchaseRequestLineId)) continue;
                var requestLine = await _purchaseRequestRepository.GetLineByIdAsync(line.PurchaseRequestLineId);
                if (requestLine != null) requestLines[line.PurchaseRequestLineId] = requestLine;
            }

            // ConfirmedQuantity is only ever set once a supplier order-status response actually
            // mentioned this SupplierPartNumber (PurchaseOrderRepository.ApplyOrderConfirmationAsync)
            // — so its mere presence *is* "is deze regel al bevestigd door de leverancier", not just
            // a number to compare. Reuses OrderConfirmationException's own (already tested)
            // QuantityMismatch/PriceMismatch/DeliveryIsLate rather than re-deriving the same
            // deviation logic here — same reasoning MainForm.OrderConfirmationExceptionsForm already
            // follows for "Orderbevestiging verwerken".
            var visibleLines = order.Lines
                .Where(l => chk_includeCompleted.Checked || !IsCompletedInMax(order.ErpPoNumber, l))
                .ToList();
            dgv_lines.DataSource = visibleLines.Select(l =>
            {
                requestLines.TryGetValue(l.PurchaseRequestLineId, out var requestLine);
                var confirmed = l.ConfirmedQuantity.HasValue;
                var confirmedShipDate = l.Deliveries.FirstOrDefault()?.EstimatedShipDate;
                var exception = new OrderConfirmationException
                {
                    OrderedQuantity = l.Quantity,
                    ConfirmedQuantity = l.ConfirmedQuantity,
                    OrderedUnitPrice = l.UnitPrice,
                    ConfirmedUnitPrice = l.ConfirmedUnitPrice,
                    RequiredDate = requestLine?.RequiredDate,
                    ConfirmedShipDate = confirmedShipDate
                };

                return new
                {
                    ErpRequestNumber = requestLine?.PurchaseRequest?.ErpRequestNumber,
                    PartID = requestLine?.ErpArticleId,
                    l.SupplierPartNumber,
                    // Raw Order_Master.STATUS_10 for this line (user request, 20 okt 2026 — there is
                    // no order-level status in MAX, only this per-line one). Shown as-is (the raw
                    // char(1) digit), not translated to a word — MAX itself shows it this way too
                    // (the "PO Schedule Inquiry 1" screenshot's "Status 4/5/6" wording).
                    MaxStatus = GetMaxStatus(order.ErpPoNumber, l),
                    Confirmed = confirmed,
                    l.Quantity,
                    l.ConfirmedQuantity,
                    // Blank (null, three-state checkbox) rather than false as long as the line isn't
                    // confirmed yet — a false checkbox would misleadingly read as "confirmed, no
                    // deviation" instead of "nothing to compare yet".
                    QuantityMismatch = confirmed ? (bool?)exception.QuantityMismatch : null,
                    l.UnitPrice,
                    l.ConfirmedUnitPrice,
                    PriceMismatch = confirmed ? (bool?)exception.PriceMismatch : null,
                    RequiredDate = requestLine?.RequiredDate,
                    ConfirmedShipDate = confirmedShipDate,
                    DeliveryIsLate = confirmed ? (bool?)exception.DeliveryIsLate : null,
                    l.LineTotal
                };
            }).ToList();
            ApplyQuantityColumns(dgv_lines, "Quantity", "ConfirmedQuantity");
            ApplyCurrencyColumns(dgv_lines, "UnitPrice", "ConfirmedUnitPrice", "LineTotal");
            ApplyDateColumns(dgv_lines, "RequiredDate", "ConfirmedShipDate");
            SetHeaderText(dgv_lines, "MaxStatus", "Status");
            SetHeaderText(dgv_lines, "QuantityMismatch", "Qty Mismatch?");
            SetHeaderText(dgv_lines, "PriceMismatch", "Price Mismatch?");
            SetHeaderText(dgv_lines, "DeliveryIsLate", "Late?");
            HighlightDeviatingLines(dgv_lines);
            // Explicit widths for every column, no Fill (user rule, 16 nov 2026: columns must not
            // stretch when the window is maximized).
            SetCharacterBasedColumnWidth(dgv_lines, "ErpRequestNumber", 12);
            SetCharacterBasedColumnWidth(dgv_lines, "PartID", 14);
            SetCharacterBasedColumnWidth(dgv_lines, "SupplierPartNumber", 20);
            SetCharacterBasedColumnWidth(dgv_lines, "MaxStatus", 10);
            SetCharacterBasedColumnWidth(dgv_lines, "Confirmed", 10);
            SetCharacterBasedColumnWidth(dgv_lines, "Quantity", 10);
            SetCharacterBasedColumnWidth(dgv_lines, "ConfirmedQuantity", 12);
            SetCharacterBasedColumnWidth(dgv_lines, "QuantityMismatch", 12);
            SetCharacterBasedColumnWidth(dgv_lines, "UnitPrice", 12);
            SetCharacterBasedColumnWidth(dgv_lines, "ConfirmedUnitPrice", 14);
            SetCharacterBasedColumnWidth(dgv_lines, "PriceMismatch", 12);
            SetCharacterBasedColumnWidth(dgv_lines, "RequiredDate", 12);
            SetCharacterBasedColumnWidth(dgv_lines, "ConfirmedShipDate", 14);
            SetCharacterBasedColumnWidth(dgv_lines, "DeliveryIsLate", 8);
            SetCharacterBasedColumnWidth(dgv_lines, "LineTotal", 14);

            var unconfirmedCount = visibleLines.Count(l => !l.ConfirmedQuantity.HasValue);
            var hiddenCompletedCount = order.Lines.Count - visibleLines.Count;
            lbl_status.Text = $"{order.SupplierCode}: {order.Status}"
                + (string.IsNullOrEmpty(order.SupplierOrderNumber) ? string.Empty : $" (ordernummer {order.SupplierOrderNumber})")
                + $" — {visibleLines.Count} regel(s), {unconfirmedCount} nog niet bevestigd"
                + (hiddenCompletedCount > 0 ? $", {hiddenCompletedCount} voltooid verborgen." : ".");
        }

        /// <summary>
        /// Raw Order_Master.STATUS_10 for this line, or null if no MAX row was found (placed while
        /// UseMockPurchaseOrders=true, or no MAX link yet). There is no order-header-level status in
        /// MAX itself (user, 20 okt 2026: "De status in de Order Master heeft betrekking op de
        /// lijnen. Er is geen status beschikbaar voor de hele inkooporder") — only this per-line one.
        /// </summary>
        private string GetMaxStatus(string erpPoNumber, PurchaseOrderLine line) =>
            _lineStatusesByKey.TryGetValue((erpPoNumber, line.MaxLineNumber, line.MaxDeliveryNumber), out var status)
                ? status
                : null;

        /// <summary>
        /// "4" is MAX's own Order_Master.STATUS_10 value for Completed (per the user-supplied "PO
        /// Schedule Inquiry 1" screenshot's "Status 4 Orders (Completed)" checkbox) — not guessed, the
        /// same convention this app already follows for "1"/"2" (Planned/Approved) elsewhere. A line
        /// missing from _lineStatusesByKey (no MAX row found, or placed while
        /// UseMockPurchaseOrders=true) is treated as still open rather than hidden — a lookup gap
        /// should never silently hide a line the user might actually need to act on.
        /// </summary>
        private bool IsCompletedInMax(string erpPoNumber, PurchaseOrderLine line) =>
            GetMaxStatus(erpPoNumber, line) == "4";

        /// <summary>
        /// True once every MAX-linked line of this order has no Order_Master row left at all (user
        /// request, 20 okt 2026: orders deleted straight in MAX should disappear from the default
        /// view, without this app ever deleting its own local PurchaseOrder record). Only considers
        /// lines that ever had a real MAX link (MaxLineNumber set) — an order placed entirely while
        /// UseMockPurchaseOrders=true never had one to begin with, so it's never "deleted", just
        /// always mock. An order with a mix of found/not-found lines (a partial MAX-side deletion)
        /// is deliberately NOT treated as deleted here — that's a different, more surprising case
        /// than this checkbox is meant to cover, and staying visible is the safer default for it.
        /// </summary>
        private bool IsNotFoundInMax(PurchaseOrder order)
        {
            var maxLinkedLines = order.Lines.Where(l => !string.IsNullOrEmpty(l.MaxLineNumber)).ToList();
            return maxLinkedLines.Count > 0 && maxLinkedLines.All(l => GetMaxStatus(order.ErpPoNumber, l) == null);
        }

        // Warning-orange for a line whose confirmation deviates in some way (quantity/prijs/te laat)
        // — mirrors OrderConfirmationExceptionsForm's own "only exceptions need a human look"
        // philosophy, but here so you can see it while browsing every order, not only right after
        // "Orderbevestiging verwerken" ran.
        private static readonly System.Drawing.Color DeviationRowColor = System.Drawing.Color.FromArgb(255, 235, 205);

        private static void HighlightDeviatingLines(DataGridView grid)
        {
            if (grid.Columns["QuantityMismatch"] == null) return;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                var deviates = (row.Cells["QuantityMismatch"].Value as bool?) == true
                    || (row.Cells["PriceMismatch"].Value as bool?) == true
                    || (row.Cells["DeliveryIsLate"].Value as bool?) == true;
                if (deviates) row.DefaultCellStyle.BackColor = DeviationRowColor;
            }
        }
    }
}
