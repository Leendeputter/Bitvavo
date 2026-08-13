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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

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
                    $"Kan geen verbinding maken met de database. Controleer de connection string in App.config.\n\n{ex.Message}",
                    "Database-fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Application.Run(new MainForm(composition));
        }
    }
}
