using System;
using System.Linq;
using System.Windows.Forms;
using Procurement.Engine;
using static Procurement.UI.Support.GridFormatting;

namespace Procurement.UI.Forms
{
    /// <summary>Goedkeuringsscherm (spec §8.3) — only lines that failed the auto-approve criteria show up here.</summary>
    public class ApprovalForm : Form
    {
        private readonly ProcurementEngine _engine;

        private DataGridView _grid;
        private TextBox _commentBox;
        private Button _approveButton;
        private Button _rejectButton;
        private Label _statusLabel;

        public ApprovalForm(ProcurementEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            InitializeComponent();
            Load += async (s, e) => await RefreshAsync();
        }

        private void InitializeComponent()
        {
            Text = "Goedkeuringen";
            Width = 1000;
            Height = 550;
            StartPosition = FormStartPosition.CenterParent;

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            EnableDoubleBuffering(_grid);

            var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70, FlowDirection = FlowDirection.LeftToRight };
            actionPanel.Controls.Add(new Label { Text = "Commentaar:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) });
            _commentBox = new TextBox { Width = 400 };
            _approveButton = new Button { Text = "Goedkeuren", AutoSize = true };
            _approveButton.Click += async (s, e) => await DecideAsync(approve: true);
            _rejectButton = new Button { Text = "Afwijzen", AutoSize = true };
            _rejectButton.Click += async (s, e) => await DecideAsync(approve: false);
            actionPanel.Controls.Add(_commentBox);
            actionPanel.Controls.Add(_approveButton);
            actionPanel.Controls.Add(_rejectButton);

            _statusLabel = new Label { Dock = DockStyle.Bottom, Height = 24, Padding = new Padding(4) };

            Controls.Add(_grid);
            Controls.Add(actionPanel);
            Controls.Add(_statusLabel);
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            var pending = await _engine.GetPendingApprovalsAsync();
            _grid.DataSource = pending.Select(a => new
            {
                a.Id,
                Regel = a.PurchaseRequestLineId,
                Onderdeel = a.PurchaseRequestLine?.ManufacturerPartNumber,
                Leverancier = a.ProposedOffer?.SupplierCode,
                Aangeboden = a.ProposedOffer?.OfferedQuantity,
                Prijs = a.ProposedOffer?.UnitPrice,
                Reden = a.Reasons,
                a.CreatedAt
            }).ToList();
            ApplyQuantityColumns(_grid, "Aangeboden");
            ApplyCurrencyColumns(_grid, "Prijs");
            ApplyDateTimeColumns(_grid, "CreatedAt");

            _statusLabel.Text = $"{pending.Count} regel(s) wachten op goedkeuring.";
        }

        private async System.Threading.Tasks.Task DecideAsync(bool approve)
        {
            if (_grid.CurrentRow == null)
            {
                MessageBox.Show(this, "Selecteer eerst een regel.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var approvalId = (int)_grid.CurrentRow.Cells["Id"].Value;

            _approveButton.Enabled = false;
            _rejectButton.Enabled = false;
            try
            {
                await _engine.ApproveAsync(approvalId, approve, _commentBox.Text, Environment.UserName);
                _commentBox.Clear();
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _approveButton.Enabled = true;
                _rejectButton.Enabled = true;
            }
        }
    }
}
