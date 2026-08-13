using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.IO;
using System.Windows.Forms;
using Procurement.Core.Session;
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
            // TEMPORARY DIAGNOSTICS — remove once the LoginForm/MaxSQL crash is understood.
            // Logs *every* exception the moment it's thrown, including ones caught somewhere
            // (e.g. inside LoginForm's own try/catch) or thrown on a different thread than the
            // one running that try/catch — both of which would explain a crash dialog still
            // appearing even though LoginForm_Load's MaxSQL call is wrapped in try/catch.
            var diagnosticsLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "procurement-firstchance.log");
            AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
            {
                try
                {
                    File.AppendAllText(diagnosticsLogPath,
                        $"{DateTime.Now:O} [thread {System.Threading.Thread.CurrentThread.ManagedThreadId}]\n{e.Exception}\n\n");
                }
                catch
                {
                    // Best-effort diagnostics only — never let logging itself crash the app.
                }
            };

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
            //
            // Database.SetInitializer<TContext,TConfig>() + Database.Initialize() is the usual
            // way to run this, but that path has EF6 construct its own ProcurementDbContext
            // internally via its *parameterless* constructor ("name=ProcurementDbContext") to
            // resolve a connection — which fails now that the connection string is only known at
            // runtime (after login) and no longer lives in App.config under that name. Driving
            // the DbMigrator directly with an explicit DbConnectionInfo (built from
            // ProcurementSession.SharedConnectionString, populated by LoginForm) sidesteps that
            // entirely.
            try
            {
                // EF6 runs a default initializer strategy (CreateDatabaseIfNotExists<TContext>)
                // for any context type that hasn't had SetInitializer called for it — and that
                // default strategy *also* constructs its own ProcurementDbContext via the
                // parameterless constructor to check the database, independent of the DbMigrator
                // call below. Disabling it explicitly is the only way to be sure nothing tries
                // that route — schema management is fully manual here (the DbMigrator call).
                Database.SetInitializer<ProcurementDbContext>(null);

                var migrationsConfiguration = new Configuration
                {
                    TargetDatabase = new DbConnectionInfo(ProcurementSession.SharedConnectionString, "System.Data.SqlClient")
                };
                new DbMigrator(migrationsConfiguration).Update();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Kan geen verbinding maken met de Unitron-database. Controleer de connection string die is opgebouwd na het inloggen.\n\n{ex.Message}",
                    "Database-fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var composition = new CompositionRoot();

            Application.Run(new MainForm(composition));
        }
    }
}
