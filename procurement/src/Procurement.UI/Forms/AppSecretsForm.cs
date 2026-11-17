using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Models;
using Procurement.Core.Session;
using Procurement.Data.Secrets;
using Procurement.UI.Support;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Beheer > App Secrets — port of UniPro2026's UI/Forms/AppSecretsForm.cs (user request,
    /// 13 nov 2026), same two halves and same rules:
    ///  - Left: one row per secret (label, "ingesteld"/"leeg" status, password box). Write-only: a
    ///    saved value is never shown again; an empty box leaves the stored value unchanged.
    ///  - Right: SecretAccess grid — which Windows user may let the app use which secret — plus
    ///    "copy all rights from user X to user(s) Y". Deleting a row revokes the grant immediately.
    /// Only reachable via the Beheer menu, which demands MGR User Security first (see ShellForm).
    ///
    /// First Procurement screen built with the Visual Studio Designer (AppSecretsForm.Designer.cs),
    /// same structure and control names as UniPro's version (user request, 16 nov 2026: "windows in
    /// Procurement via de designer opbouwen", so the merge with UniPro doesn't hit two styles).
    /// Like UniPro: styling via ClassicStyle.ApplyClassicStyle(this) right after
    /// InitializeComponent(); the secret rows and grid columns stay in code (BuildSecretRows/
    /// BuildAccessGrid), because they come from SecretSettings.Definitions. Unlike UniPro's fixed
    /// layout, everything is anchored inside a SplitContainer so the window can be resized (user
    /// feedback, 13 nov 2026).
    /// </summary>
    public partial class AppSecretsForm : Form
    {
        private readonly AppSecretsRepository _repo = new AppSecretsRepository();
        private readonly SecretAccessRepository _accessRepo = new SecretAccessRepository();
        private readonly Dictionary<string, TextBox> _inputsByName = new Dictionary<string, TextBox>();
        private readonly Dictionary<string, Label> _statusLabelsByName = new Dictionary<string, Label>();
        private BindingList<SecretAccessEntry> _accessEntries;

        public AppSecretsForm()
        {
            InitializeComponent();
            ClassicStyle.ApplyClassicStyle(this);
            this.BackColor = ClassicStyle.ClassicScreenBackColor;

            if (ProcurementSession.TestMode)
            {
                this.Text += " — TEST MODE";
                this.BackColor = Color.LightYellow;
            }

            BuildSecretRows();
            BuildAccessGrid();
            GridColumnSettingsHelper.Attach(this); // remembers column widths per user, like UniPro
            Load += AppSecretsForm_Load;
        }

        private void AppSecretsForm_Load(object sender, EventArgs e)
        {
            RunGuarded(() =>
            {
                RefreshStatuses();
                LoadAccessEntries();
            });
        }

        private void bt_save_Click(object sender, EventArgs e) => RunGuarded(SaveSecrets);

        private void bt_close_Click(object sender, EventArgs e) => Close();

        private void bt_copyAccess_Click(object sender, EventArgs e) => RunGuarded(CopyAccess);

        private void bt_saveAccess_Click(object sender, EventArgs e) => RunGuarded(SaveAccess);

        /// <summary>One row per secret (label, status, input). Non-editable secrets (DigiKey's refresh token) get a status but no input box.</summary>
        private void BuildSecretRows()
        {
            const int rowHeight = 32;
            const int labelWidth = 190;
            const int statusWidth = 70;
            const int inputWidth = 260;
            int y = 10;

            foreach (var definition in SecretSettings.Definitions)
            {
                pnl_secrets.Controls.Add(new Label
                {
                    Text = definition.Label,
                    Location = new Point(10, y + 4),
                    Size = new Size(labelWidth, 18),
                });

                var statusLabel = new Label
                {
                    Text = "...",
                    Location = new Point(10 + labelWidth, y + 4),
                    Size = new Size(statusWidth, 18),
                    ForeColor = Color.Gray,
                };
                pnl_secrets.Controls.Add(statusLabel);
                _statusLabelsByName[definition.Name] = statusLabel;

                if (definition.Editable)
                {
                    var input = new TextBox
                    {
                        Location = new Point(10 + labelWidth + statusWidth, y),
                        Size = new Size(inputWidth, 20),
                        UseSystemPasswordChar = true,
                    };
                    pnl_secrets.Controls.Add(input);
                    _inputsByName[definition.Name] = input;
                }
                else
                {
                    pnl_secrets.Controls.Add(new Label
                    {
                        Text = "set via Settings > Authorize Ordering...",
                        Location = new Point(10 + labelWidth + statusWidth, y + 4),
                        Size = new Size(inputWidth, 18),
                        ForeColor = Color.Gray,
                    });
                }

                y += rowHeight;
            }
        }

        /// <summary>Shows only whether a secret is set, never the value itself.</summary>
        private void RefreshStatuses()
        {
            var namesWithValue = new HashSet<string>(_repo.GetAllNames());

            foreach (var definition in SecretSettings.Definitions)
            {
                bool hasValue = namesWithValue.Contains(definition.Name);
                var statusLabel = _statusLabelsByName[definition.Name];
                statusLabel.Text = hasValue ? "ingesteld" : "leeg";
                statusLabel.ForeColor = hasValue ? Color.DarkGreen : Color.Gray;
            }
        }

        private void SaveSecrets()
        {
            var newValues = _inputsByName.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Text);
            _repo.UpdateIfProvided(newValues);

            foreach (var input in _inputsByName.Values)
                input.Clear();

            RefreshStatuses();
            MessageBox.Show(this,
                "Saved. Filled-in fields were encrypted and written; empty fields were left unchanged.\n\n"
                + "Restart the application for changed supplier credentials to take effect.",
                "App Secrets", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BuildAccessGrid()
        {
            dgv_access.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "SecretName",
                DataPropertyName = "SecretName",
                HeaderText = "Secret",
                Width = 150,
                DataSource = SecretSettings.Definitions.Select(d => d.Name).ToList()
            });

            dgv_access.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "UserName",
                DataPropertyName = "UserName",
                HeaderText = "User (Windows login)",
                Width = 150
            });

            dgv_access.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "CanRead",
                DataPropertyName = "CanRead",
                HeaderText = "May use",
                Width = 85
            });

            dgv_access.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "GrantedBy",
                DataPropertyName = "GrantedBy",
                HeaderText = "Granted by",
                ReadOnly = true,
                Width = 110
            });

            dgv_access.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "GrantedDate",
                DataPropertyName = "GrantedDate",
                HeaderText = "Date",
                ReadOnly = true,
                Width = 110,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "dd-MM-yyyy HH:mm" }
            });

            // Removing an existing row revokes the grant right away, without a separate "Save rights".
            dgv_access.UserDeletingRow += (s, e) =>
            {
                if (e.Row.DataBoundItem is SecretAccessEntry entry && !string.IsNullOrWhiteSpace(entry.UserName))
                    RunGuarded(() => _accessRepo.Delete(entry.SecretName, entry.UserName));
            };

            // A SecretAccess row naming a secret that's no longer in Definitions (e.g. renamed) would
            // otherwise throw the combobox column's default "value is not valid" dialog for every repaint.
            dgv_access.DataError += (s, e) => e.ThrowException = false;
        }

        private void LoadAccessEntries()
        {
            _accessEntries = GridFormatting.BindSortable(dgv_access, _accessRepo.GetAll());
            RefreshCopyFromOptions();
        }

        /// <summary>Fills "From" with the users that already have rights, so the source doesn't have to be typed.</summary>
        private void RefreshCopyFromOptions()
        {
            var users = _accessEntries
                .Select(x => x.UserName)
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(u => u)
                .ToArray();

            cb_copyFrom.Items.Clear();
            cb_copyFrom.Items.AddRange(users);
        }

        /// <summary>Copies all rights of one user to one or more users (comma- or semicolon-separated).</summary>
        private void CopyAccess()
        {
            string fromUser = (cb_copyFrom.Text ?? string.Empty).Trim();
            string toUsersRaw = (txt_copyTo.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(fromUser) || string.IsNullOrWhiteSpace(toUsersRaw))
            {
                MessageBox.Show(this, "Fill in both 'From' and 'To'.", "App Secrets", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var toUsers = toUsersRaw
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(u => u.Trim())
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToList();

            int copiedCount = toUsers.Count(toUser => _accessRepo.CopyAccess(fromUser, toUser, ProcurementSession.UserName));

            LoadAccessEntries();
            txt_copyTo.Clear();

            MessageBox.Show(this,
                copiedCount > 0
                    ? $"Copied rights from '{fromUser}' to {copiedCount} user(s)."
                    : $"No rights found for '{fromUser}' to copy.",
                "App Secrets", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SaveAccess()
        {
            dgv_access.EndEdit();

            var validEntries = _accessEntries
                .Where(x => !string.IsNullOrWhiteSpace(x.SecretName) && !string.IsNullOrWhiteSpace(x.UserName))
                .ToList();

            _accessRepo.Save(validEntries);
            LoadAccessEntries();

            MessageBox.Show(this, "Rights saved.", "App Secrets", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>SharedSecrets errors (missing UNIPRO_SECRET_KEY, no DB rights, ...) shown as a message instead of the generic crash dialog.</summary>
        private void RunGuarded(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "App Secrets", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
