using System;
using System.Linq;
using System.Windows.Forms;
using Procurement.Engine;
using static Procurement.UI.Support.GridFormatting;
using Procurement.UI.Support;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Goedkeuringsscherm (spec §8.3) — only lines that failed the auto-approve criteria show up here.
    /// Designer-built since 16 nov 2026 (ApprovalForm.Designer.cs), same pattern as UniPro:
    /// ClassicStyle.ApplyClassicStyle(this) right after InitializeComponent(); grid columns follow
    /// from data, so they stay in code. Fixed column widths, no Fill (user preference, 16 nov 2026).
    /// </summary>
    public partial class ApprovalForm : Form
    {
        private readonly ProcurementEngine _engine;

        public ApprovalForm(ProcurementEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            InitializeComponent();
            ClassicStyle.ApplyClassicStyle(this);
            this.BackColor = ClassicStyle.ClassicScreenBackColor;
            EnableRowNumberHeaders(dgv_approvals);
            GridColumnSettingsHelper.Attach(this); // remembers column widths per user, like UniPro
            Load += ApprovalForm_Load;
        }

        private async void ApprovalForm_Load(object sender, EventArgs e) => await RefreshAsync();

        private async void bt_approve_Click(object sender, EventArgs e) => await DecideAsync(approve: true);

        private async void bt_reject_Click(object sender, EventArgs e) => await DecideAsync(approve: false);

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            var pending = await _engine.GetPendingApprovalsAsync();
            dgv_approvals.DataSource = pending.Select(a => new
            {
                // Kept in the DataSource (DecideAsync reads it back from the grid) but hidden below
                // — just a sequential id, and the row header already shows a row number (user
                // request, 20 okt 2026: "in de row header staat ook een teller").
                a.Id,
                ErpRequestNumber = a.PurchaseRequestLine?.PurchaseRequest?.ErpRequestNumber,
                PartID = a.PurchaseRequestLine?.ErpArticleId,
                Part = a.PurchaseRequestLine?.ManufacturerPartNumber,
                Supplier = a.ProposedOffer?.SupplierCode,
                OfferedQty = a.ProposedOffer?.OfferedQuantity,
                Price = a.ProposedOffer?.UnitPrice,
                Reason = a.Reasons,
                a.CreatedAt
            }).ToList();
            ApplyQuantityColumns(dgv_approvals, "OfferedQty");
            ApplyCurrencyColumns(dgv_approvals, "Price");
            ApplyDateTimeColumns(dgv_approvals, "CreatedAt");
            SetHeaderText(dgv_approvals, "OfferedQty", "Offered Qty");
            if (dgv_approvals.Columns["Id"] != null) dgv_approvals.Columns["Id"].Visible = false;
            SetCharacterBasedColumnWidth(dgv_approvals, "ErpRequestNumber", 12);
            SetCharacterBasedColumnWidth(dgv_approvals, "PartID", 14);
            SetCharacterBasedColumnWidth(dgv_approvals, "Part", 18);
            SetCharacterBasedColumnWidth(dgv_approvals, "Supplier", 12);
            SetCharacterBasedColumnWidth(dgv_approvals, "OfferedQty", 12);
            SetCharacterBasedColumnWidth(dgv_approvals, "Price", 12);
            SetCharacterBasedColumnWidth(dgv_approvals, "Reason", 40);
            SetCharacterBasedColumnWidth(dgv_approvals, "CreatedAt", 19);

            lbl_status.Text = $"{pending.Count} regel(s) wachten op goedkeuring.";
        }

        private async System.Threading.Tasks.Task DecideAsync(bool approve)
        {
            if (dgv_approvals.CurrentRow == null)
            {
                MessageBox.Show(this, "Selecteer eerst een regel.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var approvalId = (int)dgv_approvals.CurrentRow.Cells["Id"].Value;

            bt_approve.Enabled = false;
            bt_reject.Enabled = false;
            try
            {
                await _engine.ApproveAsync(approvalId, approve, txt_comment.Text, Environment.UserName);
                txt_comment.Clear();
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                bt_approve.Enabled = true;
                bt_reject.Enabled = true;
            }
        }
    }
}
