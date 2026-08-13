using System;
using System.Linq;
using System.Windows.Forms;
using Procurement.Engine;

namespace Procurement.UI.Forms
{
    /// <summary>Offer-vergelijkingsscherm (spec §8.2).</summary>
    public class OfferComparisonForm : Form
    {
        private readonly ProcurementEngine _engine;
        private readonly int _purchaseRequestLineId;

        private DataGridView _offersGrid;
        private Label _reasonLabel;
        private TextBox _overrideReasonBox;
        private Button _chooseOfferButton;
        private Label _statusLabel;

        public OfferComparisonForm(ProcurementEngine engine, int purchaseRequestLineId)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _purchaseRequestLineId = purchaseRequestLineId;
            InitializeComponent();
            Load += async (s, e) => await RefreshAsync();
        }

        private void InitializeComponent()
        {
            Text = $"Offers voor regel {_purchaseRequestLineId}";
            Width = 1100;
            Height = 600;
            StartPosition = FormStartPosition.CenterParent;

            _reasonLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 60,
                Padding = new Padding(6),
                Text = "Nog geen automatische selectie.",
                AutoEllipsis = true
            };

            _offersGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };

            var overridePanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, FlowDirection = FlowDirection.LeftToRight };
            overridePanel.Controls.Add(new Label { Text = "Reden voor handmatige keuze (verplicht):", AutoSize = true, Padding = new Padding(0, 8, 4, 0) });
            _overrideReasonBox = new TextBox { Width = 400 };
            _chooseOfferButton = new Button { Text = "Deze offer handmatig kiezen", AutoSize = true };
            _chooseOfferButton.Click += async (s, e) => await ChooseSelectedOfferAsync();
            overridePanel.Controls.Add(_overrideReasonBox);
            overridePanel.Controls.Add(_chooseOfferButton);

            _statusLabel = new Label { Dock = DockStyle.Bottom, Height = 24, Padding = new Padding(4) };

            Controls.Add(_offersGrid);
            Controls.Add(overridePanel);
            Controls.Add(_statusLabel);
            Controls.Add(_reasonLabel);
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            var offers = await _engine.GetOffersForLineAsync(_purchaseRequestLineId);
            var selection = await _engine.GetSelectionForLineAsync(_purchaseRequestLineId);

            _offersGrid.DataSource = offers.Select(o => new
            {
                o.Id,
                Voorgesteld = selection != null && selection.SelectedOfferId == o.Id ? "★" : "",
                o.SupplierCode,
                o.SupplierPartNumber,
                MatchConfidence = o.MatchConfidence.ToString(),
                o.OfferedQuantity,
                o.UnitPrice,
                o.Currency,
                Packaging = o.PackagingType.ToString(),
                Reel = o.ReelType?.ToString() ?? "-",
                o.LandedCost,
                o.LeadTimeDays,
                o.AvailableQuantity,
                LevertDatum = o.EstimatedDeliveryDate.ToShortDateString()
            }).ToList();

            _reasonLabel.Text = selection != null
                ? $"Voorgesteld door de engine ({selection.Mode}):\n{selection.ReasonSummary}"
                : "Nog geen selectie voor deze regel (start eerst sourcing).";
        }

        private async System.Threading.Tasks.Task ChooseSelectedOfferAsync()
        {
            if (_offersGrid.CurrentRow == null)
            {
                MessageBox.Show(this, "Selecteer eerst een offer.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(_overrideReasonBox.Text))
            {
                MessageBox.Show(this, "Een reden is verplicht bij een handmatige keuze.", "Validatie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var offerId = (int)_offersGrid.CurrentRow.Cells["Id"].Value;

            _chooseOfferButton.Enabled = false;
            try
            {
                _statusLabel.Text = "Bezig met verwerken...";
                await _engine.SelectOfferManuallyAsync(_purchaseRequestLineId, offerId, _overrideReasonBox.Text, Environment.UserName);
                _statusLabel.Text = "Handmatige keuze verwerkt en order geplaatst (indien mogelijk).";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _statusLabel.Text = "Mislukt.";
            }
            finally
            {
                _chooseOfferButton.Enabled = true;
            }
        }
    }
}
