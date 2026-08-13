using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Entities;
using Procurement.Erp;
using Procurement.UI.Composition;

namespace Procurement.UI.Forms
{
    /// <summary>Purchase Requests-overzicht (spec §8.1) — the application's main/start screen.</summary>
    public class MainForm : Form
    {
        private readonly CompositionRoot _composition;

        private DataGridView _requestsGrid;
        private DataGridView _linesGrid;

        // "Orders ophalen uit MAX" group — status filter + the Query button that actually triggers
        // the MAX round-trip (nothing here runs automatically, only on Query click).
        private CheckBox _statusPlannedCheckBox;
        private CheckBox _statusApprovedCheckBox;
        private Button _queryButton;

        // "Due Date Range" group.
        private CheckBox _dueDateEnableCheckBox;
        private DateTimePicker _dueDateStartPicker;
        private DateTimePicker _dueDateEndPicker;

        // "Filter" group — generic range filter, applied to whichever column "Select By" picks.
        private ComboBox _rangeFieldCombo;
        private TextBox _rangeStartBox;
        private TextBox _rangeEndBox;

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
            // Niets wordt automatisch geladen bij het openen — de grid blijft leeg tot de
            // gebruiker zelf op "Query" klikt (dat geldt ook voor de al lokaal gesynchroniseerde
            // aanvragen, die enkel na een expliciete actie opnieuw worden getoond).
        }

        private void InitializeComponent()
        {
            Text = $"Componenteninkoop – {_composition.Session.CompanyName} ({_composition.Session.UserName})"
                 + (_composition.Session.TestMode ? " – TESTMODUS" : string.Empty);
            Width = 1300;
            Height = 860;
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

            // Apart van de actieknoppen hierboven: het ophalen/filteren van MAX-orders krijgt een
            // eigen rij met groepsvakken (Query + status-filter, Due Date Range, en de generieke
            // "Select By"-filter).
            var filterPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(4)
            };
            filterPanel.Controls.Add(BuildQueryGroup());
            filterPanel.Controls.Add(BuildDueDateRangeGroup());
            filterPanel.Controls.Add(BuildRangeFilterGroup());

            _requestsGrid = new DataGridView
            {
                Dock = DockStyle.Top,
                Height = 300,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            _linesGrid.CellDoubleClick += (s, e) => ShowOffersForSelectedLine();

            _viewOffersButton = new Button { Text = "Offers bekijken (dubbelklik ook mogelijk)", Dock = DockStyle.Bottom, AutoSize = true };
            _viewOffersButton.Click += (s, e) => ShowOffersForSelectedLine();

            _statusLabel = new Label { Dock = DockStyle.Bottom, Height = 24, Text = "Gereed. Klik op \"Query\" om orders op te halen uit MAX.", Padding = new Padding(4) };

            // Volgorde bepaalt de docking-stapeling (laatst toegevoegde Top-control komt bovenaan):
            // toolPanel (buitenste rand), dan filterPanel, dan requestsGrid.
            Controls.Add(_linesGrid);
            Controls.Add(linesLabel);
            Controls.Add(_viewOffersButton);
            Controls.Add(_requestsGrid);
            Controls.Add(filterPanel);
            Controls.Add(toolPanel);
            Controls.Add(_statusLabel);
        }

        private GroupBox BuildQueryGroup()
        {
            var group = new GroupBox { Text = "Orders ophalen uit MAX", AutoSize = true, Padding = new Padding(8), Margin = new Padding(4) };
            var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };

            // MAX Order_Master.STATUS_10 filter (user-supplied query): 1 = Planned, 2 = Approved.
            // Default: only Approved, matching what should auto-source without review. Alleen van
            // invloed op de *volgende* keer dat op Query wordt geklikt.
            _statusPlannedCheckBox = new CheckBox { Text = "1 - Planned", AutoSize = true, Checked = false };
            _statusApprovedCheckBox = new CheckBox { Text = "2 - Approved", AutoSize = true, Checked = true };
            UpdateIncludedOrderStatuses();

