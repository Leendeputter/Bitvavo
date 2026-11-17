using System;
using System.Linq;
using System.Windows.Forms;
using Procurement.Data.Repositories;
using Procurement.UI.Support;
using static Procurement.UI.Support.GridFormatting;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Audit-log viewer (spec §8.7). Built with the Visual Studio Designer (AuditLogForm.Designer.cs,
    /// user request, 16 nov 2026), UniPro control naming and ClassicStyle. Grid columns come from the
    /// DataSource, so their widths are set in code; no Fill mode, so columns keep their width when the
    /// window is maximized (user rule, 16 nov 2026). Double-clicking a row shows the full, untruncated
    /// text (Error/RequestPayload/ResponsePayload often hold a full HTTP error body).
    /// </summary>
    public partial class AuditLogForm : Form
    {
        private static readonly string[] DetailFields =
        {
            "Timestamp", "EntityType", "EntityId", "EventType", "SupplierCode", "Status",
            "UserOrSystem", "RequestPayload", "ResponsePayload", "Error"
        };

        private readonly ProcurementEventRepository _repository;

        public AuditLogForm(ProcurementEventRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            InitializeComponent();
            ClassicStyle.ApplyClassicStyle(this);
            this.BackColor = ClassicStyle.ClassicScreenBackColor;
            EnableRowNumberHeaders(dgv_auditlog);

            dtp_from.Value = DateTime.Today.AddDays(-7);
            dtp_to.Value = DateTime.Today.AddDays(1);

            GridColumnSettingsHelper.Attach(this); // remembers column widths per user, like UniPro
            Load += AuditLogForm_Load;
        }

        private async void AuditLogForm_Load(object sender, EventArgs e) => await SearchAsync();

        private async void bt_search_Click(object sender, EventArgs e) => await SearchAsync();

        private void chk_from_CheckedChanged(object sender, EventArgs e) => dtp_from.Enabled = chk_from.Checked;

        private void chk_to_CheckedChanged(object sender, EventArgs e) => dtp_to.Enabled = chk_to.Checked;

        private void dgv_auditlog_CellDoubleClick(object sender, DataGridViewCellEventArgs e) => ShowRowDetail(e.RowIndex);

        private void ShowRowDetail(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgv_auditlog.Rows.Count) return;

            var row = dgv_auditlog.Rows[rowIndex];
            // FormattedValue (not Value) so Timestamp matches the grid's own DateTimeFormat.
            var text = string.Join(Environment.NewLine + Environment.NewLine,
                DetailFields.Select(f => $"{f}:{Environment.NewLine}{row.Cells[f].FormattedValue}"));

            using (var dialog = new AuditLogDetailForm(text))
                dialog.ShowDialog(this);
        }

        private async System.Threading.Tasks.Task SearchAsync()
        {
            var events = await _repository.SearchAsync(
                entityType: string.IsNullOrWhiteSpace(txt_entityType.Text) ? null : txt_entityType.Text,
                eventType: string.IsNullOrWhiteSpace(txt_eventType.Text) ? null : txt_eventType.Text,
                supplierCode: string.IsNullOrWhiteSpace(txt_supplier.Text) ? null : txt_supplier.Text,
                from: chk_from.Checked ? dtp_from.Value : (DateTime?)null,
                to: chk_to.Checked ? dtp_to.Value : (DateTime?)null);

            BindSortable(dgv_auditlog, events.Select(e => new
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
            }));
            ApplyDateTimeColumns(dgv_auditlog, "Timestamp");
            SetCharacterBasedColumnWidth(dgv_auditlog, "Timestamp", 19);
            SetCharacterBasedColumnWidth(dgv_auditlog, "EntityType", 16);
            SetCharacterBasedColumnWidth(dgv_auditlog, "EntityId", 14);
            SetCharacterBasedColumnWidth(dgv_auditlog, "EventType", 26);
            SetCharacterBasedColumnWidth(dgv_auditlog, "SupplierCode", 12);
            SetCharacterBasedColumnWidth(dgv_auditlog, "Status", 10);
            SetCharacterBasedColumnWidth(dgv_auditlog, "UserOrSystem", 12);
            // Free-text columns: fixed width too; double-click a row for the full text.
            SetCharacterBasedColumnWidth(dgv_auditlog, "RequestPayload", 30);
            SetCharacterBasedColumnWidth(dgv_auditlog, "ResponsePayload", 30);
            SetCharacterBasedColumnWidth(dgv_auditlog, "Error", 40);
        }
    }
}
