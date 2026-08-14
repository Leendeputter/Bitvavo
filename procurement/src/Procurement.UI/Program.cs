using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Infrastructure;
using System.IO;
using System.Linq;
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

            // EF6 code-based migrations (Migrations/Configuration.cs, AutomaticMigrationsEnabled =
            // false) — schema changes only exist as an explicit, reviewable migration file
            // (scaffolded via Add-Migration in Visual Studio, see README's "Database-migraties"
            // section), never inferred/applied silently from a model diff. This block still
            // applies whatever migration(s) are already scaffolded, but only after the user
            // explicitly confirms which ones — nothing touches Unitron without that.
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
                var migrator = new DbMigrator(migrationsConfiguration);

                var pending = migrator.GetPendingMigrations().ToList();
                if (pending.Count > 0)
                {
                    var confirm = MessageBox.Show(
                        "De volgende database-wijzigingen staan klaar om toegepast te worden op de Unitron-database:\n\n"
                        + string.Join("\n", pending)
                        + "\n\nDoorgaan?",
                        "Database-migratie bevestigen", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes)
                        return;
                }

                migrator.Update();
            }
            catch (AutomaticMigrationsDisabledException ex)
            {
                // Thrown when the Code First model doesn't match what's recorded for this database
                // and there's no scaffolded migration to explain the difference — i.e. someone
                // changed an entity/mapping without running Add-Migration first. Not a connection
                // problem, so it gets its own message instead of the generic one below.
                MessageBox.Show(
                    "Het model is aangepast, maar er is nog geen migratie gescaffold voor deze wijziging.\n\n"
                    + "Draai in Visual Studio (Package Manager Console, Default project: Procurement.Data):\n"
                    + "Add-Migration <naam> -ConnectionString \"...\" -ConnectionProviderName \"System.Data.SqlClient\"\n\n"
                    + "Zie de \"Database-migraties\"-sectie in README.md voor de exacte stappen.\n\n"
                    + ex.Message,
                    "Migratie ontbreekt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
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
