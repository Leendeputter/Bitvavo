using System;
using System.Linq;
using System.Windows.Forms;
using Procurement.Data.Repositories;
using static Procurement.UI.Support.GridFormatting;

namespace Procurement.UI.Forms
{
    /// <summary>Audit-log viewer (spec §8.7).</summary>
    public class AuditLogForm : Form
    {
        private readonly ProcurementEventRepository _repository;

        private TextBox _entityTypeBox;
        private TextBox _eventTypeBox;
        private TextBox _supplierCodeBox;
        private DateTimePicker _fromPicker;
        private DateTimePicker _toPicker;
        private CheckBox _fromEnabled;
        private CheckBox _toEnabled;
        private DataGridView _grid;

        public AuditLogForm(ProcurementEventRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            InitializeComponent();
            Load += async (s, e) => await SearchAsync();
        }

        private void InitializeComponent()
        {
            Text = "Audit Log";
            Width = 1200;
            Height = 650;
            // CenterScreen, not CenterParent — shown owner-less via a plain Show() (independent
            // top-level window, user request, 20 okt 2026), so there's no Owner to center against.
            // StartPosition is moot for an MDI child (ShellForm.ShowAuditLog() sets MdiParent, user
            // request, 20 okt 2026 — "MDI-container?") — WinForms always places MDI children with its
            // own cascading layout regardless of this setting; left as CenterScreen only because
            // nothing depends on changing it. (The inner detail dialog opened from within this form
            // stays CenterParent, since that one is genuinely a modal dialog, not an MDI child.)
            StartPosition = FormStartPosition.CenterScreen;
            // Pale blue, matching MAX's own "MRP Detail" screenshot (user request, 20 okt 2026) — see
            // GridFormatting.ClassicScreenBackColor's own comment for why this replaced an earlier,
            // visually-no-op SystemColors.Control attempt.
            BackColor = ClassicScreenBackColor;

            var filterPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 70, FlowDirection = FlowDirection.LeftToRight };

            filterPanel.Controls.Add(new Label { Text = "EntityType:", AutoSize = true, Padding = new Padding(0, 8, 2, 0) });
            _entityTypeBox = new TextBox { Width = 130 };
            filterPanel.Controls.Add(_entityTypeBox);

            filterPanel.Controls.Add(new Label { Text = "EventType:", AutoSize = true, Padding = new Padding(8, 8, 2, 0) });
            _eventTypeBox = new TextBox { Width = 160 };
            filterPanel.Controls.Add(_eventTypeBox);

            filterPanel.Controls.Add(new Label { Text = "Supplier:", AutoSize = true, Padding = new Padding(8, 8, 2, 0) });
            _supplierCodeBox = new TextBox { Width = 100 };
            filterPanel.Controls.Add(_supplierCodeBox);

            _fromEnabled = new CheckBox { Text = "From:", AutoSize = true, Padding = new Padding(8, 4, 2, 0) };
            _fromPicker = new DateTimePicker { Width = 130, Value = DateTime.Today.AddDays(-7), Enabled = false };
            _fromEnabled.CheckedChanged += (s, e) => _fromPicker.Enabled = _fromEnabled.Checked;
            filterPanel.Controls.Add(_fromEnabled);
            filterPanel.Controls.Add(_fromPicker);

            _toEnabled = new CheckBox { Text = "To:", AutoSize = true, Padding = new Padding(8, 4, 2, 0) };
            _toPicker = new DateTimePicker { Width = 130, Value = DateTime.Today.AddDays(1), Enabled = false };
            _toEnabled.CheckedChanged += (s, e) => _toPicker.Enabled = _toEnabled.Checked;
            filterPanel.Controls.Add(_toEnabled);
            filterPanel.Controls.Add(_toPicker);

