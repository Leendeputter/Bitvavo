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
            Text = "Componenteninkoop – Purchase Requests";
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

            toolPanel.Controls.AddRange(new Control[]
            {
                _newRequestButton, _startSourcingButton, _orderDetailButton,
                _approvalsButton, _supplierMappingButton, _settingsButton, _auditLogButton
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
            _requestsGrid.SelectionChanged += async (s, e) => await LoadLinesForSelectedRequestAsync();

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

            _requestsGrid.DataSource = requests;
            SetStatus($"{requests.Count} openstaande aanvraag/aanvragen geladen.");
            await LoadLinesForSelectedRequestAsync();
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
