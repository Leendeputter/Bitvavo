using System;
using System.Linq;
using System.Windows.Forms;
using Procurement.UI.Composition;

namespace Procurement.UI.Forms
{
    /// <summary>Purchase Requests-overzicht (spec §8.1) — the application's main/start screen.</summary>
    public class MainForm : Form
    {
        private readonly CompositionRoot _composition;

        private DataGridView _requestsGrid;
        private DataGridView _linesGrid;
        private CheckBox _statusPlannedCheckBox;
        private CheckBox _statusApprovedCheckBox;
        private Button _newRequestButton;
        private Button _startSourcingButton;
        private Button _viewOffersButton;
        private Button _orderDetailButton;
        private Button _approvalsButton;
        private Button _supplierMappingButton;
        private Button _settingsButton;
        private Button _auditLogButton;
        private Label _statusLabel;

        public MainForm(CompositionRoot composition)
        {
            _composition = composition ?? throw new ArgumentNullException(nameof(composition));
            InitializeComponent();
            Load += async (s, e) => await RefreshRequestsAsync();
        }

        private void InitializeComponent()
        {
            Text = $"Componenteninkoop – {_composition.Session.CompanyName} ({_composition.Session.UserName})"
                 + (_composition.Session.TestMode ? " – TESTMODUS" : string.Empty);
            Width = 1200;
            Height = 800;
            StartPosition = FormStartPosition.CenterScreen;

            var toolPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, FlowDirection = FlowDirection.LeftToRight };
            _newRequestButton = new Button { Text = "Nieuwe testaanvraag toevoegen", AutoSize = true };
            _newRequestButton.Click += async (s, e) => await NewRequestAsync();
            _startSourcingButton = new Button { Text = "Sourcing starten", AutoSize = true };
            _startSourcingButton.Click += async (s, e) => await StartSourcingAsync();
            _orderDetailButton = new Button { Text = "Order details", AutoSize = true };
            _orderDetailButton.Click += (s, e) => ShowOrderDetail();
            _approvalsButton = new Button { Text = "Goedkeuringen...", AutoSize = true };
            _approvalsButton.Click += async (s, e) => await ShowApprovalsAsync();
            _supplierMappingButton = new Button { Text = "Supplier mapping...", AutoSize = true };
            _supplierMappingButton.Click += (s, e) => ShowSupplierMapping();
            _settingsButton = new Button { Text = "Instellingen...", AutoSize = true };
            _settingsButton.Click += (s, e) => ShowSettings();
            _auditLogButton = new Button { Text = "Audit-log...", AutoSize = true };
            _auditLogButton.Click += (s, e) => ShowAuditLog();

            // MAX Order_Master.STATUS_10 filter (user-supplied query): 1 = Planned, 2 = Approved.
            // Default: only Approved, matching what should auto-source without review.
            _statusPlannedCheckBox = new CheckBox { Text = "Planned (1)", AutoSize = true, Checked = false, Margin = new Padding(12, 12, 3, 3) };
            _statusApprovedCheckBox = new CheckBox { Text = "Approved (2)", AutoSize = true, Checked = true, Margin = new Padding(3, 12, 3, 3) };
            _statusPlannedCheckBox.CheckedChanged += async (s, e) => await OrderStatusFilterChangedAsync();
            _statusApprovedCheckBox.CheckedChanged += async (s, e) => await OrderStatusFilterChangedAsync();
            UpdateIncludedOrderStatuses();

            toolPanel.Controls.AddRange(new Control[]
            {
                _newRequestButton, _startSourcingButton, _orderDetailButton,
                _approvalsButton, _supplierMappingButton, _settingsButton, _auditLogButton,
                _statusPlannedCheckBox, _statusApprovedCheckBox
            });

            _requestsGrid = new DataGridView
            {
                Dock = DockStyle.Top,
                Height = 350,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            _requestsGrid.SelectionChanged += RequestsGrid_SelectionChanged;

            var linesLabel = new Label { Text = "Regels van geselecteerde aanvraag:", Dock = DockStyle.Top, Height = 20, Padding = new Padding(4) };

            _linesGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            _linesGrid.CellDoubleClick += (s, e) => ShowOffersForSelectedLine();

            _viewOffersButton = new Button { Text = "Offers bekijken (dubbelklik ook mogelijk)", Dock = DockStyle.Bottom, AutoSize = true };
            _viewOffersButton.Click += (s, e) => ShowOffersForSelectedLine();

            _statusLabel = new Label { Dock = DockStyle.Bottom, Height = 24, Text = "Gereed.", Padding = new Padding(4) };

            Controls.Add(_linesGrid);
            Controls.Add(linesLabel);
            Controls.Add(_viewOffersButton);
            Controls.Add(_requestsGrid);
            Controls.Add(toolPanel);
            Controls.Add(_statusLabel);
        }

        private void UpdateIncludedOrderStatuses()
        {
            var statuses = new System.Collections.Generic.HashSet<string>();
            if (_statusPlannedCheckBox.Checked) statuses.Add("1");
            if (_statusApprovedCheckBox.Checked) statuses.Add("2");
            _composition.ErpConnector.IncludedOrderStatuses = statuses;
        }

