using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Models;
using Procurement.Core.Session;
using Procurement.Data.Secrets;
using static Procurement.UI.Support.GridFormatting;

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
    /// Built in code like every other Procurement screen (no Designer file); the layout copies
    /// UniPro's Designer positions. The list of secrets lives in SecretSettings.Definitions, not
    /// here, so the migration and the getters use the same names.
    /// </summary>
    public class AppSecretsForm : Form
    {
        private readonly AppSecretsRepository _repo = new AppSecretsRepository();
        private readonly SecretAccessRepository _accessRepo = new SecretAccessRepository();
        private readonly Dictionary<string, TextBox> _inputsByName = new Dictionary<string, TextBox>();
        private readonly Dictionary<string, Label> _statusLabelsByName = new Dictionary<string, Label>();
        private BindingList<SecretAccessEntry> _accessEntries;

        private Panel _secretsPanel;
        private DataGridView _accessGrid;
        private ComboBox _copyFromCombo;
        private TextBox _copyToBox;

        public AppSecretsForm()
        {
            InitializeComponent();
            BuildSecretRows();
            BuildAccessGrid();
            Load += (s, e) => RunGuarded(() =>
            {
                RefreshStatuses();
                LoadAccessEntries();
            });
        }

        private void InitializeComponent()
        {
            Text = "App Secrets" + (ProcurementSession.TestMode ? " — TEST MODE" : string.Empty);
            ClientSize = new Size(1060, 528);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = ProcurementSession.TestMode ? Color.LightYellow : ClassicScreenBackColor;

            var instructions = new Label
            {
                Location = new Point(12, 9),
                Size = new Size(560, 50),
                Text = "Leave a field empty to keep the current value unchanged. Filled-in values are encrypted and saved "
                     + "(SharedSecrets database, shared with UniPro), and will never be shown on this screen again."
            };

            _secretsPanel = new Panel
            {
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(12, 62),
                Size = new Size(560, 380)
            };

            var saveButton = new Button { Text = "Save", Location = new Point(416, 454), Size = new Size(75, 23) };
            ApplyClassicButtonStyle(saveButton);
            saveButton.Click += (s, e) => RunGuarded(SaveSecrets);

            var closeButton = new Button { Text = "Close", Location = new Point(497, 454), Size = new Size(75, 23) };
            ApplyClassicButtonStyle(closeButton);
            closeButton.Click += (s, e) => Close();

            var accessGroup = new GroupBox
            {
                Text = "Secret Access per user - who may use which setting",
                Location = new Point(588, 9),
                Size = new Size(460, 507)
            };

            _accessGrid = new DataGridView
            {
                Location = new Point(10, 22),
                Size = new Size(440, 406),
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                RowHeadersWidth = 20
            };
            EnableDoubleBuffering(_accessGrid);
            ApplyClassicHeaderStyle(_accessGrid);

            var copyFromLabel = new Label { Text = "From:", Location = new Point(10, 437), Size = new Size(34, 18) };
            _copyFromCombo = new ComboBox { Location = new Point(44, 434), Size = new Size(128, 21) };
            var copyToLabel = new Label { Text = "To:", Location = new Point(178, 437), Size = new Size(35, 18) };
            _copyToBox = new TextBox { Location = new Point(216, 434), Size = new Size(130, 20) };
            var copyButton = new Button { Text = "Copy", Location = new Point(350, 433), Size = new Size(100, 23) };
            ApplyClassicButtonStyle(copyButton);
            copyButton.Click += (s, e) => RunGuarded(CopyAccess);

            var saveAccessButton = new Button { Text = "Save rights", Location = new Point(350, 470), Size = new Size(100, 23) };
            ApplyClassicButtonStyle(saveAccessButton);
            saveAccessButton.Click += (s, e) => RunGuarded(SaveAccess);

            accessGroup.Controls.Add(_accessGrid);
            accessGroup.Controls.Add(copyFromLabel);
            accessGroup.Controls.Add(_copyFromCombo);
            accessGroup.Controls.Add(copyToLabel);
            accessGroup.Controls.Add(_copyToBox);
            accessGroup.Controls.Add(copyButton);
            accessGroup.Controls.Add(saveAccessButton);

            Controls.Add(instructions);
            Controls.Add(_secretsPanel);
            Controls.Add(saveButton);
            Controls.Add(closeButton);
            Controls.Add(accessGroup);
        }

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
                _secretsPanel.Controls.Add(new Label
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
                _secretsPanel.Controls.Add(statusLabel);
                _statusLabelsByName[definition.Name] = statusLabel;

                if (definition.Editable)
                {
                    var input = new TextBox
                    {
                        Location = new Point(10 + labelWidth + statusWidth, y),
                        Size = new Size(inputWidth, 20),
                        UseSystemPasswordChar = true,
                    };
                    _secretsPanel.Controls.Add(input);
                    _inputsByName[definition.Name] = input;
                }
                else
                {
                    _secretsPanel.Controls.Add(new Label
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
            _accessGrid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "SecretName",
                DataPropertyName = "SecretName",
                HeaderText = "Secret",
                Width = 150,
                DataSource = SecretSettings.Definitions.Select(d => d.Name).ToList()
            });

            _accessGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "UserName",
                DataPropertyName = "UserName",
                HeaderText = "User (Windows login)",
                Width = 150
            });

            _accessGrid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "CanRead",
                DataPropertyName = "CanRead",
                HeaderText = "May use",
                Width = 85
            });

            _accessGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "GrantedBy",
                DataPropertyName = "GrantedBy",
                HeaderText = "Granted by",
                ReadOnly = true,
                Width = 110
            });

            _accessGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "GrantedDate",
                DataPropertyName = "GrantedDate",
                HeaderText = "Date",
                ReadOnly = true,
                Width = 110,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "dd-MM-yyyy HH:mm" }
            });

            // Removing an existing row revokes the grant right away, without a separate "Save rights".
            _accessGrid.UserDeletingRow += (s, e) =>
            {
                if (e.Row.DataBoundItem is SecretAccessEntry entry && !string.IsNullOrWhiteSpace(entry.UserName))
                    RunGuarded(() => _accessRepo.Delete(entry.SecretName, entry.UserName));
            };

            // A SecretAccess row naming a secret that's no longer in Definitions (e.g. renamed) would
            // otherwise throw the combobox column's default "value is not valid" dialog for every repaint.
            _accessGrid.DataError += (s, e) => e.ThrowException = false;
        }

        private void LoadAccessEntries()
        {
            _accessEntries = new BindingList<SecretAccessEntry>(_accessRepo.GetAll());
            _accessGrid.DataSource = _accessEntries;
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

            _copyFromCombo.Items.Clear();
            _copyFromCombo.Items.AddRange(users);
        }

        /// <summary>Copies all rights of one user to one or more users (comma- or semicolon-separated).</summary>
        private void CopyAccess()
        {
            string fromUser = (_copyFromCombo.Text ?? string.Empty).Trim();
            string toUsersRaw = (_copyToBox.Text ?? string.Empty).Trim();

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
            _copyToBox.Clear();

            MessageBox.Show(this,
                copiedCount > 0
                    ? $"Copied rights from '{fromUser}' to {copiedCount} user(s)."
                    : $"No rights found for '{fromUser}' to copy.",
                "App Secrets", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SaveAccess()
        {
            _accessGrid.EndEdit();

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
