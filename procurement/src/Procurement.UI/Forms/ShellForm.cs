using System;
using System.Drawing;
using System.Windows.Forms;
using Procurement.UI.Composition;
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
    /// </summary>
    public class ShellForm : Form
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
        }

        private void InitializeComponent()
        {
            Text = $"Component Procurement – {_composition.Session.CompanyName} ({_composition.Session.UserName})"
                 + (_composition.Session.TestMode ? " – TEST MODE" : string.Empty);
            // A normal, resizable/maximizable window, opening maximized (user request, 20 okt 2026 —
            // the first version was deliberately small/fixed-size as "just a launcher", but that read
            // as broken rather than minimal). Width/Height are only what it'd fall back to if the
            // user un-maximizes it.
            Width = 1300;
            Height = 860;
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            // White instead of the default SystemColors.Control gray, for more contrast against the
            // menu bar (user request, 20 okt 2026 — this is the "startscherm" they meant, not
            // MainForm, which keeps its own default background).
            BackColor = Color.White;

            // Confines every screen to this window instead of letting them be dragged out onto the
            // desktop (user request, 20 okt 2026 — see class comment). The actual MDI child area is a
            // separate, auto-created MdiClient control, not this Form itself, hence OnLoad below to
            // reach it and match its backdrop to the child forms' own pale-blue background — otherwise
            // it'd default to a plain flat gray (SystemColors.AppWorkspace) behind/around them.
            IsMdiContainer = true;

            // Classic blue menu-bar look (user request, 20 okt 2026 — screenshot of MAX's own MRP
            // menu bar as the reference). ToolStripProfessionalRenderer is the standard WinForms way
            // to recolor a MenuStrip beyond plain BackColor (System render mode mostly ignores it in
            // favor of the OS theme); see ClassicBlueColorTable below for the actual palette.
            var menuStrip = new MenuStrip
            {
                Dock = DockStyle.Top,
                RenderMode = ToolStripRenderMode.Professional,
                Renderer = new ToolStripProfessionalRenderer(new ClassicBlueColorTable())
            };

            // Approvals grouped under Activity too, not just the two the user named explicitly when
            // this menu was first built — same category (recurring work to check on), not
            // configuration like the Options items.
            var activityMenu = new ToolStripMenuItem("Activity");
            activityMenu.DropDownItems.Add("Purchase Requests", null, (s, e) => ShowPurchaseRequests());
            activityMenu.DropDownItems.Add("Purchase Orders...", null, (s, e) => ShowOrders());
            activityMenu.DropDownItems.Add("Approvals...", null, (s, e) => ShowApprovals());

            var optionsMenu = new ToolStripMenuItem("Options");
            optionsMenu.DropDownItems.Add("Supplier Mapping...", null, (s, e) => ShowSupplierMapping());
            optionsMenu.DropDownItems.Add("Settings...", null, (s, e) => ShowSettings());
            optionsMenu.DropDownItems.Add("Audit Log...", null, (s, e) => ShowAuditLog());

            // Activity before Options (user request, 20 okt 2026).
            menuStrip.Items.AddRange(new ToolStripItem[] { activityMenu, optionsMenu });
            MainMenuStrip = menuStrip;

            Controls.Add(menuStrip);
        }

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
            var form = new OrdersForm(_composition.PurchaseOrderRepository, _composition.PurchaseRequestRepository) { MdiParent = this };
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
