using System;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Enums;
using Procurement.Data.Repositories;
using static Procurement.UI.Support.GridFormatting;
using Procurement.UI.Support;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Supplier-mapping beheer (spec §8.5) — corrigeer een Unknown/Low match naar Verified.
    /// Designer-built since 16 nov 2026 (SupplierMappingForm.Designer.cs), same pattern as UniPro:
    /// ClassicStyle.ApplyClassicStyle(this) right after InitializeComponent(); the filter's items
    /// and the grid's columns follow from data, so they stay in code.
    /// </summary>
    public partial class SupplierMappingForm : Form
    {
        private readonly SupplierProductMappingRepository _repository;

        // Set while the constructor fills cb_filter, so that doesn't trigger a query before Load.
        private bool _isInitializing;

        public SupplierMappingForm(SupplierProductMappingRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            InitializeComponent();
            ClassicStyle.ApplyClassicStyle(this);
            this.BackColor = ClassicStyle.ClassicScreenBackColor;
            EnableRowNumberHeaders(dgv_mappings);

            _isInitializing = true;
            cb_filter.Items.Add("(all)");
            cb_filter.Items.AddRange(Enum.GetNames(typeof(MatchConfidence)));
            cb_filter.SelectedIndex = 0;
            _isInitializing = false;

            GridColumnSettingsHelper.Attach(this); // remembers column widths per user, like UniPro
            Load += SupplierMappingForm_Load;
        }

        private async void SupplierMappingForm_Load(object sender, EventArgs e) => await RefreshAsync();

        private async void cb_filter_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isInitializing) return;
            await RefreshAsync();
        }

        private async void bt_verify_Click(object sender, EventArgs e) => await VerifySelectedAsync();

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            MatchConfidence? filter = null;
            if (cb_filter.SelectedIndex > 0)
                filter = (MatchConfidence)Enum.Parse(typeof(MatchConfidence), (string)cb_filter.SelectedItem);

            var mappings = await _repository.GetAllAsync(filter);
            BindSortable(dgv_mappings, mappings.Select(m => new
            {
                m.Id,
                m.ErpArticleId,
                m.SupplierCode,
                m.SupplierPartNumber,
                m.Manufacturer,
                m.ManufacturerPartNumber,
                MatchConfidence = m.MatchConfidence.ToString(),
                m.CreatedAt
            }));
            ApplyDateTimeColumns(dgv_mappings, "CreatedAt");
            // Fixed widths, no Fill (user feedback, 16 nov 2026: maximizing stretched every column,
            // "niet handig als ik de kolomrijen even breed wil houden"). The grid itself still grows
            // with the window (more rows/room), only the columns keep their width; user-dragged
            // widths are remembered by GridColumnSettingsHelper.
            SetCharacterBasedColumnWidth(dgv_mappings, "Id", 6);
            SetCharacterBasedColumnWidth(dgv_mappings, "ErpArticleId", 14);
            SetCharacterBasedColumnWidth(dgv_mappings, "SupplierCode", 12);
            SetCharacterBasedColumnWidth(dgv_mappings, "SupplierPartNumber", 20);
            SetCharacterBasedColumnWidth(dgv_mappings, "Manufacturer", 16);
            SetCharacterBasedColumnWidth(dgv_mappings, "ManufacturerPartNumber", 24);
            SetCharacterBasedColumnWidth(dgv_mappings, "MatchConfidence", 14);
            SetCharacterBasedColumnWidth(dgv_mappings, "CreatedAt", 19);
        }

        private async System.Threading.Tasks.Task VerifySelectedAsync()
        {
            if (dgv_mappings.CurrentRow == null)
            {
                MessageBox.Show(this, "Selecteer eerst een mapping.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var id = (int)dgv_mappings.CurrentRow.Cells["Id"].Value;
            await _repository.UpdateConfidenceAsync(id, MatchConfidence.Verified);
            await RefreshAsync();
        }
    }
}
