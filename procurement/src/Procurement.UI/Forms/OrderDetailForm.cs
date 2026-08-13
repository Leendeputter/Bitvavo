using System;
using System.Linq;
using System.Windows.Forms;
using Procurement.Data.Repositories;

namespace Procurement.UI.Forms
{
    /// <summary>Order-detailscherm (spec §8.4).</summary>
    public class OrderDetailForm : Form
    {
        private readonly PurchaseOrderRepository _purchaseOrderRepository;
        private readonly SupplierOrderRepository _supplierOrderRepository;
        private readonly int _purchaseRequestId;

        private DataGridView _purchaseOrdersGrid;
        private DataGridView _supplierOrdersGrid;
        private Label _statusBar;

        public OrderDetailForm(PurchaseOrderRepository purchaseOrderRepository, SupplierOrderRepository supplierOrderRepository, int purchaseRequestId)
        {
            _purchaseOrderRepository = purchaseOrderRepository ?? throw new ArgumentNullException(nameof(purchaseOrderRepository));
            _supplierOrderRepository = supplierOrderRepository ?? throw new ArgumentNullException(nameof(supplierOrderRepository));
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

            var poLabel = new Label { Text = "ERP Purchase Orders:", Dock = DockStyle.Top, Height = 20, Padding = new Padding(4) };
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
            _purchaseOrdersGrid.SelectionChanged += PurchaseOrdersGrid_SelectionChanged;

            var soLabel = new Label { Text = "Supplier Orders voor geselecteerde PO:", Dock = DockStyle.Top, Height = 20, Padding = new Padding(4) };
            _supplierOrdersGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };

            _statusBar = new Label { Dock = DockStyle.Bottom, Height = 24, Padding = new Padding(4), BackColor = System.Drawing.SystemColors.ControlLight };

            Controls.Add(_supplierOrdersGrid);
            Controls.Add(soLabel);
            Controls.Add(_purchaseOrdersGrid);
            Controls.Add(poLabel);
            Controls.Add(_statusBar);
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            var orders = await _purchaseOrderRepository.GetByPurchaseRequestIdAsync(_purchaseRequestId);

            // Assigning DataSource can itself raise SelectionChanged; detach first so that
            // doesn't race with the explicit reload below against the same shared DbContext.
            _purchaseOrdersGrid.SelectionChanged -= PurchaseOrdersGrid_SelectionChanged;
            _purchaseOrdersGrid.DataSource = orders.Select(po => new
            {
                po.Id,
                po.ErpPoNumber,
                Status = po.Status.ToString(),
                po.CreatedAt,
                Regels = po.Lines.Count
            }).ToList();
            _purchaseOrdersGrid.SelectionChanged += PurchaseOrdersGrid_SelectionChanged;

            await LoadSupplierOrdersAsync();
        }

        private async void PurchaseOrdersGrid_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                await LoadSupplierOrdersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Fout bij laden van supplier orders", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadSupplierOrdersAsync()
        {
            if (_purchaseOrdersGrid.CurrentRow == null)
            {
                _supplierOrdersGrid.DataSource = null;
                _statusBar.Text = string.Empty;
                return;
            }

            var poId = (int)_purchaseOrdersGrid.CurrentRow.Cells["Id"].Value;
            var supplierOrders = await _supplierOrderRepository.GetByErpPoIdAsync(poId);

            _supplierOrdersGrid.DataSource = supplierOrders.Select(so => new
            {
                so.Id,
                so.SupplierCode,
                so.SupplierOrderNumber,
                Status = so.Status.ToString(),
                so.IdempotencyKey,
                so.OrderVersion,
                so.OrderTotal,
                so.Currency,
                so.SubmittedAt,
                so.ConfirmedAt
            }).ToList();

            _statusBar.Text = supplierOrders.Count == 0
                ? "Nog geen supplier order geplaatst voor deze PO."
                : $"Status: {string.Join(", ", supplierOrders.Select(o => $"{o.SupplierCode}={o.Status}"))}";
        }
    }
}
