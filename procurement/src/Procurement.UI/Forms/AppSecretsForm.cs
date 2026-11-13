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

        /// <summary>
        /// Resizable, Dock/flow-based layout (user feedback, 13 nov 2026: "De tekst van de knoppen van
        /// beheer scherm vallen weg en het scherm is ook niet groter te maken"). The first version
        /// copied UniPro's fixed Designer pixel positions, but ApplyClassicButtonStyle's padding
        /// doesn't fit in a fixed 23px-high button, so the captions got clipped. Buttons now AutoSize
        /// like everywhere else in Procurement, and the two halves share the width via a SplitContainer.
        /// </summary>
        private void InitializeComponent()
        {
            Text = "App Secrets" + (ProcurementSession.TestMode ? " — TEST MODE" : string.Empty);
            ClientSize = new Size(1080, 560);
            MinimumSize = new Size(800, 400);
            BackColor = ProcurementSession.TestMode ? Color.LightYellow : ClassicScreenBackColor;

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical
            };
            // Set after the form has its real size: SplitterDistance on a not-yet-sized
            // SplitContainer (default 150px wide) throws an out-of-range exception.
            Load += (s, e) => split.SplitterDistance = Math.Min(580, Math.Max(200, split.Width - 300));

            // Left half: instructions, scrollable list of secrets, Save/Close.
            var instructions = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(4, 6, 4, 0),
                Text = "Leave a field empty to keep the current value unchanged. Filled-in values are encrypted and saved "
                     + "(SharedSecrets database, shared with UniPro), and will never be shown on this screen again."
            };

            _secretsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };

            var leftButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 6, 0, 0)
            };
            var closeButton = new Button { Text = "Close", AutoSize = true };
            ApplyClassicButtonStyle(closeButton);
            closeButton.Click += (s, e) => Close();
            var saveButton = new Button { Text = "Save", AutoSize = true };
            ApplyClassicButtonStyle(saveButton);
            saveButton.Click += (s, e) => RunGuarded(SaveSecrets);
            leftButtons.Controls.Add(closeButton);
            leftButtons.Controls.Add(saveButton);

            split.Panel1.Padding = new Padding(10);
            split.Panel1.Controls.Add(_secretsPanel);
            split.Panel1.Controls.Add(instructions);
            split.Panel1.Controls.Add(leftButtons);

            // Right half: SecretAccess grid plus copy/save controls.
            var accessGroup = new GroupBox
            {
                Text = "Secret Access per user - who may use which setting",
                Dock = DockStyle.Fill,
                Padding = new Padding(8)
            };

            _accessGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                RowHeadersWidth = 20,
                BackgroundColor = Color.White
            };
            EnableDoubleBuffering(_accessGrid);
            ApplyClassicHeaderStyle(_accessGrid);

            var accessButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                WrapContents = true,
                Padding = new Padding(0, 6, 0, 0)
            };
            var copyFromLabel = new Label { Text = "Copy rights from:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
            _copyFromCombo = new ComboBox { Width = 130, Margin = new Padding(3, 5, 3, 3) };
            var copyToLabel = new Label { Text = "to:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
            _copyToBox = new TextBox { Width = 130, Margin = new Padding(3, 5, 3, 3) };
            var copyButton = new Button { Text = "Copy", AutoSize = true };
            ApplyClassicButtonStyle(copyButton);
            copyButton.Click += (s, e) => RunGuarded(CopyAccess);
            var saveAccessButton = new Button { Text = "Save rights", AutoSize = true };
            ApplyClassicButtonStyle(saveAccessButton);
            saveAccessButton.Click += (s, e) => RunGuarded(SaveAccess);
            accessButtons.Controls.Add(copyFromLabel);
            accessButtons.Controls.Add(_copyFromCombo);
            accessButtons.Controls.Add(copyToLabel);
            accessButtons.Controls.Add(_copyToBox);
            accessButtons.Controls.Add(copyButton);
            accessButtons.Controls.Add(saveAccessButton);
            accessButtons.SetFlowBreak(copyButton, true);

            accessGroup.Controls.Add(_accessGrid);
            accessGroup.Controls.Add(accessButtons);

            split.Panel2.Padding = new Padding(10);
            split.Panel2.Controls.Add(accessGroup);

            Controls.Add(split);
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
