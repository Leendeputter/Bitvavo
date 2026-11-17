using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
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
            foreach (var grid in new[] { dgv_suppliers, dgv_supplierPreferences, dgv_packagingPolicies, dgv_attritionRules, dgv_capabilities })
                EnableRowNumberHeaders(grid);
            GridColumnSettingsHelper.Attach(this); // remembers column widths per user, like UniPro
            // Unknown (Any) isn't something to allow or forbid, and EitherReel is just OriginalReel + ReReel.
            foreach (var type in SelectablePackagingTypes)
            {
                lst_allowedPackaging.Items.Add(type);
                lst_exceptionPackaging.Items.Add(type);
            }
            Load += SettingsForm_Load;
        }

        private async void SettingsForm_Load(object sender, EventArgs e) => await RefreshAllAsync();

        private async void bt_saveSuppliers_Click(object sender, EventArgs e) => await SaveSuppliersAsync();

        private async void bt_editShipping_Click(object sender, EventArgs e) => await EditOrderingContactForSelectedSupplierAsync();

        private async void bt_authorizeOrdering_Click(object sender, EventArgs e) => await AuthorizeDigiKeyOrderingAsync();

        private async void bt_savePreferences_Click(object sender, EventArgs e) => await SaveSupplierPreferencesAsync();

        private async void bt_savePackaging_Click(object sender, EventArgs e) => await SavePackagingPoliciesAsync();

        private async void bt_saveApproval_Click(object sender, EventArgs e) => await SaveApprovalPolicyAsync();

        private async void bt_saveAttrition_Click(object sender, EventArgs e) => await SaveAttritionRulesAsync();

        private static readonly PackagingType[] SelectablePackagingTypes =
        {
            PackagingType.OriginalReel, PackagingType.ReReel, PackagingType.CutTape,
            PackagingType.Tray, PackagingType.Tube, PackagingType.Bulk
        };

        // True while the two packaging lists are being filled from the selected policy, so their
        // ItemCheck events don't write that same state back.
        private bool _loadingPackagingLists;

        private PackagingPolicy SelectedPackagingPolicy => dgv_packagingPolicies.CurrentRow?.DataBoundItem as PackagingPolicy;

        private void dgv_packagingPolicies_SelectionChanged(object sender, EventArgs e) => LoadPackagingLists();

        private void LoadPackagingLists()
        {
            var policy = SelectedPackagingPolicy;
            _loadingPackagingLists = true;
            try
            {
                gb_packaging.Enabled = policy != null;
                FillChecks(lst_allowedPackaging, Expand(policy?.AllowedPackaging));
                FillChecks(lst_exceptionPackaging, Expand(policy?.ExceptionPackaging));
                gb_packaging.Text = policy == null
                    ? "Verpakkingen van geselecteerde regel"
                    : $"Verpakkingen: {policy.ComponentCategory}";
            }
            finally
            {
                _loadingPackagingLists = false;
            }
        }

        private static HashSet<PackagingType> Expand(IEnumerable<PackagingType> types)
        {
            var set = new HashSet<PackagingType>(types ?? Enumerable.Empty<PackagingType>());
            if (set.Remove(PackagingType.EitherReel))
            {
                set.Add(PackagingType.OriginalReel);
                set.Add(PackagingType.ReReel);
            }
            return set;
        }

        private static void FillChecks(CheckedListBox list, HashSet<PackagingType> checkedTypes)
        {
            for (var i = 0; i < list.Items.Count; i++)
                list.SetItemChecked(i, checkedTypes.Contains((PackagingType)list.Items[i]));
        }

        private void lst_allowedPackaging_ItemCheck(object sender, ItemCheckEventArgs e) =>
            ApplyPackagingCheck(lst_allowedPackaging, e, (policy, types) => policy.AllowedPackaging = types);

        private void lst_exceptionPackaging_ItemCheck(object sender, ItemCheckEventArgs e) =>
            ApplyPackagingCheck(lst_exceptionPackaging, e, (policy, types) => policy.ExceptionPackaging = types);

        // ItemCheck fires before the item's state changes, so the new state comes from e.NewValue.
        private void ApplyPackagingCheck(CheckedListBox list, ItemCheckEventArgs e, Action<PackagingPolicy, List<PackagingType>> assign)
        {
            if (_loadingPackagingLists) return;
            var policy = SelectedPackagingPolicy;
            if (policy == null) return;

            var types = new List<PackagingType>();
            for (var i = 0; i < list.Items.Count; i++)
            {
                var isChecked = i == e.Index ? e.NewValue == CheckState.Checked : list.GetItemChecked(i);
                if (isChecked) types.Add((PackagingType)list.Items[i]);
            }
            assign(policy, types);
        }

        private async System.Threading.Tasks.Task RefreshAllAsync()
        {
            var suppliersForGrid = await _supplierRepository.GetAllAsync();
            BindSortable(dgv_suppliers, suppliersForGrid);
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
            BindSortable(dgv_supplierPreferences, preferences);
            if (dgv_supplierPreferences.Columns["Id"] != null)
                dgv_supplierPreferences.Columns["Id"].ReadOnly = true;
            SetCharacterBasedColumnWidth(dgv_supplierPreferences, "Id", 6);
            SetCharacterBasedColumnWidth(dgv_supplierPreferences, "SupplierCode", 14);
            SetCharacterBasedColumnWidth(dgv_supplierPreferences, "Priority", 10);
            SetCharacterBasedColumnWidth(dgv_supplierPreferences, "Active", 10);
            SetCharacterBasedColumnWidth(dgv_supplierPreferences, "AllowedForAutoOrder", 14);

            var packagingPolicies = await _policyRepository.GetPackagingPoliciesAsync();
            BindSortable(dgv_packagingPolicies, packagingPolicies);
            if (dgv_packagingPolicies.Columns["Id"] != null)
                dgv_packagingPolicies.Columns["Id"].ReadOnly = true;
            // The packaging lists are edited with the two checklists next to the grid (17 nov 2026),
            // not as csv text; PreferredPackaging no longer does anything (cost decides).
            foreach (var column in new[] { "AllowedPackaging", "AllowedPackagingCsv", "ExceptionPackaging", "ExceptionPackagingCsv", "PreferredPackaging" })
                if (dgv_packagingPolicies.Columns[column] != null)
                    dgv_packagingPolicies.Columns[column].Visible = false;
            SetHeaderText(dgv_packagingPolicies, "ComponentCategory", "Category / soort");
            SetHeaderText(dgv_packagingPolicies, "ExceptionPriceThreshold", "Prijsdrempel (€)");
            SetHeaderText(dgv_packagingPolicies, "SurplusPenaltyFactor", "Factor overschot");
            SetHeaderText(dgv_packagingPolicies, "OriginalReelRequired", "Originele reel verplicht");
            ApplyNullableDecimalColumns(dgv_packagingPolicies, "ExceptionPriceThreshold");
            SetCharacterBasedColumnWidth(dgv_packagingPolicies, "Id", 6);
            SetCharacterBasedColumnWidth(dgv_packagingPolicies, "ComponentCategory", 26);
            SetCharacterBasedColumnWidth(dgv_packagingPolicies, "ExceptionPriceThreshold", 14);
            SetCharacterBasedColumnWidth(dgv_packagingPolicies, "SurplusPenaltyFactor", 14);
            SetCharacterBasedColumnWidth(dgv_packagingPolicies, "OriginalReelRequired", 20);
            LoadPackagingLists();

            var attritionRules = await _policyRepository.GetAttritionRulesAsync();
            BindSortable(dgv_attritionRules, attritionRules);
            if (dgv_attritionRules.Columns["Id"] != null)
                dgv_attritionRules.Columns["Id"].Visible = false;
            SetHeaderText(dgv_attritionRules, "AppliesTo", "Geldt voor (Category of SMT/THT)");
            SetHeaderText(dgv_attritionRules, "MinUnitPrice", "Prijs vanaf (€)");
            SetHeaderText(dgv_attritionRules, "MaxUnitPrice", "Prijs tot (€)");
            SetHeaderText(dgv_attritionRules, "Percentage", "Percentage");
            SetHeaderText(dgv_attritionRules, "MinimumExtra", "Minimum extra (st)");
            ApplyNullableDecimalColumns(dgv_attritionRules, "MinUnitPrice", "MaxUnitPrice");
            SetCharacterBasedColumnWidth(dgv_attritionRules, "AppliesTo", 32);
            SetCharacterBasedColumnWidth(dgv_attritionRules, "MinUnitPrice", 14);
            SetCharacterBasedColumnWidth(dgv_attritionRules, "MaxUnitPrice", 14);
            SetCharacterBasedColumnWidth(dgv_attritionRules, "Percentage", 12);
            SetCharacterBasedColumnWidth(dgv_attritionRules, "MinimumExtra", 16);

            _approvalPolicy = await _policyRepository.GetApprovalPolicyAsync() ?? new ApprovalPolicy();
            nud_maxOrderValue.Value = _approvalPolicy.MaxOrderValueForAutoApproval;
            nud_maxPriceVariance.Value = _approvalPolicy.MaxPriceVariancePercentage;
            chk_allowExternalSupplier.Checked = _approvalPolicy.AllowExternalSupplier;
            chk_allowNonOriginalPackaging.Checked = _approvalPolicy.AllowNonOriginalPackaging;
            chk_allowAlternativePart.Checked = _approvalPolicy.AllowAlternativePart;

            var suppliers = await _supplierRepository.GetAllAsync();
            BindSortable(dgv_capabilities, suppliers
                .SelectMany(s => s.Capabilities.Select(c => new
                {
                    Supplier = s.SupplierCode,
                    c.Capability,
                    Status = c.Status.ToString()
                }))
                .OrderBy(x => x.Supplier).ThenBy(x => x.Capability)
                );
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
            dgv_packagingPolicies.EndEdit();
            var items = ((BindingList<PackagingPolicy>)dgv_packagingPolicies.DataSource)
                .Where(p => !string.IsNullOrWhiteSpace(p.ComponentCategory)).ToList();
            foreach (var item in items)
                await _policyRepository.SavePackagingPolicyAsync(item);

            await RefreshAllAsync();
            MessageBox.Show(this, "Packaging policies opgeslagen.", "Opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async System.Threading.Tasks.Task SaveAttritionRulesAsync()
        {
            dgv_attritionRules.EndEdit();
            var items = ((BindingList<AttritionRule>)dgv_attritionRules.DataSource)
                .Where(r => !string.IsNullOrWhiteSpace(r.AppliesTo)).ToList();
            foreach (var item in items)
                item.AppliesTo = item.AppliesTo.Trim();

            await _policyRepository.SaveAttritionRulesAsync(items);

            await RefreshAllAsync();
            MessageBox.Show(this, "Extra voor uitval opgeslagen.", "Opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>An empty cell in a nullable decimal column means "no value" (null), not an input error.</summary>
        private static void ApplyNullableDecimalColumns(DataGridView grid, params string[] columnNames)
        {
            foreach (var name in columnNames)
            {
                var column = grid.Columns[name];
                if (column == null) continue;
                column.DefaultCellStyle.NullValue = string.Empty;
                column.DefaultCellStyle.DataSourceNullValue = null;
            }
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
