using MAX50;
using System;
using System.Collections.Generic;
using System.Configuration;
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

        // MaxSQL.GetPrimaryConnection reads MyLogManager.Instance() as its very first statement
        // and NREs if that was never initialized — UniPro2026's own Login_Load always calls
        // MyLogManager.Create(...) first (see AppSession.Log). Defaults below mirror UniPro2026's
        // Login.cs (logPath/logFile/pvDebug), adjusted to this app's own folder; override via
        // App.config appSettings if these are wrong (no rebuild needed).
        private static readonly string LogPath =
            ConfigurationManager.AppSettings["Logging.Path"] ?? @"C:\Unitron\Procurement\Log";
        private static readonly string LogFile =
            ConfigurationManager.AppSettings["Logging.FileName"] ?? "Procurement.log";
        private static readonly bool DebugLogging =
            !bool.TryParse(ConfigurationManager.AppSettings["Logging.Debug"], out var debugSetting) || debugSetting;

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

        // Absolute positions/sizes mirror UniPro2026's own UI/Forms/Login.Designer.cs
        // (cmbAdministrations at 69,35 size 143x21; cb_testMode at 69,62; bt_LogIn at 101,89 size
        // 75x23 — a small button, not docked/stretched — lbl_company at 12,38; ClientSize
        // 263x147), shifted down one row to make room for the username label UniPro doesn't show.
        private void InitializeComponent()
        {
            Text = "Login";
            ClientSize = new System.Drawing.Size(263, 175);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblUser = new Label
            {
                Text = "Gebruiker",
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(51, 13)
            };
            _userLabel = new Label
            {
                Text = "(onbekend)",
                Location = new System.Drawing.Point(69, 12),
                Size = new System.Drawing.Size(182, 13)
            };

            _companyLabel = new Label
            {
                Text = "Company",
                Location = new System.Drawing.Point(12, 38 + 26),
                Size = new System.Drawing.Size(51, 13)
            };
            _companyCombo = new ComboBox
            {
                Location = new System.Drawing.Point(69, 35 + 26),
                Size = new System.Drawing.Size(143, 21),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            _testModeCheckBox = new CheckBox
            {
                Text = "Testmodus",
                Location = new System.Drawing.Point(69, 62 + 26),
                Size = new System.Drawing.Size(76, 17),
                AutoSize = true
            };

            _loginButton = new Button
            {
                Text = "Log In",
                Location = new System.Drawing.Point(101, 89 + 26),
                Size = new System.Drawing.Size(75, 23)
            };
            _loginButton.Click += LoginButton_Click;

            _statusLabel = new Label
            {
                Location = new System.Drawing.Point(12, 122 + 26),
                Size = new System.Drawing.Size(239, 32),
                ForeColor = System.Drawing.Color.DarkRed
            };

            Controls.Add(lblUser);
            Controls.Add(_userLabel);
            Controls.Add(_companyLabel);
            Controls.Add(_companyCombo);
            Controls.Add(_testModeCheckBox);
            Controls.Add(_loginButton);
            Controls.Add(_statusLabel);
            AcceptButton = _loginButton;
        }

        private void LoginForm_Load(object sender, EventArgs e)
        {
            try
            {
                ProcurementSession.LicensePath = LicPath;
                ProcurementSession.LogPath = LogPath;
                ProcurementSession.LogFile = LogFile;
                ProcurementSession.UserName = ResolveWindowsUserName();
                _userLabel.Text = ProcurementSession.UserName;

                // Must run before any MaxSQL call — see the LogPath/LogFile/DebugLogging comment above.
                MyLogManager.Create(LogFile, LogPath, DebugLogging).GetCurrentClassLogger();

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
