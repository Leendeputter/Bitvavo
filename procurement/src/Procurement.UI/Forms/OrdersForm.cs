using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Entities;
using Procurement.Core.Models;
using Procurement.Data.Repositories;
using static Procurement.UI.Support.GridFormatting;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Global purchase-order register — every PO for this MAX company, independent of any one
    /// purchase request. Complements OrderDetailForm (which only shows the PO(s) touching a single
    /// selected aanvraag) rather than replacing it: a PO can bundle lines from several requests
    /// (grouped by supplier, not by request — see PurchaseOrder.cs), and a PO's own lifecycle
    /// (Created/Submitted/Acknowledged/Confirmed/.../Shipped/Completed) is a different axis from a
    /// PurchaseRequest's Workflow status, so cramming both into one PR-shaped grid never worked well
    /// (user request, sep 2026, after repeatedly having to open Order details per row just to see an
    /// order's own status/confirmed quantity/price).
    /// </summary>
    public class OrdersForm : Form
    {
        private readonly PurchaseOrderRepository _purchaseOrderRepository;
        private readonly PurchaseRequestRepository _purchaseRequestRepository;

        private DataGridView _ordersGrid;
        private DataGridView _linesGrid;
        private Label _statusBar;
        private IReadOnlyList<PurchaseOrder> _loadedOrders = new List<PurchaseOrder>();

        public OrdersForm(PurchaseOrderRepository purchaseOrderRepository, PurchaseRequestRepository purchaseRequestRepository)
        {
            _purchaseOrderRepository = purchaseOrderRepository ?? throw new ArgumentNullException(nameof(purchaseOrderRepository));
            _purchaseRequestRepository = purchaseRequestRepository ?? throw new ArgumentNullException(nameof(purchaseRequestRepository));
            InitializeComponent();
            Load += async (s, e) => await RefreshAsync();
        }

        private void InitializeComponent()
        {
            Text = "Orders";
            Width = 1150;
            Height = 650;
            StartPosition = FormStartPosition.CenterParent;

            var topPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, FlowDirection = FlowDirection.LeftToRight };
            var refreshButton = new Button { Text = "Verversen", AutoSize = true, Margin = new Padding(4) };
            refreshButton.Click += async (s, e) => await RefreshAsync();
            topPanel.Controls.Add(refreshButton);

            var poLabel = new Label { Text = "Alle purchase orders:", Dock = DockStyle.Top, Height = 20, Padding = new Padding(4) };
            _ordersGrid = new DataGridView
            {
                Dock = DockStyle.Top,
                Height = 260,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            EnableDoubleBuffering(_ordersGrid);
            _ordersGrid.SelectionChanged += OrdersGrid_SelectionChanged;

            var linesLabel = new Label { Text = "Regels van geselecteerde PO:", Dock = DockStyle.Top, Height = 20, Padding = new Padding(4) };
            _linesGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            EnableDoubleBuffering(_linesGrid);

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

            // Assigning DataSource can itself raise SelectionChanged; detach first so that doesn't
            // race with LoadLinesForSelectedOrder below against the same in-memory list (same
            // reasoning as OrderDetailForm/MainForm).
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
                Regels = po.Lines.Count
            }).ToList();
            _ordersGrid.SelectionChanged += OrdersGrid_SelectionChanged;

            if (_ordersGrid.Columns["Id"] != null)
                _ordersGrid.Columns["Id"].Visible = false;
            ApplyCurrencyColumns(_ordersGrid, "OrderTotal");
            ApplyDateTimeColumns(_ordersGrid, "CreatedAt", "SubmittedAt", "ConfirmedAt");

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

            // Traces each PO line back to the aanvraag/artikel it originated from — not shown
            // anywhere else, and exactly the kind of thing this screen exists for (OrderDetailForm's
            // own lines grid doesn't show it either, but that one is already reached *from* a
            // specific aanvraag, so the gap matters less there). One lookup per line is fine at
            // prototype scale (same reasoning as MainForm.GetSelectionsByLineIdAsync).
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
            _linesGrid.DataSource = order.Lines.Select(l =>
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
            SetHeaderText(_linesGrid, "QuantityMismatch", "Qty afwijkt?");
            SetHeaderText(_linesGrid, "PriceMismatch", "Prijs afwijkt?");
            SetHeaderText(_linesGrid, "DeliveryIsLate", "Te laat?");
            HighlightDeviatingLines(_linesGrid);

            var unconfirmedCount = order.Lines.Count(l => !l.ConfirmedQuantity.HasValue);
            _statusBar.Text = $"{order.SupplierCode}: {order.Status}"
                + (string.IsNullOrEmpty(order.SupplierOrderNumber) ? string.Empty : $" (ordernummer {order.SupplierOrderNumber})")
                + $" — {order.Lines.Count} regel(s), {unconfirmedCount} nog niet bevestigd.";
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
