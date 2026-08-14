using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Entities;
using Procurement.Data.Repositories;
using static Procurement.UI.Support.GridFormatting;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Order-detailscherm (spec §8.4). A PurchaseOrder now has exactly one supplier but can bundle
    /// lines from several PurchaseRequests (spec correction, aug 2026: grouped by supplier via
    /// "Order plaatsen", never per request) — so this screen doesn't own a 1-op-1
    /// PurchaseRequest-&gt;PurchaseOrder relation anymore. It shows every PO that contains at least
    /// one line of the selected aanvraag, found by joining on that aanvraag's own line IDs.
    /// </summary>
    public class OrderDetailForm : Form
    {
        private readonly PurchaseRequestRepository _purchaseRequestRepository;
        private readonly PurchaseOrderRepository _purchaseOrderRepository;
        private readonly int _purchaseRequestId;

        private DataGridView _purchaseOrdersGrid;
        private DataGridView _linesGrid;
        private Label _statusBar;
        private IReadOnlyList<PurchaseOrder> _loadedOrders = new List<PurchaseOrder>();

        public OrderDetailForm(PurchaseRequestRepository purchaseRequestRepository, PurchaseOrderRepository purchaseOrderRepository, int purchaseRequestId)
        {
            _purchaseRequestRepository = purchaseRequestRepository ?? throw new ArgumentNullException(nameof(purchaseRequestRepository));
            _purchaseOrderRepository = purchaseOrderRepository ?? throw new ArgumentNullException(nameof(purchaseOrderRepository));
            _purchaseRequestId = purchaseRequestId;
            InitializeComponent();
            Load += async (s, e) => await RefreshAsync();
        }

        private void InitializeComponent()
        {
            Text = $"Order details – aanvraag {_purchaseRequestId}";
            Width = 1000;
            Height = 600;
            StartPosition = FormStartPosition.CenterParent;

            var poLabel = new Label { Text = "Purchase Orders (1 per leverancier) met een regel van deze aanvraag:", Dock = DockStyle.Top, Height = 20, Padding = new Padding(4) };
            _purchaseOrdersGrid = new DataGridView
            {
                Dock = DockStyle.Top,
                Height = 220,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            EnableDoubleBuffering(_purchaseOrdersGrid);
            _purchaseOrdersGrid.SelectionChanged += PurchaseOrdersGrid_SelectionChanged;

            var linesLabel = new Label { Text = "Regels van geselecteerde PO (kunnen ook van andere aanvragen zijn):", Dock = DockStyle.Top, Height = 20, Padding = new Padding(4) };
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
            Controls.Add(_purchaseOrdersGrid);
            Controls.Add(poLabel);
            Controls.Add(_statusBar);
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            var request = await _purchaseRequestRepository.GetByIdAsync(_purchaseRequestId);
            var lineIds = request?.Lines.Select(l => l.Id).ToList() ?? new List<int>();
            _loadedOrders = await _purchaseOrderRepository.GetByPurchaseRequestLineIdsAsync(lineIds);

            // Assigning DataSource can itself raise SelectionChanged; detach first so that doesn't
            // race with LoadLinesForSelectedOrder below against the same in-memory list.
            _purchaseOrdersGrid.SelectionChanged -= PurchaseOrdersGrid_SelectionChanged;
            _purchaseOrdersGrid.DataSource = _loadedOrders.Select(po => new
            {
                po.Id,
                po.ErpPoNumber,
                po.SupplierCode,
                po.SupplierOrderNumber,
                Status = po.Status.ToString(),
                po.CreatedAt,
                po.OrderTotal,
                po.Currency,
                Regels = po.Lines.Count
            }).ToList();
            _purchaseOrdersGrid.SelectionChanged += PurchaseOrdersGrid_SelectionChanged;
            ApplyCurrencyColumns(_purchaseOrdersGrid, "OrderTotal");
            ApplyDateTimeColumns(_purchaseOrdersGrid, "CreatedAt");

            LoadLinesForSelectedOrder();
        }

        private void PurchaseOrdersGrid_SelectionChanged(object sender, EventArgs e) => LoadLinesForSelectedOrder();

        private void LoadLinesForSelectedOrder()
        {
            if (_purchaseOrdersGrid.CurrentRow == null)
            {
                _linesGrid.DataSource = null;
                _statusBar.Text = string.Empty;
                return;
            }

            var poId = (int)_purchaseOrdersGrid.CurrentRow.Cells["Id"].Value;
            var order = _loadedOrders.FirstOrDefault(o => o.Id == poId);
            if (order == null)
            {
                _linesGrid.DataSource = null;
                _statusBar.Text = string.Empty;
                return;
            }

            _linesGrid.DataSource = order.Lines.Select(l => new
            {
                l.PurchaseRequestLineId,
                l.SupplierPartNumber,
                l.Quantity,
                l.ConfirmedQuantity,
                l.UnitPrice,
                l.LineTotal
            }).ToList();
            ApplyQuantityColumns(_linesGrid, "Quantity", "ConfirmedQuantity");
            ApplyCurrencyColumns(_linesGrid, "UnitPrice", "LineTotal");

            _statusBar.Text = $"{order.SupplierCode}: {order.Status}"
                + (string.IsNullOrEmpty(order.SupplierOrderNumber) ? string.Empty : $" (ordernummer {order.SupplierOrderNumber})");
        }
    }
}
