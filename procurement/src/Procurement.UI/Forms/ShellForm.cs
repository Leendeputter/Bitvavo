using System;
using System.Drawing;
using System.Windows.Forms;
using Procurement.Services.Login;
using Procurement.UI.Composition;
using Procurement.UI.Security;
using static Procurement.UI.Support.GridFormatting;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// The actual application root (what Program.cs passes to Application.Run) — a menu bar plus an
    /// MDI client area, no content of its own otherwise. Took over the Application.Run() role from
    /// MainForm (user feedback, 20 okt 2026): "De Purchase Requests ziet er nog steeds uit als een
    /// hoofdform. Ik kan dat niet afsluiten of minimaliseren. En ik wil het bij opstarten gewoon zelf
    /// kunnen openen via menu - Purchase Requests. Nu opent het default." Both complaints had the same
    /// root cause — MainForm being the literal Application.Run() form made it the application itself,
    /// not a peer window: closing it ends the whole app instead of just that one screen, and nothing
    /// opens until the user picks it from this menu.
    ///
    /// MainForm (and Orders/Approvals/Supplier Mapping/Settings/Audit Log) were briefly independent,
    /// owner-less top-level windows, but that let them be dragged anywhere on the desktop, outside
    /// this window entirely — not what "waarbij Component Procurement de container is" meant (user
    /// feedback, 20 okt 2026): "De schermen van bv Purchase Request kan ik nog steeds buiten het
    /// hoofdscherm plaatsen, bedoeling is dat die binnen het hoofdscherm blijven, MDI-container?".
    /// IsMdiContainer = true plus MdiParent = this on each child is exactly that: every screen now
    /// lives inside this window's client area and can't be moved outside it.
    ///
    /// Nothing opens automatically at startup — the user picks what to look at, Activity > Purchase
    /// Requests included.
    ///
    /// Built with the Visual Studio Designer (user request, 16 nov 2026): menu bar and MDI settings
    /// in ShellForm.Designer.cs; the title (depends on the login) and the blue menu renderer are set
    /// in the constructor.
    /// </summary>
    public partial class ShellForm : Form
    {
        private readonly CompositionRoot _composition;

        // Tracked (unlike the other five screens) so a second "Purchase Requests" click brings the
        // existing window forward instead of opening a confusing second copy of this app's main
        // work surface, with its own separate in-progress Query/selection state.
        private MainForm _mainForm;

        public ShellForm(CompositionRoot composition)
        {
            _composition = composition ?? throw new ArgumentNullException(nameof(composition));
            InitializeComponent();

            // Window title depends on the login, so it's set here rather than in the Designer.
            Text = $"Component Procurement – {_composition.Session.CompanyName} ({_composition.Session.UserName})"
                 + (_composition.Session.TestMode ? " – TEST MODE" : string.Empty);

            // Classic blue menu-bar look like MAX's own MRP menu (user request, 20 okt 2026). A custom
            // renderer can't be set in the Designer, so it's applied here.
            menuStrip1.RenderMode = ToolStripRenderMode.Professional;
            menuStrip1.Renderer = new ToolStripProfessionalRenderer(new ClassicBlueColorTable());
        }

        // Activity: recurring work to check on. Options: configuration. Beheer: same menu name, item
        // and access right as UniPro2026's own Beheer menu (user request, 13 nov 2026), hidden in
        // OnLoad for users without MGR User Security. Menu items use the Designer's default
        // "xxxToolStripMenuItem" names, as UniPro.Designer.cs does.
        private void purchaseRequestsToolStripMenuItem_Click(object sender, EventArgs e) => ShowPurchaseRequests();

        private void purchaseOrdersToolStripMenuItem_Click(object sender, EventArgs e) => ShowOrders();

        private void approvalsToolStripMenuItem_Click(object sender, EventArgs e) => ShowApprovals();

        private void supplierMappingToolStripMenuItem_Click(object sender, EventArgs e) => ShowSupplierMapping();

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e) => ShowSettings();

        private void auditLogToolStripMenuItem_Click(object sender, EventArgs e) => ShowAuditLog();

        private void appSecretsToolStripMenuItem_Click(object sender, EventArgs e) => ShowAppSecrets();

        /// <summary>
        /// The MdiClient control behind the menu strip is created automatically once IsMdiContainer
        /// is true and this Form's handle exists — not before, hence doing this in OnLoad rather than
        /// InitializeComponent. Matches its backdrop to ClassicScreenBackColor so the area
        /// behind/around child windows (e.g. a non-maximized child, or the margin next to one) reads
        /// as the same pale blue instead of the MdiClient's own default flat gray.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            foreach (Control control in Controls)
            {
                if (control is MdiClient mdiClient)
                    mdiClient.BackColor = ClassicScreenBackColor;
            }

            beheerToolStripMenuItem.Visible = HasBeheerAccess();

            if (_composition.StartupWarnings.Count > 0)
            {
                MessageBox.Show(this,
                    string.Join("\n\n", _composition.StartupWarnings),
                    "Let op", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>A failing rights lookup hides the menu rather than crashing startup.</summary>
        private static bool HasBeheerAccess()
        {
            try
            {
                return SecurityHelper.HasAccess(AccessRights.MgrUserSecurity, MAX50.SecLevel.USERSEC_READWRITE);
            }
            catch
            {
                return false;
            }
        }

        private void ShowAppSecrets()
        {
            try
            {
                SecurityHelper.DemandAccess(AccessRights.MgrUserSecurity, MAX50.SecLevel.USERSEC_READWRITE);
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show(this, ex.Message, "Geen rechten", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // One instance at a time, like UniPro's OpenOrActivateForm<AppSecretsForm>().
            foreach (var child in MdiChildren)
            {
                if (child is AppSecretsForm existing)
                {
                    if (existing.WindowState == FormWindowState.Minimized)
                        existing.WindowState = FormWindowState.Normal;
                    existing.Activate();
                    return;
                }
            }

            var form = new AppSecretsForm { MdiParent = this };
            form.FormClosed += (s, e) => form.Dispose();
            form.Show();
        }

        private void ShowPurchaseRequests()
        {
            if (_mainForm == null || _mainForm.IsDisposed)
            {
                _mainForm = new MainForm(_composition) { MdiParent = this };
                _mainForm.FormClosed += (s, e) => _mainForm = null;
                _mainForm.Show();
                return;
            }

            if (_mainForm.WindowState == FormWindowState.Minimized)
                _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.Activate();
        }

        private void ShowOrders()
        {
            var form = new OrdersForm(_composition.PurchaseOrderRepository, _composition.PurchaseRequestRepository, _composition.MaxOrderRepository) { MdiParent = this };
            form.FormClosed += (s, e) => form.Dispose();
            form.Show();
        }

        /// <summary>Refreshes Purchase Requests (if open) after Approvals closes — a manual approval decision can move a request's Workflow status out of WaitingApproval, previously refreshed by MainForm itself when it owned this button directly.</summary>
        private void ShowApprovals()
        {
            var form = new ApprovalForm(_composition.Engine) { MdiParent = this };
            form.FormClosed += async (s, e) =>
            {
                form.Dispose();
                if (_mainForm != null && !_mainForm.IsDisposed)
                    await _mainForm.RefreshLocalRequestsAsync();
            };
            form.Show();
        }

        private void ShowSupplierMapping()
        {
            var form = new SupplierMappingForm(_composition.SupplierProductMappingRepository) { MdiParent = this };
            form.FormClosed += (s, e) => form.Dispose();
            form.Show();
        }

        private void ShowSettings()
        {
            var form = new SettingsForm(_composition.PolicyRepository, _composition.SupplierRepository) { MdiParent = this };
            form.FormClosed += (s, e) => form.Dispose();
            form.Show();
        }

        private void ShowAuditLog()
        {
            var form = new AuditLogForm(_composition.ProcurementEventRepository) { MdiParent = this };
            form.FormClosed += (s, e) => form.Dispose();
            form.Show();
        }

        /// <summary>
        /// Light-blue gradient menu bar matching the look of MAX's own MRP menu (user-supplied
        /// screenshot, 20 okt 2026) — only the colors that screenshot's menu bar actually shows
        /// (gradient background, selection/press highlight, borders) are overridden; everything else
        /// falls back to ProfessionalColorTable's own defaults.
        /// </summary>
        private sealed class ClassicBlueColorTable : ProfessionalColorTable
        {
            public override Color MenuStripGradientBegin => Color.FromArgb(222, 234, 246);
            public override Color MenuStripGradientEnd => Color.FromArgb(185, 211, 238);
            public override Color MenuBorder => Color.FromArgb(120, 160, 210);
            public override Color MenuItemBorder => Color.FromArgb(120, 160, 210);
            public override Color MenuItemSelected => Color.FromArgb(193, 218, 244);
            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(193, 218, 244);
            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(193, 218, 244);
            public override Color MenuItemPressedGradientBegin => Color.FromArgb(155, 190, 230);
            public override Color MenuItemPressedGradientEnd => Color.FromArgb(155, 190, 230);
            public override Color ImageMarginGradientBegin => Color.White;
            public override Color ImageMarginGradientMiddle => Color.White;
            public override Color ImageMarginGradientEnd => Color.White;
        }
    }
}
