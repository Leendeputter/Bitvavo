using System;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Session;
using Procurement.Data;
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

            // No EF6 migrations tooling — Add-Migration/Update-Database rely on the classic NuGet
            // install.ps1/init.ps1 script mechanism to register their PowerShell cmdlets in
            // Package Manager Console, which NuGet never runs for PackageReference-style projects
            // (every project in this solution, including this one). So "Add-Migration" is simply
            // not available here, no matter how Package Manager Console is opened — that's not a
            // missing Visual Studio component. Schema changes are instead reviewed as plain SQL
            // scripts (see README's "Database" section for how a future one gets generated/applied)
            // and are expected to already exist on the target database by the time the app runs;
            // this block only checks that a canonical table exists and seeds business-rule data —
            // it never creates or alters tables itself.
            Database.SetInitializer<ProcurementDbContext>(null);

            using (var context = new ProcurementDbContext(ProcurementSession.SharedConnectionString))
            {
                bool schemaExists;
                try
                {
                    schemaExists = context.Database.SqlQuery<int>(
                        "SELECT COUNT(*) FROM sys.tables WHERE name = 'Procurement_PurchaseRequest'").Single() > 0;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Kan geen verbinding maken met de Unitron-database. Controleer de connection string die is opgebouwd na het inloggen.\n\n{ex.Message}",
                        "Database-fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!schemaExists)
                {
                    // ObjectContext.CreateDatabaseScript() generates the CREATE TABLE/INDEX/FK DDL
                    // for the *current* Code First model directly from EF6 itself — no migrations
                    // tooling needed, and guaranteed to match the model exactly (safer than
                    // hand-written DDL for ~18 tables). The user reviews this in SSMS before running
                    // it, which is the actual goal here, not any particular tool.
                    //
                    // It only reflects the current model though — it has no idea these 18 tables
                    // used to exist under different (pre-"Procurement_"-prefix) names, so dropping
                    // those (agreed: clean slate, old test data can go) has to be prepended by hand.
                    var script = OldTableCleanupSql + ((IObjectContextAdapter)context).ObjectContext.CreateDatabaseScript();
                    var scriptPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        $"procurement-schema-{DateTime.Now:yyyyMMdd-HHmmss}.sql");
                    File.WriteAllText(scriptPath, script);

                    MessageBox.Show(
                        "De Procurement_-tabellen bestaan nog niet in deze database.\n\n"
                        + $"Er is een script gegenereerd op je bureaublad:\n{scriptPath}\n\n"
                        + "Bekijk het script, voer het uit in SQL Server Management Studio, en start de app daarna opnieuw.\n\n"
                        + "Het script begint met het opruimen van de oude tabellen (van vóór het Procurement_-prefix) — "
                        + "dat is veilig om nog een keer te draaien tegen bv. Unitron_test, ook als die tabellen daar niet (allemaal) bestaan.",
                        "Schema aanmaken", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                SeedData.EnsureSeeded(context);
            }

            var composition = new CompositionRoot();

            Application.Run(new MainForm(composition));
        }

        // The 18 tables this app used to create before the Procurement_ prefix — dropped ahead of
        // recreating the (now prefixed) schema, agreed as a clean slate for both Unitron and
        // Unitron_test. Drops every foreign key touching one of these tables first (by name, via
        // sys.foreign_keys) instead of hand-ordering 18 DROP TABLEs around their dependencies —
        // safe because these are exclusively this app's own old tables, nothing else references
        // them. Every statement is guarded so the same script can run against a database that
        // never had some/any of these tables without failing.
        private const string OldTableCleanupSql = @"
DECLARE @oldTables TABLE (Name NVARCHAR(128));
INSERT INTO @oldTables (Name) VALUES
    ('Supplier'), ('SupplierCapability'), ('PurchaseRequest'), ('PurchaseRequestLine'),
    ('SupplierOffer'), ('SupplierOfferPackaging'), ('SupplierOrder'), ('SupplierOrderLine'),
    ('SupplierProduct'), ('SupplierProductMapping'), ('SupplierSelection'), ('ApprovalRequest'),
    ('PurchaseOrder'), ('PurchaseOrderLine'), ('ProcurementEvent'), ('SupplierPreference'),
    ('PackagingPolicy'), ('ApprovalPolicy');

DECLARE @dropFkSql NVARCHAR(MAX) = N'';
SELECT @dropFkSql += 'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + '.' + QUOTENAME(t.name)
    + ' DROP CONSTRAINT ' + QUOTENAME(fk.name) + ';' + CHAR(13)
FROM sys.foreign_keys fk
JOIN sys.tables t ON fk.parent_object_id = t.object_id
WHERE t.name IN (SELECT Name FROM @oldTables)
   OR OBJECT_NAME(fk.referenced_object_id) IN (SELECT Name FROM @oldTables);
EXEC sp_executesql @dropFkSql;

DECLARE @dropTableSql NVARCHAR(MAX) = N'';
SELECT @dropTableSql += 'IF OBJECT_ID(''dbo.' + Name + ''', ''U'') IS NOT NULL DROP TABLE dbo.' + Name + ';' + CHAR(13)
FROM @oldTables;
EXEC sp_executesql @dropTableSql;
GO
";
    }
}
