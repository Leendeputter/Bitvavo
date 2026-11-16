using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Entities;
using Procurement.Core.Exceptions;
using Procurement.Data.Repositories;
using Procurement.Data.Secrets;
using Procurement.Suppliers.DigiKey;
using Procurement.Suppliers.DigiKey.Http;
using static Procurement.UI.Support.GridFormatting;
using Procurement.UI.Support;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Instellingen (spec §8.6): SupplierPreference, PackagingPolicy en ApprovalPolicy zijn hier
    /// als data te beheren in plaats van hard-coded in de code, plus een read-only weergave van
    /// de supplier capability-matrix (§5).
    /// Built with the Visual Studio Designer (user request, 16 nov 2026): the tabs, grids and
    /// buttons are in SettingsForm.Designer.cs; the two small dialogs are ShippingDetailsForm and
    /// DigiKeyAuthorizeForm.
    /// </summary>
    public partial class SettingsForm : Form
    {
        private readonly PolicyRepository _policyRepository;
        private readonly SupplierRepository _supplierRepository;

        private ApprovalPolicy _approvalPolicy;

        public SettingsForm(PolicyRepository policyRepository, SupplierRepository supplierRepository)
        {
            _policyRepository = policyRepository ?? throw new ArgumentNullException(nameof(policyRepository));
            _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
            InitializeComponent();
            ClassicStyle.ApplyClassicStyle(this);
            this.BackColor = ClassicStyle.ClassicScreenBackColor;
            foreach (var grid in new[] { dgv_suppliers, dgv_supplierPreferences, dgv_packagingPolicies, dgv_capabilities })
                EnableRowNumberHeaders(grid);
            GridColumnSettingsHelper.Attach(this); // remembers column widths per user, like UniPro
            Load += SettingsForm_Load;
        }

        private async void SettingsForm_Load(object sender, EventArgs e) => await RefreshAllAsync();

        private async void bt_saveSuppliers_Click(object sender, EventArgs e) => await SaveSuppliersAsync();

        private async void bt_editShipping_Click(object sender, EventArgs e) => await EditOrderingContactForSelectedSupplierAsync();

        private async void bt_authorizeOrdering_Click(object sender, EventArgs e) => await AuthorizeDigiKeyOrderingAsync();

        private async void bt_savePreferences_Click(object sender, EventArgs e) => await SaveSupplierPreferencesAsync();

        private async void bt_savePackaging_Click(object sender, EventArgs e) => await SavePackagingPoliciesAsync();

        private async void bt_saveApproval_Click(object sender, EventArgs e) => await SaveApprovalPolicyAsync();

        private async System.Threading.Tasks.Task RefreshAllAsync()
        {
            var suppliersForGrid = await _supplierRepository.GetAllAsync();
            dgv_suppliers.DataSource = new BindingList<Supplier>(suppliersForGrid.ToList());
            foreach (var column in new[] { "Id", "SupplierCode", "Name" })
                if (dgv_suppliers.Columns[column] != null)
                    dgv_suppliers.Columns[column].ReadOnly = true;
            // IsSandbox/UseMockData are directly editable here (saved by SaveSuppliersAsync below).
            // Credentials never appear in this grid at all: since 13 nov 2026 they live in the
            // SharedSecrets database and are edited only via Beheer > App Secrets (same as UniPro);
            // the legacy *Encrypted columns are hidden below. The account/shipping-contact
            // fields aren't secret, but ten extra columns here would be pure clutter for something
            // edited a handful of times ever — same reasoning, different (read/write, pre-filled)
            // dialog: "Verzendgegevens bewerken" (EditOrderingContactForSelectedSupplierAsync).
            foreach (var column in new[]
            {
                "Capabilities", "ClientIdEncrypted", "ClientSecretEncrypted", "ApiKeyEncrypted", "RefreshTokenEncrypted",
                "AccountId", "ContactName", "ContactEmail", "ContactTelephone", "AddressLine1", "AddressLine2", "City", "Province", "PostalCode", "CountryCode"
            })
                if (dgv_suppliers.Columns[column] != null)
                    dgv_suppliers.Columns[column].Visible = false;
            // Fixed widths, no Fill (user rule, 16 nov 2026: columns don't stretch when maximized).
            SetCharacterBasedColumnWidth(dgv_suppliers, "Id", 6);
            SetCharacterBasedColumnWidth(dgv_suppliers, "SupplierCode", 14);
            SetCharacterBasedColumnWidth(dgv_suppliers, "Name", 30);
            SetCharacterBasedColumnWidth(dgv_suppliers, "VendorId", 10);
            SetCharacterBasedColumnWidth(dgv_suppliers, "IsSandbox", 10);
            SetCharacterBasedColumnWidth(dgv_suppliers, "UseMockData", 12);

            var preferences = await _policyRepository.GetSupplierPreferencesAsync();
            dgv_supplierPreferences.DataSource = new BindingList<SupplierPreference>(preferences.ToList());
            if (dgv_supplierPreferences.Columns["Id"] != null)
                dgv_supplierPreferences.Columns["Id"].ReadOnly = true;
            SetCharacterBasedColumnWidth(dgv_supplierPreferences, "Id", 6);
            SetCharacterBasedColumnWidth(dgv_supplierPreferences, "SupplierCode", 14);
            SetCharacterBasedColumnWidth(dgv_supplierPreferences, "Priority", 10);
            SetCharacterBasedColumnWidth(dgv_supplierPreferences, "Active", 10);
            SetCharacterBasedColumnWidth(dgv_supplierPreferences, "AllowedForAutoOrder", 14);

            var packagingPolicies = await _policyRepository.GetPackagingPoliciesAsync();
            dgv_packagingPolicies.DataSource = new BindingList<PackagingPolicy>(packagingPolicies.ToList());
            if (dgv_packagingPolicies.Columns["Id"] != null)
                dgv_packagingPolicies.Columns["Id"].ReadOnly = true;
            // The NotMapped List<PackagingType> convenience property also gets auto-bound; only
            // the underlying CSV string column is meant to be edited here.
            if (dgv_packagingPolicies.Columns["AllowedPackaging"] != null)
                dgv_packagingPolicies.Columns["AllowedPackaging"].Visible = false;
            if (dgv_packagingPolicies.Columns["AllowedPackagingCsv"] != null)
                dgv_packagingPolicies.Columns["AllowedPackagingCsv"].HeaderText = "AllowedPackaging (csv)";
            ApplyQuantityColumns(dgv_packagingPolicies, "MinimumQuantity");
            SetCharacterBasedColumnWidth(dgv_packagingPolicies, "Id", 6);
            SetCharacterBasedColumnWidth(dgv_packagingPolicies, "ComponentCategory", 20);
            SetCharacterBasedColumnWidth(dgv_packagingPolicies, "MinimumQuantity", 12);
            SetCharacterBasedColumnWidth(dgv_packagingPolicies, "PreferredPackaging", 16);
            SetCharacterBasedColumnWidth(dgv_packagingPolicies, "AllowedPackagingCsv", 30);
            SetCharacterBasedColumnWidth(dgv_packagingPolicies, "OriginalReelRequired", 14);

            _approvalPolicy = await _policyRepository.GetApprovalPolicyAsync() ?? new ApprovalPolicy();
            nud_maxOrderValue.Value = _approvalPolicy.MaxOrderValueForAutoApproval;
            nud_maxPriceVariance.Value = _approvalPolicy.MaxPriceVariancePercentage;
            chk_allowExternalSupplier.Checked = _approvalPolicy.AllowExternalSupplier;
            chk_allowNonOriginalPackaging.Checked = _approvalPolicy.AllowNonOriginalPackaging;
            chk_allowAlternativePart.Checked = _approvalPolicy.AllowAlternativePart;

            var suppliers = await _supplierRepository.GetAllAsync();
            dgv_capabilities.DataSource = suppliers
                .SelectMany(s => s.Capabilities.Select(c => new
                {
                    Supplier = s.SupplierCode,
                    c.Capability,
                    Status = c.Status.ToString()
                }))
                .OrderBy(x => x.Supplier).ThenBy(x => x.Capability)
                .ToList();
            SetCharacterBasedColumnWidth(dgv_capabilities, "Supplier", 14);
            SetCharacterBasedColumnWidth(dgv_capabilities, "Capability", 16);
            SetCharacterBasedColumnWidth(dgv_capabilities, "Status", 16);
        }

        private async System.Threading.Tasks.Task SaveSuppliersAsync()
        {
            var items = (BindingList<Supplier>)dgv_suppliers.DataSource;
            foreach (var item in items)
                await _supplierRepository.UpdateSettingsAsync(item.Id, item.VendorId, item.IsSandbox, item.UseMockData);

            await RefreshAllAsync();
            MessageBox.Show(this, "Suppliers opgeslagen.", "Opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Account/shipping-contact info a real Ordering API call needs (see Supplier.cs) — per
        /// supplier, since a different supplier's Ordering API could need a completely different
        /// account reference or ship-to address. Unlike the secrets (Beheer > App Secrets) this dialog does
        /// pre-fill the current values (nothing here is a secret) and always overwrites on save.
        /// </summary>
        private async System.Threading.Tasks.Task EditOrderingContactForSelectedSupplierAsync()
        {
            if (dgv_suppliers.CurrentRow == null)
            {
                MessageBox.Show(this, "Selecteer eerst een supplier.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var supplierId = (int)dgv_suppliers.CurrentRow.Cells["Id"].Value;
            var supplierCode = (string)dgv_suppliers.CurrentRow.Cells["SupplierCode"].Value;
            var current = await _supplierRepository.GetByCodeAsync(supplierCode);
            if (current == null) return;

            using (var dialog = new ShippingDetailsForm(current))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    await _supplierRepository.UpdateOrderingContactAsync(
                        supplierId, dialog.AccountId, dialog.ContactName, dialog.ContactEmail, dialog.ContactTelephone,
                        dialog.AddressLine1, dialog.AddressLine2, dialog.City, dialog.Province, dialog.PostalCode, dialog.CountryCode);
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
        /// Persists the resulting refresh token in SharedSecrets (SecretSettings.SaveDigiKeyRefreshToken,
        /// secret "DigiKeyRefreshToken"); CompositionRoot picks
        /// it up the next time the app starts (this dialog doesn't attempt to hot-swap the
        /// already-running DigiKeyHttpClientWrapper's in-memory options).
        /// </summary>
        private async System.Threading.Tasks.Task AuthorizeDigiKeyOrderingAsync()
        {
            if (dgv_suppliers.CurrentRow == null)
            {
                MessageBox.Show(this, "Selecteer eerst een supplier.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var supplierCode = (string)dgv_suppliers.CurrentRow.Cells["SupplierCode"].Value;
            if (!string.Equals(supplierCode, DigiKeyAdapter.Code, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "\"Ordering autoriseren\" is alleen van toepassing op DigiKey.", "Niet van toepassing", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var current = await _supplierRepository.GetByCodeAsync(supplierCode);
            if (current == null) return;

            DigiKeyOptions options;
            try
            {
                options = new DigiKeyOptions
                {
                    ClientId = SecretSettings.DigiKeyClientId,
                    ClientSecret = SecretSettings.DigiKeyClientSecret,
                    IsSandbox = current.IsSandbox
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show(this, ex.Message, "Geen rechten", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

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
                SecretSettings.SaveDigiKeyRefreshToken(refreshToken);
                MessageBox.Show(this, "DigiKey Ordering is geautoriseerd. Herstart de applicatie om de nieuwe autorisatie te gebruiken.", "Gelukt", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (SupplierException ex)
            {
                MessageBox.Show(this, ex.Message, "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                // e.g. SharedSecrets unreachable or UNIPRO_SECRET_KEY not set while saving the token.
                MessageBox.Show(this, $"De autorisatie is gelukt, maar het refresh-token kon niet worden opgeslagen:\n\n{ex.Message}",
                    "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Asks for the pasted "https://localhost/?code=..." URL (see DigiKeyAuthorizeForm). Null if cancelled.</summary>
        private string ShowPasteRedirectDialog()
        {
            using (var dialog = new DigiKeyAuthorizeForm())
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.RedirectUrl : null;
        }

        private async System.Threading.Tasks.Task SaveSupplierPreferencesAsync()
        {
            var items = ((BindingList<SupplierPreference>)dgv_supplierPreferences.DataSource)
                .Where(p => !string.IsNullOrWhiteSpace(p.SupplierCode)).ToList();
            foreach (var item in items)
                await _policyRepository.SaveSupplierPreferenceAsync(item);

            await RefreshAllAsync();
            MessageBox.Show(this, "Supplier preferences opgeslagen.", "Opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async System.Threading.Tasks.Task SavePackagingPoliciesAsync()
        {
            var items = ((BindingList<PackagingPolicy>)dgv_packagingPolicies.DataSource)
                .Where(p => !string.IsNullOrWhiteSpace(p.ComponentCategory)).ToList();
            foreach (var item in items)
                await _policyRepository.SavePackagingPolicyAsync(item);

            await RefreshAllAsync();
            MessageBox.Show(this, "Packaging policies opgeslagen.", "Opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async System.Threading.Tasks.Task SaveApprovalPolicyAsync()
        {
            _approvalPolicy.MaxOrderValueForAutoApproval = nud_maxOrderValue.Value;
            _approvalPolicy.MaxPriceVariancePercentage = nud_maxPriceVariance.Value;
            _approvalPolicy.AllowExternalSupplier = chk_allowExternalSupplier.Checked;
            _approvalPolicy.AllowNonOriginalPackaging = chk_allowNonOriginalPackaging.Checked;
            _approvalPolicy.AllowAlternativePart = chk_allowAlternativePart.Checked;

            await _policyRepository.SaveApprovalPolicyAsync(_approvalPolicy);
            MessageBox.Show(this, "Approval policy opgeslagen.", "Opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
