using System;
using System.Drawing;
using System.Windows.Forms;
using Procurement.UI.Composition;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// The actual application root (what Program.cs passes to Application.Run) — just a menu bar,
    /// no content of its own. Took over that role from MainForm (user feedback, 20 okt 2026): "De
    /// Purchase Requests ziet er nog steeds uit als een hoofdform. Ik kan dat niet afsluiten of
    /// minimaliseren. En ik wil het bij opstarten gewoon zelf kunnen openen via menu - Purchase
    /// Requests. Nu opent het default." Both complaints had the same root cause — MainForm being the
    /// literal Application.Run() form made it the application itself, not a peer window: closing it
    /// ends the whole app instead of just that one screen, and nothing opens until the user picks it
    /// from this menu. MainForm (and Orders/Approvals/Supplier Mapping/Settings/Audit Log) are now
    /// independent windows opened from here via a plain Show() with no Owner, exactly like the other
    /// five already were (see MainForm's own former ShowOrders/etc. comment, now moved here).
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

            var menuStrip = new MenuStrip { Dock = DockStyle.Top };

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

        private void ShowPurchaseRequests()
        {
            if (_mainForm == null || _mainForm.IsDisposed)
            {
                _mainForm = new MainForm(_composition);
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
            var form = new OrdersForm(_composition.PurchaseOrderRepository, _composition.PurchaseRequestRepository);
            form.FormClosed += (s, e) => form.Dispose();
            form.Show();
        }

        /// <summary>Refreshes Purchase Requests (if open) after Approvals closes — a manual approval decision can move a request's Workflow status out of WaitingApproval, previously refreshed by MainForm itself when it owned this button directly.</summary>
        private void ShowApprovals()
        {
            var form = new ApprovalForm(_composition.Engine);
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
            var form = new SupplierMappingForm(_composition.SupplierProductMappingRepository);
            form.FormClosed += (s, e) => form.Dispose();
            form.Show();
        }

        private void ShowSettings()
        {
            var form = new SettingsForm(_composition.PolicyRepository, _composition.SupplierRepository);
            form.FormClosed += (s, e) => form.Dispose();
            form.Show();
        }

        private void ShowAuditLog()
        {
            var form = new AuditLogForm(_composition.ProcurementEventRepository);
            form.FormClosed += (s, e) => form.Dispose();
            form.Show();
        }
    }
}
