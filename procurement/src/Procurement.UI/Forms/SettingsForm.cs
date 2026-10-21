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
            Text = "Settings";
            Width = 900;
            Height = 600;
            // CenterScreen, not CenterParent — shown owner-less via a plain Show() (independent
            // top-level window, user request, 20 okt 2026), so there's no Owner to center against.
            // StartPosition is moot for an MDI child (ShellForm.ShowSettings() sets MdiParent, user
            // request, 20 okt 2026 — "MDI-container?") — WinForms always places MDI children with its
            // own cascading layout regardless of this setting; left as CenterScreen only because
            // nothing depends on changing it. (Inner dialogs opened from within this form —
            // credential/contact editors, etc. — stay CenterParent, since those are genuinely modal
            // dialogs, not MDI children themselves.)
            StartPosition = FormStartPosition.CenterScreen;
            // Pale blue, matching MAX's own "MRP Detail" screenshot (user request, 20 okt 2026) — see
            // GridFormatting.ClassicScreenBackColor's own comment for why this replaced an earlier,
            // visually-no-op SystemColors.Control attempt.
            BackColor = ClassicScreenBackColor;

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
            EnableRowNumberHeaders(_suppliersGrid);
            ApplyClassicHeaderStyle(_suppliersGrid);
            var hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(4),
                Text = "VendorId: MAX Part_Vendor.VENID_07 for this supplier (e.g. Farnell = 0349, DigiKey = 10194) — used to translate the Part_Vendor cross-reference table into recognized supplier mappings."
            };
            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
            var saveButton = new Button { Text = "Save", AutoSize = true };
            saveButton.Click += async (s, e) => await SaveSuppliersAsync();
            var credentialsButton = new Button { Text = "Edit Credentials...", AutoSize = true };
            credentialsButton.Click += async (s, e) => await EditCredentialsForSelectedSupplierAsync();
            var shippingButton = new Button { Text = "Edit Shipping Details...", AutoSize = true };
            shippingButton.Click += async (s, e) => await EditOrderingContactForSelectedSupplierAsync();
            var authorizeOrderingButton = new Button { Text = "Authorize Ordering...", AutoSize = true };
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
            EnableRowNumberHeaders(_supplierPreferenceGrid);
            ApplyClassicHeaderStyle(_supplierPreferenceGrid);
            var saveButton = new Button { Text = "Save", Dock = DockStyle.Bottom, AutoSize = true };
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
            EnableRowNumberHeaders(_packagingPolicyGrid);
            ApplyClassicHeaderStyle(_packagingPolicyGrid);
            var hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(4),
                Text = "AllowedPackaging: comma-separated list of enum names, e.g. OriginalReel,CutTape"
            };
            var saveButton = new Button { Text = "Save", Dock = DockStyle.Bottom, AutoSize = true };
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

            AddRow(layout, "Max order value for auto-approval:", _maxOrderValueBox);
            AddRow(layout, "Max price variance (%):", _maxPriceVarianceBox);
            AddRow(layout, "External supplier allowed:", _allowExternalSupplierBox);
            AddRow(layout, "Non-original packaging allowed:", _allowNonOriginalPackagingBox);
            AddRow(layout, "Alternative part allowed:", _allowAlternativePartBox);

            var saveButton = new Button { Text = "Save", Dock = DockStyle.Bottom, AutoSize = true };
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
            EnableRowNumberHeaders(_capabilitiesGrid);
            ApplyClassicHeaderStyle(_capabilitiesGrid);
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
            // Explicit widths (user request, 20 okt 2026) — Name is the one genuinely free-text
            // column here, left in Fill with a floor.
            SetCharacterBasedColumnWidth(_suppliersGrid, "Id", 6);
            SetCharacterBasedColumnWidth(_suppliersGrid, "SupplierCode", 14);
            SetMinimumWidth(_suppliersGrid, "Name", 140);
            SetCharacterBasedColumnWidth(_suppliersGrid, "VendorId", 10);
            SetCharacterBasedColumnWidth(_suppliersGrid, "IsSandbox", 10);
            SetCharacterBasedColumnWidth(_suppliersGrid, "UseMockData", 12);

            var preferences = await _policyRepository.GetSupplierPreferencesAsync();
            _supplierPreferenceGrid.DataSource = new BindingList<SupplierPreference>(preferences.ToList());
            if (_supplierPreferenceGrid.Columns["Id"] != null)
                _supplierPreferenceGrid.Columns["Id"].ReadOnly = true;
            SetCharacterBasedColumnWidth(_supplierPreferenceGrid, "Id", 6);
            SetCharacterBasedColumnWidth(_supplierPreferenceGrid, "SupplierCode", 14);
            SetCharacterBasedColumnWidth(_supplierPreferenceGrid, "Priority", 10);
            SetCharacterBasedColumnWidth(_supplierPreferenceGrid, "Active", 10);
            SetCharacterBasedColumnWidth(_supplierPreferenceGrid, "AllowedForAutoOrder", 14);

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
            // AllowedPackagingCsv is the one genuinely variable-length column here (a comma-separated
            // list), left in Fill with a floor.
            SetCharacterBasedColumnWidth(_packagingPolicyGrid, "Id", 6);
            SetMinimumWidth(_packagingPolicyGrid, "ComponentCategory", 140);
            SetCharacterBasedColumnWidth(_packagingPolicyGrid, "MinimumQuantity", 12);
            SetCharacterBasedColumnWidth(_packagingPolicyGrid, "PreferredPackaging", 16);
            SetMinimumWidth(_packagingPolicyGrid, "AllowedPackagingCsv", 160);
            SetCharacterBasedColumnWidth(_packagingPolicyGrid, "OriginalReelRequired", 14);

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
            SetCharacterBasedColumnWidth(_capabilitiesGrid, "Supplier", 14);
            SetCharacterBasedColumnWidth(_capabilitiesGrid, "Capability", 16);
            SetCharacterBasedColumnWidth(_capabilitiesGrid, "Status", 16);
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
                Text = $"Edit Credentials – {supplierCode}",
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
                    Text = "Leave blank = unchanged. Existing values are never shown here."
                };

                var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
                var saveButton = new Button { Text = "Save", AutoSize = true, DialogResult = DialogResult.OK };
                var cancelButton = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
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
                Text = $"Edit Shipping Details – {supplierCode}",
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
                AddRow(layout, "Contact Name:", contactNameBox);
                AddRow(layout, "Contact Email:", contactEmailBox);
                AddRow(layout, "Contact Phone:", contactTelephoneBox);
                AddRow(layout, "Address Line 1:", addressLine1Box);
                AddRow(layout, "Address Line 2:", addressLine2Box);
                AddRow(layout, "City:", cityBox);
                AddRow(layout, "Province/State:", provinceBox);
                AddRow(layout, "Postal Code:", postalCodeBox);
                AddRow(layout, "Country Code (2 letters, ISO):", countryCodeBox);

                var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
                var saveButton = new Button { Text = "Save", AutoSize = true, DialogResult = DialogResult.OK };
                var cancelButton = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
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

            var authorizer = new DigiKeyOrderingAuthorizer();
            var state = Guid.NewGuid().ToString("N");
            string authorizeUrl;
            try
            {
                authorizeUrl = authorizer.BuildAuthorizeUrl(options, state);
            }
            catch (SupplierException ex)
            {
                MessageBox.Show(this, ex.Message, "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            authorizer.OpenBrowser(authorizeUrl);

            var pasted = ShowPasteRedirectDialog();
            if (pasted == null) return; // cancelled

            try
            {
                var code = DigiKeyOrderingAuthorizer.ExtractAuthorizationCode(pasted, state);
                var refreshToken = await authorizer.ExchangeAuthorizationCodeAsync(options, code);
                await _supplierRepository.UpdateRefreshTokenAsync(supplierCode, refreshToken);
                MessageBox.Show(this, "DigiKey Ordering is geautoriseerd. Herstart de applicatie om de nieuwe autorisatie te gebruiken.", "Gelukt", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (SupplierException ex)
            {
                MessageBox.Show(this, ex.Message, "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// DigiKey rejects any "http://" Callback URL, so this app registers/uses the literal
        /// "https://localhost" value it documents for apps without a real callback endpoint — nothing
        /// ever listens there, so after consent the browser simply fails to load
        /// "https://localhost/?code=...&amp;state=..." (no server at that address). The address bar
        /// still shows that attempted URL though (including the query string), so the user copies it
        /// from there and pastes it into this dialog instead of an automatic redirect being caught.
        /// Returns null if the user cancelled.
        /// </summary>
        private string ShowPasteRedirectDialog()
        {
            using (var dialog = new Form
            {
                Text = "Authorize DigiKey Ordering",
                Width = 520,
                Height = 270,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false
            })
            {
                var hint = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 90,
                    Padding = new Padding(10),
                    Text = "A browser window has just opened to log in to DigiKey and give consent. "
                        + "After approving, DigiKey redirects the browser to \"https://localhost/...\" — that page won't "
                        + "load (expected, nothing runs there), but the address bar will contain the full URL. "
                        + "Copy that complete URL from the address bar and paste it below."
                };
                var textBox = new TextBox { Dock = DockStyle.Top, Height = 60 };

                var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
                var okButton = new Button { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK };
                var cancelButton = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
                buttonPanel.Controls.Add(cancelButton);
                buttonPanel.Controls.Add(okButton);

                dialog.Controls.Add(textBox);
                dialog.Controls.Add(hint);
                dialog.Controls.Add(buttonPanel);
                dialog.AcceptButton = okButton;
                dialog.CancelButton = cancelButton;

                return dialog.ShowDialog(this) == DialogResult.OK ? textBox.Text : null;
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
