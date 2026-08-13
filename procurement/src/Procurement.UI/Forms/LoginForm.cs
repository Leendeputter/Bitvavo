using MAX50;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;
using Procurement.Core.Models;
using Procurement.Core.Session;
using Procurement.UI.Composition;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Login + company selection, mirroring UniPro2026's UI/Forms/Login.cs: Windows identity (no
    /// password field), a MAX "administratie" (company) dropdown sourced from ExactRMCompanies
    /// via the same MaxSQL infrastructure, and a Testmodus checkbox. On success this populates
    /// <see cref="ProcurementSession"/>; Program.cs then proceeds to build the composition root
    /// and open MainForm.
    ///
    /// TODO: verify LicPath and the MAXCore/MaxOrderNET/MaxTransNET HintPaths in
    /// Procurement.UI.csproj match this machine's real MAX installation — copied from
    /// UniPro2026's own working project, not independently verified against the MAX50 API
    /// surface (that library's source isn't available here).
    ///
    /// Deliberate simplification vs UniPro2026's Login.cs: this form does not keep a
    /// process-wide open SqlConnection for the admin/primary connections (UniPro2026 does, for
    /// MaxTransferAdapter/MAX SDK compatibility — see AppSession's comment). Procurement's own
    /// data access goes through EF6's ProcurementDbContext, which manages its own connection
    /// lifecycle, so only the resolved connection strings are kept.
    /// </summary>
    public class LoginForm : Form
    {
        // TODO: confirm this is the correct license path for this machine (same as UniPro2026's Login.licPath).
        private const string LicPath = @"\\192.168.0.12\Exact Max\RMServer\LIC";

        private Label _userLabel;
        private Label _companyLabel;
        private ComboBox _companyCombo;
        private CheckBox _testModeCheckBox;
        private Button _loginButton;
        private Label _statusLabel;

        public LoginForm()
        {
            InitializeComponent();
            Load += LoginForm_Load;
        }

        private void InitializeComponent()
        {
            Text = "Componenteninkoop – Inloggen";
            Width = 340;
            Height = 220;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, Padding = new Padding(12), AutoSize = true };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));

            _userLabel = new Label { Text = "(gebruiker onbekend)", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            _companyCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _testModeCheckBox = new CheckBox { Text = "Testmodus", AutoSize = true };

            AddRow(layout, "Gebruiker:", _userLabel);
            _companyLabel = AddRow(layout, "Administratie:", _companyCombo);
            AddRow(layout, string.Empty, _testModeCheckBox);

            _loginButton = new Button { Text = "Inloggen", Width = 100, Dock = DockStyle.Bottom };
            _loginButton.Click += LoginButton_Click;

            _statusLabel = new Label { Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(4), ForeColor = System.Drawing.Color.DarkRed };

            Controls.Add(layout);
            Controls.Add(_loginButton);
            Controls.Add(_statusLabel);
            AcceptButton = _loginButton;
        }

        private static Label AddRow(TableLayoutPanel layout, string label, Control control)
        {
            var row = layout.RowCount;
            layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var labelControl = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            layout.Controls.Add(labelControl, 0, row);
            layout.Controls.Add(control, 1, row);
            return labelControl;
        }

        private void LoginForm_Load(object sender, EventArgs e)
        {
            try
            {
                ProcurementSession.LicensePath = LicPath;
                ProcurementSession.UserName = ResolveWindowsUserName();
                _userLabel.Text = ProcurementSession.UserName;

                using (var conn = MaxSQL.GetPrimaryConnection(LicPath))
                {
                    ProcurementSession.PrimaryConnectionString = conn.ConnectionString;
                    LoadCompanies(conn);
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Kan geen verbinding maken met MAX: {ex.Message}";
                _loginButton.Enabled = false;
            }
        }

        // WindowsIdentity.GetCurrent().Name can be null in some environments (restricted token,
        // certain service/sandbox contexts) — UniPro2026's own Login.cs assumes it never is,
        // which is exactly what crashed here with a NullReferenceException before this try/catch
        // existed. Environment.UserName is a simpler, more broadly reliable way to get the same
        // short account name and is used as a fallback rather than the sole source, to stay
        // close to UniPro2026's original behavior when it does work.
        private static string ResolveWindowsUserName()
        {
            try
            {
                var identityName = WindowsIdentity.GetCurrent()?.Name;
                if (!string.IsNullOrEmpty(identityName))
                    return identityName.Split('\\').Last();
            }
            catch
            {
                // Fall through to Environment.UserName below.
            }

            return Environment.UserName;
        }

        private void LoadCompanies(SqlConnection conn)
        {
            var companies = new List<CompanyInfo>();

            using (var cmd = new SqlCommand("SELECT CompanyID, CompanyName FROM ExactRMCompanies ORDER BY CompanyID", conn))
            {
                if (conn.State != ConnectionState.Open) conn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        companies.Add(new CompanyInfo
                        {
                            CompanyID = int.Parse(reader["CompanyID"].ToString()),
                            CompanyName = reader["CompanyName"].ToString()
                        });
                    }
                }
            }

            _companyCombo.DataSource = companies;
            _companyCombo.DisplayMember = "CompanyName";
            _companyCombo.ValueMember = "CompanyID";
        }

        private void LoginButton_Click(object sender, EventArgs e)
        {
            var selectedCompany = _companyCombo.SelectedItem as CompanyInfo;
            if (selectedCompany == null)
            {
                _statusLabel.Text = "Selecteer eerst een administratie.";
                return;
            }

            ProcurementSession.TestMode = _testModeCheckBox.Checked;

            try
            {
                using (var primary = new SqlConnection(ProcurementSession.PrimaryConnectionString))
                using (var admin = MaxSQL.GetConnection(LicPath, primary, selectedCompany.CompanyName))
                {
                    ProcurementSession.AdminConnectionString = admin.ConnectionString;
                }

                ProcurementSession.SharedConnectionString = ProcurementConnectionStringHelper.BuildSharedConnectionString(
                    ProcurementSession.AdminConnectionString, ProcurementSession.TestMode);
                ProcurementSession.CompanyId = selectedCompany.CompanyID;
                ProcurementSession.CompanyName = selectedCompany.CompanyName;

                // Only clear the pools this login actually touched, not every pooled SQL
                // connection process-wide (same reasoning as UniPro2026's Login.cs).
                using (var primaryForClear = new SqlConnection(ProcurementSession.PrimaryConnectionString))
                    SqlConnection.ClearPool(primaryForClear);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Inloggen bij '{selectedCompany.CompanyName}' is mislukt: {ex.Message}";
            }
        }
    }
}