            var searchButton = new Button { Text = "Search", AutoSize = true, Margin = new Padding(8, 4, 0, 0) };
            searchButton.Click += async (s, e) => await SearchAsync();
            filterPanel.Controls.Add(searchButton);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.RowHeaderSelect,
                MultiSelect = false
            };
            EnableDoubleBuffering(_grid);
            EnableRowNumberHeaders(_grid);
            ApplyClassicHeaderStyle(_grid);
            // Error/RequestPayload/ResponsePayload routinely hold a full HTTP error body (e.g. a
            // DigiKey 403's JSON) that the grid's own column width truncates — dubbelklik geeft de
            // volledige, niet-afgekapte tekst in een los venster i.p.v. de kolom handmatig te moeten
            // verbreden of de cel te moeten kopiëren om de rest te kunnen lezen.
            _grid.CellDoubleClick += (s, e) => ShowRowDetail(e.RowIndex);

            Controls.Add(_grid);
            Controls.Add(filterPanel);
        }

        private void ShowRowDetail(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count) return;

            var row = _grid.Rows[rowIndex];
            var fields = new[] { "Timestamp", "EntityType", "EntityId", "EventType", "SupplierCode", "Status", "UserOrSystem", "RequestPayload", "ResponsePayload", "Error" };
            // FormattedValue (not Value) so Timestamp matches the grid's own DateTimeFormat instead
            // of DateTime's culture-default ToString().
            var text = string.Join(Environment.NewLine + Environment.NewLine,
                fields.Select(f => $"{f}:{Environment.NewLine}{row.Cells[f].FormattedValue}"));

            using (var dialog = new Form
            {
                Text = "Audit Log Detail",
                Width = 700,
                Height = 550,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false
            })
            {
                var textBox = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    WordWrap = true,
                    Font = new System.Drawing.Font("Consolas", 9f),
                    Text = text
                };
                var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
                var closeButton = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.OK };
                var copyButton = new Button { Text = "Copy to Clipboard", AutoSize = true };
                copyButton.Click += (s, e) => Clipboard.SetText(text);
                buttonPanel.Controls.Add(closeButton);
                buttonPanel.Controls.Add(copyButton);

                dialog.Controls.Add(textBox);
                dialog.Controls.Add(buttonPanel);
                dialog.AcceptButton = closeButton;
                dialog.ShowDialog(this);
            }
        }

        private async System.Threading.Tasks.Task SearchAsync()
        {
            var events = await _repository.SearchAsync(
                entityType: string.IsNullOrWhiteSpace(_entityTypeBox.Text) ? null : _entityTypeBox.Text,
                eventType: string.IsNullOrWhiteSpace(_eventTypeBox.Text) ? null : _eventTypeBox.Text,
                supplierCode: string.IsNullOrWhiteSpace(_supplierCodeBox.Text) ? null : _supplierCodeBox.Text,
                from: _fromEnabled.Checked ? _fromPicker.Value : (DateTime?)null,
                to: _toEnabled.Checked ? _toPicker.Value : (DateTime?)null);

            _grid.DataSource = events.Select(e => new
            {
                e.Timestamp,
                e.EntityType,
                e.EntityId,
                e.EventType,
                e.SupplierCode,
                e.Status,
                e.UserOrSystem,
                e.RequestPayload,
                e.ResponsePayload,
                e.Error
            }).ToList();
            ApplyDateTimeColumns(_grid, "Timestamp");
            // RequestPayload/ResponsePayload/Error are the genuinely free-text columns here (per
            // ShowRowDetail's own comment, routinely a full HTTP error body) — left in Fill with a
            // floor; everything else gets an explicit width (user request, 20 okt 2026).
            SetCharacterBasedColumnWidth(_grid, "Timestamp", 19);
            SetCharacterBasedColumnWidth(_grid, "EntityType", 16);
            SetCharacterBasedColumnWidth(_grid, "EntityId", 14);
            SetCharacterBasedColumnWidth(_grid, "EventType", 26);
            SetCharacterBasedColumnWidth(_grid, "SupplierCode", 12);
            SetCharacterBasedColumnWidth(_grid, "Status", 10);
            SetCharacterBasedColumnWidth(_grid, "UserOrSystem", 12);
            SetMinimumWidth(_grid, "RequestPayload", 160);
            SetMinimumWidth(_grid, "ResponsePayload", 160);
            SetMinimumWidth(_grid, "Error", 160);
        }
    }
}