        private async System.Threading.Tasks.Task OrderStatusFilterChangedAsync()
        {
            UpdateIncludedOrderStatuses();
            try
            {
                await RefreshRequestsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Fout bij laden van aanvragen", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async System.Threading.Tasks.Task RefreshRequestsAsync()
        {
            SetStatus("Aanvragen laden...");
            var requests = (await _composition.Engine.GetOpenPurchaseRequestsAsync())
                .Select(r => new
                {
                    r.Id,
                    r.ErpRequestNumber,
                    r.RequestDate,
                    r.RequiredDate,
                    r.Warehouse,
                    r.Project,
                    r.Priority,
                    Status = r.Status.ToString(),
                    Regels = r.Lines.Count
                })
                .ToList();

            // Assigning DataSource can itself raise SelectionChanged (when the grid picks a
            // default current cell); detach first so that doesn't race with the explicit reload
            // below against the same shared DbContext.
            _requestsGrid.SelectionChanged -= RequestsGrid_SelectionChanged;
            _requestsGrid.DataSource = requests;
            _requestsGrid.SelectionChanged += RequestsGrid_SelectionChanged;

            SetStatus($"{requests.Count} openstaande aanvraag/aanvragen geladen.");
            await LoadLinesForSelectedRequestAsync();
        }

        private async void RequestsGrid_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                await LoadLinesForSelectedRequestAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Fout bij laden van regels", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadLinesForSelectedRequestAsync()
        {
            var requestId = GetSelectedRequestId();
            if (requestId == null)
            {
                _linesGrid.DataSource = null;
                return;
            }

            var request = await _composition.PurchaseRequestRepository.GetByIdAsync(requestId.Value);
            if (request == null)
            {
                _linesGrid.DataSource = null;
                return;
            }

            _linesGrid.DataSource = request.Lines.Select(l => new
            {
                l.Id,
                l.ErpArticleId,
                l.Manufacturer,
                l.ManufacturerPartNumber,
                l.Description,
                l.RequestedQuantity,
                l.RequiredDate,
                Packaging = l.PackagingRequirement.ToString(),
                l.ReelRequirement
            }).ToList();
        }

        private int? GetSelectedRequestId()
        {
            if (_requestsGrid.CurrentRow == null) return null;
            return (int)_requestsGrid.CurrentRow.Cells["Id"].Value;
        }

        private int? GetSelectedLineId()
        {
            if (_linesGrid.CurrentRow == null) return null;
            return (int)_linesGrid.CurrentRow.Cells["Id"].Value;
        }

        private async System.Threading.Tasks.Task NewRequestAsync()
        {
            using (var form = new NewPurchaseRequestForm(_composition.PurchaseRequestRepository))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    await RefreshRequestsAsync();
                }
            }
        }

        private async System.Threading.Tasks.Task StartSourcingAsync()
        {
            var requestId = GetSelectedRequestId();
            if (requestId == null)
            {
                MessageBox.Show(this, "Selecteer eerst een aanvraag.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Guard against a double-click firing two overlapping SourcePurchaseRequestAsync
            // calls — also makes clear to the user that something is happening, instead of a
            // click that appears to do nothing while sourcing runs in the background.
            _startSourcingButton.Enabled = false;
            SetStatus("Sourcing wordt gestart (DigiKey + Farnell parallel)...");
            try
            {
                await _composition.Engine.SourcePurchaseRequestAsync(requestId.Value);
                SetStatus("Sourcing voltooid.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Fout tijdens sourcing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Sourcing mislukt.");
            }
            finally
            {
                _startSourcingButton.Enabled = true;
            }

            await RefreshRequestsAsync();
        }

        private void ShowOffersForSelectedLine()
        {
            var lineId = GetSelectedLineId();
            if (lineId == null)
            {
                MessageBox.Show(this, "Selecteer eerst een regel.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var form = new OfferComparisonForm(_composition.Engine, lineId.Value))
            {
                form.ShowDialog(this);
            }
        }

        private void ShowOrderDetail()
        {
            var requestId = GetSelectedRequestId();
            if (requestId == null)
            {
                MessageBox.Show(this, "Selecteer eerst een aanvraag.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var form = new OrderDetailForm(_composition.PurchaseOrderRepository, _composition.SupplierOrderRepository, requestId.Value))
            {
                form.ShowDialog(this);
            }
        }

        private async System.Threading.Tasks.Task ShowApprovalsAsync()
        {
            using (var form = new ApprovalForm(_composition.Engine))
            {
                form.ShowDialog(this);
            }
            await RefreshRequestsAsync();
        }

        private void ShowSupplierMapping()
        {
            using (var form = new SupplierMappingForm(_composition.SupplierProductMappingRepository))
            {
                form.ShowDialog(this);
            }
        }

        private void ShowSettings()
        {
            using (var form = new SettingsForm(_composition.PolicyRepository, _composition.SupplierRepository))
            {
                form.ShowDialog(this);
            }
        }

        private void ShowAuditLog()
        {
            using (var form = new AuditLogForm(_composition.ProcurementEventRepository))
            {
                form.ShowDialog(this);
            }
        }

        private void SetStatus(string text) => _statusLabel.Text = text;
    }
}
