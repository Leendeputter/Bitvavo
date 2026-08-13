using System;
using System.Data.Entity;
using System.Windows.Forms;
using Procurement.Data;
using Procurement.Data.Migrations;
using Procurement.UI.Composition;
using Procurement.UI.Forms;

namespace Procurement.UI
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // Without this, an exception thrown after the first `await` inside an async-void
            // event handler (e.g. Load, SelectionChanged, Click) is rethrown on the UI
            // SynchronizationContext with no visible feedback — the form just appears to hang on
            // whatever status text was last set. Route it to a message box instead.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => MessageBox.Show(
                $"Onverwachte fout:\n\n{e.Exception}",
                "Onverwachte fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => MessageBox.Show(
                $"Onverwachte fout:\n\n{e.ExceptionObject}",
                "Onverwachte fout", MessageBoxButtons.OK, MessageBoxIcon.Error);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Mirrors UniPro2026's Program.cs: login (which resolves the MAX company + this
            // app's own "Unitron" connection string into ProcurementSession) runs before
            // anything that needs a database connection.
            using (var login = new LoginForm())
            {
                if (login.ShowDialog() != DialogResult.OK)
                    return;
            }

            // EF6 automatic migrations (Migrations/Configuration.cs) create/update the schema and
            // seed the business-rule tables on first run — no design-time Add-Migration needed.
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<ProcurementDbContext, Configuration>());

            var composition = new CompositionRoot();

            try
            {
                composition.DbContext.Database.Initialize(force: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Kan geen verbinding maken met de Unitron-database. Controleer de connection string die is opgebouwd na het inloggen.\n\n{ex.Message}",
                    "Database-fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Application.Run(new MainForm(composition));
        }
    }
}