            _queryButton = new Button { Text = "Query", AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
            _queryButton.Click += async (s, e) => await QueryOrdersAsync();

            layout.Controls.Add(_statusPlannedCheckBox);
            layout.Controls.Add(_statusApprovedCheckBox);
            layout.Controls.Add(_queryButton);
            group.Controls.Add(layout);
            return group;
        }

        private GroupBox BuildDueDateRangeGroup()
        {
            var group = new GroupBox { Text = "Due Date Range", AutoSize = true, Padding = new Padding(8), Margin = new Padding(4) };
            var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };

            _dueDateEnableCheckBox = new CheckBox { Text = "Enable", AutoSize = true };
            _dueDateStartPicker = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Enabled = false };
            _dueDateEndPicker = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Enabled = false };
            _dueDateEnableCheckBox.CheckedChanged += (s, e) =>
            {
                _dueDateStartPicker.Enabled = _dueDateEnableCheckBox.Checked;
                _dueDateEndPicker.Enabled = _dueDateEnableCheckBox.Checked;
            };

            AddFilterRow(layout, string.Empty, _dueDateEnableCheckBox);
            AddFilterRow(layout, "Start Date", _dueDateStartPicker);
            AddFilterRow(layout, "End Date", _dueDateEndPicker);

            group.Controls.Add(layout);
            return group;
        }

        private GroupBox BuildRangeFilterGroup()
        {
            var group = new GroupBox { Text = "Filter", AutoSize = true, Padding = new Padding(8), Margin = new Padding(4) };
            var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };

            _rangeFieldCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
            _rangeFieldCombo.Items.AddRange(new object[] { "Order Number", "Customer", "Part" });
            _rangeFieldCombo.SelectedIndex = 0;
            _rangeStartBox = new TextBox { Width = 110 };
            _rangeEndBox = new TextBox { Width = 110 };

            AddFilterRow(layout, "Select By", _rangeFieldCombo);
            AddFilterRow(layout, "Start", _rangeStartBox);
            AddFilterRow(layout, "End", _rangeEndBox);

            group.Controls.Add(layout);
            return group;
        }

        private static void AddFilterRow(TableLayoutPanel layout, string label, Control control)
        {
            var row = layout.RowCount;
            layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (string.IsNullOrEmpty(label))
            {
                layout.Controls.Add(control, 0, row);
                layout.SetColumnSpan(control, 2);
                return;
            }
            layout.Controls.Add(new Label { Text = label, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(3, 6, 3, 3) }, 0, row);
            layout.Controls.Add(control, 1, row);
        }

        private void UpdateIncludedOrderStatuses()
        {
            var statuses = new HashSet<string>();
            if (_statusPlannedCheckBox.Checked) statuses.Add("1");
            if (_statusApprovedCheckBox.Checked) statuses.Add("2");
            _composition.ErpConnector.IncludedOrderStatuses = statuses;
        }

        /// <summary>Laadt enkel de al lokaal gesynchroniseerde aanvragen — raakt MAX niet aan. Gebruikt na lokale wijzigingen (nieuwe testaanvraag, sourcing, goedkeuringen), niet bij het openen van het scherm.</summary>
        private async System.Threading.Tasks.Task LoadLocalRequestsAsync()
        {
            SetStatus("Aanvragen laden...");
            var requests = await _composition.PurchaseRequestRepository.GetOpenAsync();
            PopulateRequestsGrid(requests);
            SetStatus($"{requests.Count} openstaande aanvraag/aanvragen geladen.");
            await LoadLinesForSelectedRequestAsync();
        }

        /// <summary>Vraagt open orders op uit MAX (Order_Master/Part_Master, incl. Part_Vendor-mapping sync) met de huidige filterinstellingen, en synchroniseert ze naar lokale aanvragen — enkel aangeroepen via de "Query"-knop.</summary>
        private async System.Threading.Tasks.Task RefreshRequestsAsync()
        {
            SetStatus("Orders opvragen uit MAX...");
            ApplyFiltersToErpConnector();
            var requests = await _composition.Engine.GetOpenPurchaseRequestsAsync();
            PopulateRequestsGrid(requests);
            SetStatus($"{requests.Count} openstaande aanvraag/aanvragen geladen.");
            await LoadLinesForSelectedRequestAsync();
        }

        private void ApplyFiltersToErpConnector()
        {
            UpdateIncludedOrderStatuses();

            _composition.ErpConnector.DueDateFilterStart = _dueDateEnableCheckBox.Checked ? _dueDateStartPicker.Value.Date : (DateTime?)null;
            _composition.ErpConnector.DueDateFilterEnd = _dueDateEnableCheckBox.Checked ? _dueDateEndPicker.Value.Date : (DateTime?)null;

            var rangeStart = _rangeStartBox.Text.Trim();
            var rangeEnd = _rangeEndBox.Text.Trim();
            var hasRange = !string.IsNullOrEmpty(rangeStart) || !string.IsNullOrEmpty(rangeEnd);

            _composition.ErpConnector.RangeFilterField = hasRange ? GetSelectedRangeField() : (MaxOrderRangeField?)null;
            _composition.ErpConnector.RangeFilterStart = string.IsNullOrEmpty(rangeStart) ? null : rangeStart;
            _composition.ErpConnector.RangeFilterEnd = string.IsNullOrEmpty(rangeEnd) ? null : rangeEnd;
        }

        private MaxOrderRangeField GetSelectedRangeField()
        {
            var text = _rangeFieldCombo.SelectedItem as string;
            if (text == "Customer") return MaxOrderRangeField.Customer;
            if (text == "Part") return MaxOrderRangeField.Part;
            return MaxOrderRangeField.OrderNumber;
        }

        private async System.Threading.Tasks.Task QueryOrdersAsync()
        {
            _queryButton.Enabled = false;
            UseWaitCursor = true;
            try
            {
                await RefreshRequestsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Fout bij opvragen van orders", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
                _queryButton.Enabled = true;
            }
        }

        private void PopulateRequestsGrid(IReadOnlyList<PurchaseRequest> requests)
        {
            // Kolomvolgorde/-namen op verzoek: Order, Status, Firm, Type, PartID, Rev, Desc1,
            // Desc2, Quantity, Cost, Cnv, DueDate, Reference, Manufacturing Part, Customer,
            // StockID — zoals in de MAX Order_Master/Part_Master-query. Elke MAX-order sync't naar
            // precies één PurchaseRequestLine (zie MaxErpConnector), dus de eerste regel volstaat
            // hier; handmatige testaanvragen hebben ook altijd precies één regel.
            var rows = requests
                .Select(r =>
                {
                    var line = r.Lines.FirstOrDefault();
                    return new
                    {
                        Id = r.Id,
                        Order = r.ErpRequestNumber,
                        Status = r.MaxOrderStatus,
                        Firm = line?.Firm ?? false,
                        Type = line?.PartType,
                        PartID = line?.ErpArticleId,
                        Rev = line?.Revision,
                        Desc1 = line?.Desc1,
                        Desc2 = line?.Desc2,
                        Quantity = line?.RequestedQuantity ?? 0,
                        Cost = line?.Cost,
                        Cnv = line?.CostConv,
                        DueDate = r.RequiredDate,
                        Reference = r.Project,
                        ManufacturingPart = line?.ManufacturerPartNumber,
                        Customer = line?.Customer,
                        StockID = line?.StockId
                    };
                })
                .ToList();

            // Assigning DataSource can itself raise SelectionChanged (when the grid picks a
            // default current cell); detach first so that doesn't race with the explicit reload
            // below against the same shared DbContext.
            _requestsGrid.SelectionChanged -= RequestsGrid_SelectionChanged;
            _requestsGrid.DataSource = rows;
            _requestsGrid.SelectionChanged += RequestsGrid_SelectionChanged;

            if (_requestsGrid.Columns["Id"] != null)
                _requestsGrid.Columns["Id"].Visible = false;
            SetHeaderText("ManufacturingPart", "Manufacturing Part");
        }

        private void SetHeaderText(string columnName, string headerText)
        {
            if (_requestsGrid.Columns[columnName] != null)
                _requestsGrid.Columns[columnName].HeaderText = headerText;
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
                    await LoadLocalRequestsAsync();
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

            await LoadLocalRequestsAsync();
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
            await LoadLocalRequestsAsync();
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
