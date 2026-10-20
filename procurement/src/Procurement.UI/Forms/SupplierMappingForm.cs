using System;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Enums;
using Procurement.Data.Repositories;
using static Procurement.UI.Support.GridFormatting;

namespace Procurement.UI.Forms
{
    /// <summary>Supplier-mapping beheer (spec §8.5) — corrigeer een Unknown/Low match naar Verified.</summary>
    public class SupplierMappingForm : Form
    {
        private readonly SupplierProductMappingRepository _repository;

        private ComboBox _filterCombo;
        private DataGridView _grid;
        private Button _verifyButton;

        public SupplierMappingForm(SupplierProductMappingRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            InitializeComponent();
            Load += async (s, e) => await RefreshAsync();
        }

        private void InitializeComponent()
        {
            Text = "Supplier Mapping";
            Width = 900;
            Height = 550;
            // CenterScreen, not CenterParent — shown owner-less via a plain Show() (independent
            // top-level window, user request, 20 okt 2026), so there's no Owner to center against.
            StartPosition = FormStartPosition.CenterScreen;

            var topPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, FlowDirection = FlowDirection.LeftToRight };
            topPanel.Controls.Add(new Label { Text = "Filter by match confidence:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) });
            _filterCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            _filterCombo.Items.Add("(all)");
            _filterCombo.Items.AddRange(Enum.GetNames(typeof(MatchConfidence)));
            _filterCombo.SelectedIndex = 0;
            _filterCombo.SelectedIndexChanged += async (s, e) => await RefreshAsync();
            topPanel.Controls.Add(_filterCombo);

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

            _verifyButton = new Button { Text = "Mark as Verified", Dock = DockStyle.Bottom, AutoSize = true };
            _verifyButton.Click += async (s, e) => await VerifySelectedAsync();

            Controls.Add(_grid);
            Controls.Add(_verifyButton);
            Controls.Add(topPanel);
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            MatchConfidence? filter = null;
            if (_filterCombo.SelectedIndex > 0)
                filter = (MatchConfidence)Enum.Parse(typeof(MatchConfidence), (string)_filterCombo.SelectedItem);

            var mappings = await _repository.GetAllAsync(filter);
            _grid.DataSource = mappings.Select(m => new
            {
                m.Id,
                m.ErpArticleId,
                m.SupplierCode,
                m.SupplierPartNumber,
                m.Manufacturer,
                m.ManufacturerPartNumber,
                MatchConfidence = m.MatchConfidence.ToString(),
                m.CreatedAt
            }).ToList();
            ApplyDateTimeColumns(_grid, "CreatedAt");
            // ManufacturerPartNumber is the one genuinely free-text column here, left in Fill with a
            // floor (user request, 20 okt 2026).
            SetCharacterBasedColumnWidth(_grid, "Id", 6);
            SetCharacterBasedColumnWidth(_grid, "ErpArticleId", 14);
            SetCharacterBasedColumnWidth(_grid, "SupplierCode", 12);
            SetMinimumWidth(_grid, "SupplierPartNumber", 120);
            SetCharacterBasedColumnWidth(_grid, "Manufacturer", 16);
            SetMinimumWidth(_grid, "ManufacturerPartNumber", 140);
            SetCharacterBasedColumnWidth(_grid, "MatchConfidence", 14);
            SetCharacterBasedColumnWidth(_grid, "CreatedAt", 19);
        }

        private async System.Threading.Tasks.Task VerifySelectedAsync()
        {
            if (_grid.CurrentRow == null)
            {
                MessageBox.Show(this, "Selecteer eerst een mapping.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var id = (int)_grid.CurrentRow.Cells["Id"].Value;
            await _repository.UpdateConfidenceAsync(id, MatchConfidence.Verified);
            await RefreshAsync();
        }
    }
}
