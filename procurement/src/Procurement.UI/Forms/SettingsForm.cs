using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Entities;
using Procurement.Data.Repositories;
using static Procurement.UI.Support.GridFormatting;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Instellingen (spec §8.6): SupplierPreference, PackagingPolicy en ApprovalPolicy zijn hier
    /// als data te beheren in plaats van hard-coded in de code, plus een read-only weergave van
    /// de supplier capability-matrix (§5).
    /// </summary>
    public class SettingsForm : Form
    {
        private readonly PolicyRepository _policyRepository;
        private readonly SupplierRepository _supplierRepository;

        private DataGridView _supplierPreferenceGrid;
        private DataGridView _packagingPolicyGrid;
        private DataGridView _capabilitiesGrid;
        private DataGridView _suppliersGrid;

        private NumericUpDown _maxOrderValueBox;
        private NumericUpDown _maxPriceVarianceBox;
        private CheckBox _allowExternalSupplierBox;
        private CheckBox _allowNonOriginalPackagingBox;
        private CheckBox _allowAlternativePartBox;
        private ApprovalPolicy _approvalPolicy;

        public SettingsForm(PolicyRepository policyRepository, SupplierRepository supplierRepository)
        {
            _policyRepository = policyRepository ?? throw new ArgumentNullException(nameof(policyRepository));
            _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
            InitializeComponent();
            Load += async (s, e) => await RefreshAllAsync();
        }

        private void InitializeComponent()
        {
            Text = "Instellingen";
            Width = 900;
            Height = 600;
            StartPosition = FormStartPosition.CenterParent;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildSuppliersTab());
            tabs.TabPages.Add(BuildSupplierPreferenceTab());
            tabs.TabPages.Add(BuildPackagingPolicyTab());
            tabs.TabPages.Add(BuildApprovalPolicyTab());
            tabs.TabPages.Add(BuildCapabilitiesTab());

            Controls.Add(tabs);
        }

        private TabPage BuildSuppliersTab()
        {
            var page = new TabPage("Suppliers");
            _suppliersGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false
            };
            EnableDoubleBuffering(_suppliersGrid);
            var hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(4),
                Text = "VendorId: MAX Part_Vendor.VENID_07 voor deze leverancier (bv. Farnell = 0349, DigiKey = 10194) — gebruikt om de Part_Vendor-koppeltabel te vertalen naar herkende supplier-mappings."
            };
            var saveButton = new Button { Text = "Opslaan", Dock = DockStyle.Bottom, AutoSize = true };
            saveButton.Click += async (s, e) => await SaveSuppliersAsync();

            page.Controls.Add(_suppliersGrid);
            page.Controls.Add(hint);
            page.Controls.Add(saveButton);
            return page;
        }

        private TabPage BuildSupplierPreferenceTab()
        {
            var page = new TabPage("Supplier preferences");
            _supplierPreferenceGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = true
            };
            EnableDoubleBuffering(_supplierPreferenceGrid);
            var saveButton = new Button { Text = "Opslaan", Dock = DockStyle.Bottom, AutoSize = true };
            saveButton.Click += async (s, e) => await SaveSupplierPreferencesAsync();

            page.Controls.Add(_supplierPreferenceGrid);
            page.Controls.Add(saveButton);
            return page;
        }

        private TabPage BuildPackagingPolicyTab()
        {
            var page = new TabPage("Packaging policy");
            _packagingPolicyGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = true
            };
            EnableDoubleBuffering(_packagingPolicyGrid);
            var hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(4),
                Text = "AllowedPackaging: komma-gescheiden lijst van enum-namen, bv. OriginalReel,CutTape"
            };
            var saveButton = new Button { Text = "Opslaan", Dock = DockStyle.Bottom, AutoSize = true };
            saveButton.Click += async (s, e) => await SavePackagingPoliciesAsync();

            page.Controls.Add(_packagingPolicyGrid);
            page.Controls.Add(hint);
            page.Controls.Add(saveButton);
            return page;
        }

        private TabPage BuildApprovalPolicyTab()
        {
            var page = new TabPage("Approval policy");
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Padding = new Padding(10) };

            _maxOrderValueBox = new NumericUpDown { Maximum = 1000000, DecimalPlaces = 2, Width = 150 };
            _maxPriceVarianceBox = new NumericUpDown { Maximum = 100, DecimalPlaces = 1, Width = 150 };
            _allowExternalSupplierBox = new CheckBox();
            _allowNonOriginalPackagingBox = new CheckBox();
            _allowAlternativePartBox = new CheckBox();

            AddRow(layout, "Max orderwaarde voor auto-approval:", _maxOrderValueBox);
            AddRow(layout, "Max prijsafwijking (%):", _maxPriceVarianceBox);
            AddRow(layout, "Externe leverancier toegestaan:", _allowExternalSupplierBox);
            AddRow(layout, "Niet-originele packaging toegestaan:", _allowNonOriginalPackagingBox);
            AddRow(layout, "Alternatief onderdeel toegestaan:", _allowAlternativePartBox);

            var saveButton = new Button { Text = "Opslaan", Dock = DockStyle.Bottom, AutoSize = true };
            saveButton.Click += async (s, e) => await SaveApprovalPolicyAsync();

            page.Controls.Add(layout);
            page.Controls.Add(saveButton);
            return page;
        }

        private TabPage BuildCapabilitiesTab()
        {
            var page = new TabPage("Supplier capabilities");
            _capabilitiesGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            EnableDoubleBuffering(_capabilitiesGrid);
            page.Controls.Add(_capabilitiesGrid);
            return page;
        }

        private static void AddRow(TableLayoutPanel layout, string label, Control control)
        {
            var row = layout.RowCount;
            layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = label, AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Margin = new Padding(3, 8, 3, 3) }, 0, row);
            layout.Controls.Add(control, 1, row);
        }

        private async System.Threading.Tasks.Task RefreshAllAsync()
        {
            var suppliersForGrid = await _supplierRepository.GetAllAsync();
            _suppliersGrid.DataSource = new BindingList<Supplier>(suppliersForGrid.ToList());
            foreach (var column in new[] { "Id", "SupplierCode", "Name", "IsSandbox", "UseMockData" })
                if (_suppliersGrid.Columns[column] != null)
                    _suppliersGrid.Columns[column].ReadOnly = true;
            if (_suppliersGrid.Columns["Capabilities"] != null)
                _suppliersGrid.Columns["Capabilities"].Visible = false;

            var preferences = await _policyRepository.GetSupplierPreferencesAsync();
            _supplierPreferenceGrid.DataSource = new BindingList<SupplierPreference>(preferences.ToList());
            if (_supplierPreferenceGrid.Columns["Id"] != null)
                _supplierPreferenceGrid.Columns["Id"].ReadOnly = true;

            var packagingPolicies = await _policyRepository.GetPackagingPoliciesAsync();
            _packagingPolicyGrid.DataSource = new BindingList<PackagingPolicy>(packagingPolicies.ToList());
            if (_packagingPolicyGrid.Columns["Id"] != null)
                _packagingPolicyGrid.Columns["Id"].ReadOnly = true;
            // The NotMapped List<PackagingType> convenience property also gets auto-bound; only
            // the underlying CSV string column is meant to be edited here.
            if (_packagingPolicyGrid.Columns["AllowedPackaging"] != null)
                _packagingPolicyGrid.Columns["AllowedPackaging"].Visible = false;
            if (_packagingPolicyGrid.Columns["AllowedPackagingCsv"] != null)
                _packagingPolicyGrid.Columns["AllowedPackagingCsv"].HeaderText = "AllowedPackaging (csv)";
            ApplyQuantityColumns(_packagingPolicyGrid, "MinimumQuantity");

            _approvalPolicy = await _policyRepository.GetApprovalPolicyAsync() ?? new ApprovalPolicy();
            _maxOrderValueBox.Value = _approvalPolicy.MaxOrderValueForAutoApproval;
            _maxPriceVarianceBox.Value = _approvalPolicy.MaxPriceVariancePercentage;
            _allowExternalSupplierBox.Checked = _approvalPolicy.AllowExternalSupplier;
            _allowNonOriginalPackagingBox.Checked = _approvalPolicy.AllowNonOriginalPackaging;
            _allowAlternativePartBox.Checked = _approvalPolicy.AllowAlternativePart;

            var suppliers = await _supplierRepository.GetAllAsync();
            _capabilitiesGrid.DataSource = suppliers
                .SelectMany(s => s.Capabilities.Select(c => new
                {
                    Supplier = s.SupplierCode,
                    c.Capability,
                    Status = c.Status.ToString()
                }))
                .OrderBy(x => x.Supplier).ThenBy(x => x.Capability)
                .ToList();
        }

        private async System.Threading.Tasks.Task SaveSuppliersAsync()
        {
            var items = (BindingList<Supplier>)_suppliersGrid.DataSource;
            foreach (var item in items)
                await _supplierRepository.UpdateVendorIdAsync(item.Id, item.VendorId);

            await RefreshAllAsync();
            MessageBox.Show(this, "Suppliers opgeslagen.", "Opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async System.Threading.Tasks.Task SaveSupplierPreferencesAsync()
        {
            var items = ((BindingList<SupplierPreference>)_supplierPreferenceGrid.DataSource)
                .Where(p => !string.IsNullOrWhiteSpace(p.SupplierCode)).ToList();
            foreach (var item in items)
                await _policyRepository.SaveSupplierPreferenceAsync(item);

            await RefreshAllAsync();
            MessageBox.Show(this, "Supplier preferences opgeslagen.", "Opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async System.Threading.Tasks.Task SavePackagingPoliciesAsync()
        {
            var items = ((BindingList<PackagingPolicy>)_packagingPolicyGrid.DataSource)
                .Where(p => !string.IsNullOrWhiteSpace(p.ComponentCategory)).ToList();
            foreach (var item in items)
                await _policyRepository.SavePackagingPolicyAsync(item);

            await RefreshAllAsync();
            MessageBox.Show(this, "Packaging policies opgeslagen.", "Opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async System.Threading.Tasks.Task SaveApprovalPolicyAsync()
        {
            _approvalPolicy.MaxOrderValueForAutoApproval = _maxOrderValueBox.Value;
            _approvalPolicy.MaxPriceVariancePercentage = _maxPriceVarianceBox.Value;
            _approvalPolicy.AllowExternalSupplier = _allowExternalSupplierBox.Checked;
            _approvalPolicy.AllowNonOriginalPackaging = _allowNonOriginalPackagingBox.Checked;
            _approvalPolicy.AllowAlternativePart = _allowAlternativePartBox.Checked;

            await _policyRepository.SaveApprovalPolicyAsync(_approvalPolicy);
            MessageBox.Show(this, "Approval policy opgeslagen.", "Opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
