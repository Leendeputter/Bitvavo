using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Entities;
using Procurement.Core.Exceptions;
using Procurement.Data.Repositories;
using Procurement.Suppliers.DigiKey;
using Procurement.Suppliers.DigiKey.Http;
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
            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
            var saveButton = new Button { Text = "Opslaan", AutoSize = true };
            saveButton.Click += async (s, e) => await SaveSuppliersAsync();
            var credentialsButton = new Button { Text = "Credentials bewerken...", AutoSize = true };
            credentialsButton.Click += async (s, e) => await EditCredentialsForSelectedSupplierAsync();
            var shippingButton = new Button { Text = "Verzendgegevens bewerken...", AutoSize = true };
            shippingButton.Click += async (s, e) => await EditOrderingContactForSelectedSupplierAsync();
            var authorizeOrderingButton = new Button { Text = "Ordering autoriseren...", AutoSize = true };
            authorizeOrderingButton.Click += async (s, e) => await AuthorizeDigiKeyOrderingAsync();
            buttonPanel.Controls.Add(saveButton);
            buttonPanel.Controls.Add(credentialsButton);
            buttonPanel.Controls.Add(shippingButton);
            buttonPanel.Controls.Add(authorizeOrderingButton);

            page.Controls.Add(_suppliersGrid);
            page.Controls.Add(hint);
            page.Controls.Add(buttonPanel);
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
            foreach (var column in new[] { "Id", "SupplierCode", "Name" })
                if (_suppliersGrid.Columns[column] != null)
                    _suppliersGrid.Columns[column].ReadOnly = true;
            // IsSandbox/UseMockData are directly editable here (saved by SaveSuppliersAsync below).
            // Credentials never appear in this grid at all, encrypted or not — a raw ciphertext
            // column would be noise, and a decrypted one would put a secret in plain view on
            // screen; both are edited exclusively through the write-only "Credentials bewerken"
            // dialog instead (EditCredentialsForSelectedSupplier). The account/shipping-contact
            // fields aren't secret, but ten extra columns here would be pure clutter for something
            // edited a handful of times ever — same reasoning, different (read/write, pre-filled)
            // dialog: "Verzendgegevens bewerken" (EditOrderingContactForSelectedSupplierAsync).
            foreach (var column in new[]
            {
                "Capabilities", "ClientIdEncrypted", "ClientSecretEncrypted", "ApiKeyEncrypted", "ClientId", "ClientSecret", "ApiKey",
                "AccountId", "ContactName", "ContactEmail", "ContactTelephone", "AddressLine1", "AddressLine2", "City", "Province", "PostalCode", "CountryCode"
            })
                if (_suppliersGrid.Columns[column] != null)
                    _suppliersGrid.Columns[column].Visible = false;

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
                await _supplierRepository.UpdateSettingsAsync(item.Id, item.VendorId, item.IsSandbox, item.UseMockData);

            await RefreshAllAsync();
            MessageBox.Show(this, "Suppliers opgeslagen.", "Opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Credentials worden bewust nooit in de suppliers-grid getoond (versleuteld of niet) —
        /// dit losse, write-only dialoogvenster is de enige plek waar ze bewerkt worden. Velden
        /// staan altijd leeg bij openen (een bestaande waarde wordt nooit teruggetoond, ook niet
        /// ontsleuteld) en leeg laten = ongewijzigd laten; alleen invullen overschrijft. Welke
        /// velden een supplier daadwerkelijk gebruikt verschilt (DigiKey: ClientId+ClientSecret;
        /// Farnell/Mouser: enkel ApiKey; TME: Token in ClientId, HMAC-secret in ClientSecret;
        /// overige distributeurs: nog geen bevestigde API, velden liggen klaar voor later) — het
        /// dialoogvenster toont voor elke supplier gewoon alle drie velden, niet-toepasselijke
        /// velden blijven dan leeg.
        /// </summary>
        private async System.Threading.Tasks.Task EditCredentialsForSelectedSupplierAsync()
        {
            if (_suppliersGrid.CurrentRow == null)
            {
                MessageBox.Show(this, "Selecteer eerst een supplier.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var supplierId = (int)_suppliersGrid.CurrentRow.Cells["Id"].Value;
            var supplierCode = (string)_suppliersGrid.CurrentRow.Cells["SupplierCode"].Value;

            using (var dialog = new Form
            {
                Text = $"Credentials bewerken – {supplierCode}",
                Width = 420,
                Height = 260,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false
            })
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Padding = new Padding(10) };
                var clientIdBox = new TextBox { Width = 220 };
                var clientSecretBox = new TextBox { Width = 220, PasswordChar = '●' };
                var apiKeyBox = new TextBox { Width = 220, PasswordChar = '●' };
                AddRow(layout, "Client Id / Token (DigiKey/TME):", clientIdBox);
                AddRow(layout, "Client Secret (DigiKey/TME):", clientSecretBox);
                AddRow(layout, "Api Key (Farnell/Mouser):", apiKeyBox);

                var hint = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 40,
                    Padding = new Padding(10, 0, 10, 0),
                    Text = "Leeg laten = ongewijzigd. Bestaande waarden worden hier nooit getoond."
                };

                var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
                var saveButton = new Button { Text = "Opslaan", AutoSize = true, DialogResult = DialogResult.OK };
                var cancelButton = new Button { Text = "Annuleren", AutoSize = true, DialogResult = DialogResult.Cancel };
                buttonPanel.Controls.Add(cancelButton);
                buttonPanel.Controls.Add(saveButton);

                dialog.Controls.Add(layout);
                dialog.Controls.Add(hint);
                dialog.Controls.Add(buttonPanel);
                dialog.AcceptButton = saveButton;
                dialog.CancelButton = cancelButton;

                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    await _supplierRepository.UpdateCredentialsAsync(supplierId, clientIdBox.Text, clientSecretBox.Text, apiKeyBox.Text);
                    MessageBox.Show(this, "Credentials opgeslagen.", "Opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Account/shipping-contact info a real Ordering API call needs (see Supplier.cs) — per
        /// supplier, since a different supplier's Ordering API could need a completely different
        /// account reference or ship-to address. Unlike "Credentials bewerken" this dialog does
        /// pre-fill the current values (nothing here is a secret) and always overwrites on save.
        /// </summary>
        private async System.Threading.Tasks.Task EditOrderingContactForSelectedSupplierAsync()
        {
            if (_suppliersGrid.CurrentRow == null)
            {
                MessageBox.Show(this, "Selecteer eerst een supplier.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var supplierId = (int)_suppliersGrid.CurrentRow.Cells["Id"].Value;
            var supplierCode = (string)_suppliersGrid.CurrentRow.Cells["SupplierCode"].Value;
            var current = await _supplierRepository.GetByCodeAsync(supplierCode);
            if (current == null) return;

            using (var dialog = new Form
            {
                Text = $"Verzendgegevens bewerken – {supplierCode}",
                Width = 440,
                Height = 460,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false
            })
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Padding = new Padding(10) };
                var accountIdBox = new TextBox { Width = 220, Text = current.AccountId };
                var contactNameBox = new TextBox { Width = 220, Text = current.ContactName };
                var contactEmailBox = new TextBox { Width = 220, Text = current.ContactEmail };
                var contactTelephoneBox = new TextBox { Width = 220, Text = current.ContactTelephone };
                var addressLine1Box = new TextBox { Width = 220, Text = current.AddressLine1 };
                var addressLine2Box = new TextBox { Width = 220, Text = current.AddressLine2 };
                var cityBox = new TextBox { Width = 220, Text = current.City };
                var provinceBox = new TextBox { Width = 220, Text = current.Province };
                var postalCodeBox = new TextBox { Width = 220, Text = current.PostalCode };
                var countryCodeBox = new TextBox { Width = 220, Text = current.CountryCode, MaxLength = 2 };
                AddRow(layout, "Account ID:", accountIdBox);
                AddRow(layout, "Contactnaam:", contactNameBox);
                AddRow(layout, "Contact e-mail:", contactEmailBox);
                AddRow(layout, "Contact telefoon:", contactTelephoneBox);
                AddRow(layout, "Adresregel 1:", addressLine1Box);
                AddRow(layout, "Adresregel 2:", addressLine2Box);
                AddRow(layout, "Plaats:", cityBox);
                AddRow(layout, "Provincie/staat:", provinceBox);
                AddRow(layout, "Postcode:", postalCodeBox);
                AddRow(layout, "Landcode (2 letters, ISO):", countryCodeBox);

                var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
                var saveButton = new Button { Text = "Opslaan", AutoSize = true, DialogResult = DialogResult.OK };
                var cancelButton = new Button { Text = "Annuleren", AutoSize = true, DialogResult = DialogResult.Cancel };
                buttonPanel.Controls.Add(cancelButton);
                buttonPanel.Controls.Add(saveButton);

                var scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
                scrollPanel.Controls.Add(layout);

                dialog.Controls.Add(scrollPanel);
                dialog.Controls.Add(buttonPanel);
                dialog.AcceptButton = saveButton;
                dialog.CancelButton = cancelButton;

                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    await _supplierRepository.UpdateOrderingContactAsync(
                        supplierId, accountIdBox.Text, contactNameBox.Text, contactEmailBox.Text, contactTelephoneBox.Text,
                        addressLine1Box.Text, addressLine2Box.Text, cityBox.Text, provinceBox.Text, postalCodeBox.Text, countryCodeBox.Text);
                    MessageBox.Show(this, "Verzendgegevens opgeslagen.", "Opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// One-time interactive 3-legged OAuth consent flow for DigiKey's Ordering v3 API (see
        /// DigiKeyOrderingAuthorizer) — only DigiKey needs this (Product Information's 2-legged
        /// client-credentials flow needs no interactive step at all), so this button is a no-op
        /// for any other selected supplier rather than something every supplier row supports.
        /// Persists the resulting refresh token via UpdateRefreshTokenAsync; CompositionRoot picks
        /// it up the next time the app starts (this dialog doesn't attempt to hot-swap the
        /// already-running DigiKeyHttpClientWrapper's in-memory options).
        /// </summary>
        private async System.Threading.Tasks.Task AuthorizeDigiKeyOrderingAsync()
        {
            if (_suppliersGrid.CurrentRow == null)
            {
                MessageBox.Show(this, "Selecteer eerst een supplier.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var supplierCode = (string)_suppliersGrid.CurrentRow.Cells["SupplierCode"].Value;
            if (!string.Equals(supplierCode, DigiKeyAdapter.Code, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "\"Ordering autoriseren\" is alleen van toepassing op DigiKey.", "Niet van toepassing", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var current = await _supplierRepository.GetByCodeAsync(supplierCode);
            if (current == null) return;

            var options = new DigiKeyOptions
            {
                ClientId = current.ClientId,
                ClientSecret = current.ClientSecret,
                IsSandbox = current.IsSandbox
            };

            MessageBox.Show(this,
                "Er wordt zo een browservenster geopend om in te loggen bij DigiKey en toestemming te geven. " +
                $"Zorg dat de Callback URL in het DigiKey-portal exact \"{DigiKeyOrderingAuthorizer.RedirectUri}\" is, anders mislukt dit.",
                "DigiKey Ordering autoriseren", MessageBoxButtons.OK, MessageBoxIcon.Information);

            try
            {
                var refreshToken = await new DigiKeyOrderingAuthorizer().AuthorizeAsync(options);
                await _supplierRepository.UpdateRefreshTokenAsync(supplierCode, refreshToken);
                MessageBox.Show(this, "DigiKey Ordering is geautoriseerd. Herstart de applicatie om de nieuwe autorisatie te gebruiken.", "Gelukt", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (SupplierException ex)
            {
                MessageBox.Show(this, ex.Message, "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
