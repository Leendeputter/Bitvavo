using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Entities;
using Procurement.Core.Models;
using Procurement.Data.Repositories;
using Procurement.Erp;
using static Procurement.UI.Support.GridFormatting;

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
    /// </summary>
    public class OrdersForm : Form
    {
        private readonly PurchaseOrderRepository _purchaseOrderRepository;
        private readonly PurchaseRequestRepository _purchaseRequestRepository;
        private readonly MaxOrderRepository _maxOrderRepository;

        private DataGridView _ordersGrid;
        private DataGridView _linesGrid;
        private CheckBox _includeCompletedCheckBox;
        private Label _statusBar;
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
            Load += async (s, e) => await RefreshAsync();
        }

        private void InitializeComponent()
        {
            Text = "Orders";
            Width = 1150;
            Height = 650;
            // StartPosition is moot for an MDI child (ShellForm.ShowOrders() sets MdiParent, user
            // request, 20 okt 2026 — "MDI-container?") — WinForms always places MDI children with its
            // own cascading layout regardless of this setting; left as CenterScreen only because
            // nothing depends on changing it.
            StartPosition = FormStartPosition.CenterScreen;
            // Pale blue, matching MAX's own "MRP Detail" screenshot (user request, 20 okt 2026) — see
            // GridFormatting.ClassicScreenBackColor's own comment for why this replaced an earlier,
            // visually-no-op SystemColors.Control attempt.
            BackColor = ClassicScreenBackColor;

            // Taller than a plain button bar (user request, 20 okt 2026 — matching MAX's own "PO
            // Schedule Inquiry" screenshot) to fit the "Include" group below.
            var topPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 70, FlowDirection = FlowDirection.LeftToRight };
            var refreshButton = new Button { Text = "Refresh", AutoSize = true, Margin = new Padding(4) };
            ApplyClassicButtonStyle(refreshButton);
            refreshButton.Click += async (s, e) => await RefreshAsync();
            topPanel.Controls.Add(refreshButton);

            // "Orderlijnen die al geleverd zijn" default-hidden in Lines of Selected PO (user request,
            // 20 okt 2026, matching MAX's own "PO Schedule Inquiry 1" filter screen — its "Status 4
            // Orders (Completed)" checkbox, same Order_Master.STATUS_10 field). Unchecked by default:
            // only still-open lines show; checking it also shows lines MAX's own STATUS_10 marks
            // Completed ("4"). Re-filters instantly from the cached _lineStatusesByKey (set once per
            // RefreshAsync) rather than re-querying MAX on every toggle. Label text matches MAX's own
            // screenshot verbatim rather than this app's own wording (user request, 20 okt 2026 — "de
            // tekst die ik je in het voorbeeld gestuurd had, dat verbeterd de adaptie wellicht").
            var includeLayout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
            _includeCompletedCheckBox = new CheckBox { Text = "Status 4 Orders (Completed)", AutoSize = true };
            _includeCompletedCheckBox.CheckedChanged += async (s, e) => await LoadLinesForSelectedOrderAsync();
            includeLayout.Controls.Add(_includeCompletedCheckBox);
            var includeGroup = WrapInGroupBox("Include", includeLayout);
            includeGroup.Margin = new Padding(12, 2, 4, 2);
            topPanel.Controls.Add(includeGroup);

            var poLabel = new Label { Text = "All Purchase Orders:", Dock = DockStyle.Top, Height = 20, Padding = new Padding(4) };
            _ordersGrid = new DataGridView
            {
                Dock = DockStyle.Top,
                Height = 260,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.RowHeaderSelect,
                MultiSelect = false
            };
            EnableDoubleBuffering(_ordersGrid);
            EnableRowNumberHeaders(_ordersGrid);
            ApplyClassicHeaderStyle(_ordersGrid);
            _ordersGrid.SelectionChanged += OrdersGrid_SelectionChanged;

            var linesLabel = new Label { Text = "Lines of Selected PO:", Dock = DockStyle.Top, Height = 20, Padding = new Padding(4) };
            _linesGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.RowHeaderSelect,
                MultiSelect = false
            };
            EnableDoubleBuffering(_linesGrid);
            EnableRowNumberHeaders(_linesGrid);
            ApplyClassicHeaderStyle(_linesGrid);

            _statusBar = new Label { Dock = DockStyle.Bottom, Height = 24, Padding = new Padding(4), BackColor = System.Drawing.SystemColors.ControlLight };

            Controls.Add(_linesGrid);
            Controls.Add(linesLabel);
            Controls.Add(_ordersGrid);
            Controls.Add(poLabel);
            Controls.Add(topPanel);
            Controls.Add(_statusBar);
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            _statusBar.Text = "Orders laden...";
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

            // Assigning DataSource can itself raise SelectionChanged; detach first so that doesn't
            // race with LoadLinesForSelectedOrder below against the same in-memory list (same
            // reasoning as MainForm's own grids).
            _ordersGrid.SelectionChanged -= OrdersGrid_SelectionChanged;
            _ordersGrid.DataSource = _loadedOrders.Select(po => new
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
            _ordersGrid.SelectionChanged += OrdersGrid_SelectionChanged;

            if (_ordersGrid.Columns["Id"] != null)
                _ordersGrid.Columns["Id"].Visible = false;
            ApplyCurrencyColumns(_ordersGrid, "OrderTotal");
            ApplyDateTimeColumns(_ordersGrid, "CreatedAt", "SubmittedAt", "ConfirmedAt");
            // Explicit widths for every column (user request, 20 okt 2026) — none of these are
            // genuinely free-text, so none need Fill's stretch-to-fill behavior.
            SetCharacterBasedColumnWidth(_ordersGrid, "ErpPoNumber", 12);
            SetCharacterBasedColumnWidth(_ordersGrid, "Supplier", 12);
            SetCharacterBasedColumnWidth(_ordersGrid, "SupplierOrderNumber", 16);
            SetCharacterBasedColumnWidth(_ordersGrid, "Status", 14);
            SetCharacterBasedColumnWidth(_ordersGrid, "CreatedAt", 19);
            SetCharacterBasedColumnWidth(_ordersGrid, "SubmittedAt", 19);
            SetCharacterBasedColumnWidth(_ordersGrid, "ConfirmedAt", 19);
            SetCharacterBasedColumnWidth(_ordersGrid, "OrderTotal", 14);
            SetCharacterBasedColumnWidth(_ordersGrid, "Currency", 8);
            SetCharacterBasedColumnWidth(_ordersGrid, "Lines", 8);

            _statusBar.Text = $"{_loadedOrders.Count} order(s).";
            await LoadLinesForSelectedOrderAsync();
        }

        private async void OrdersGrid_SelectionChanged(object sender, EventArgs e) => await LoadLinesForSelectedOrderAsync();

        private async System.Threading.Tasks.Task LoadLinesForSelectedOrderAsync()
        {
            if (_ordersGrid.CurrentRow == null)
            {
                _linesGrid.DataSource = null;
                return;
            }

            var poId = (int)_ordersGrid.CurrentRow.Cells["Id"].Value;
            var order = _loadedOrders.FirstOrDefault(o => o.Id == poId);
            if (order == null)
            {
                _linesGrid.DataSource = null;
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
                .Where(l => _includeCompletedCheckBox.Checked || !IsCompletedInMax(order.ErpPoNumber, l))
                .ToList();
            _linesGrid.DataSource = visibleLines.Select(l =>
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
            ApplyQuantityColumns(_linesGrid, "Quantity", "ConfirmedQuantity");
            ApplyCurrencyColumns(_linesGrid, "UnitPrice", "ConfirmedUnitPrice", "LineTotal");
            ApplyDateColumns(_linesGrid, "RequiredDate", "ConfirmedShipDate");
            SetHeaderText(_linesGrid, "MaxStatus", "Status");
            SetHeaderText(_linesGrid, "QuantityMismatch", "Qty Mismatch?");
            SetHeaderText(_linesGrid, "PriceMismatch", "Price Mismatch?");
            SetHeaderText(_linesGrid, "DeliveryIsLate", "Late?");
            HighlightDeviatingLines(_linesGrid);
            // Explicit widths for every column except SupplierPartNumber (genuinely variable-length,
            // left in Fill with a floor) — same reasoning as MainForm's offers grid (user request,
            // 20 okt 2026).
            SetCharacterBasedColumnWidth(_linesGrid, "ErpRequestNumber", 12);
            SetCharacterBasedColumnWidth(_linesGrid, "PartID", 14);
            SetMinimumWidth(_linesGrid, "SupplierPartNumber", 140);
            SetCharacterBasedColumnWidth(_linesGrid, "MaxStatus", 10);
            SetCharacterBasedColumnWidth(_linesGrid, "Confirmed", 10);
            SetCharacterBasedColumnWidth(_linesGrid, "Quantity", 10);
            SetCharacterBasedColumnWidth(_linesGrid, "ConfirmedQuantity", 12);
            SetCharacterBasedColumnWidth(_linesGrid, "QuantityMismatch", 12);
            SetCharacterBasedColumnWidth(_linesGrid, "UnitPrice", 12);
            SetCharacterBasedColumnWidth(_linesGrid, "ConfirmedUnitPrice", 14);
            SetCharacterBasedColumnWidth(_linesGrid, "PriceMismatch", 12);
            SetCharacterBasedColumnWidth(_linesGrid, "RequiredDate", 12);
            SetCharacterBasedColumnWidth(_linesGrid, "ConfirmedShipDate", 14);
            SetCharacterBasedColumnWidth(_linesGrid, "DeliveryIsLate", 8);
            SetCharacterBasedColumnWidth(_linesGrid, "LineTotal", 14);

            var unconfirmedCount = visibleLines.Count(l => !l.ConfirmedQuantity.HasValue);
            var hiddenCompletedCount = order.Lines.Count - visibleLines.Count;
            _statusBar.Text = $"{order.SupplierCode}: {order.Status}"
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
