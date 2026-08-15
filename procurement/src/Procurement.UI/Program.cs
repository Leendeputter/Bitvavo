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
            // this block only checks whether the current model's shape is already there and seeds
            // business-rule data — it never creates or alters tables itself.
            Database.SetInitializer<ProcurementDbContext>(null);

            using (var context = new ProcurementDbContext(ProcurementSession.SharedConnectionString))
            {
                bool schemaExists;
                try
                {
                    // Checking for a table's mere existence isn't enough to catch a schema that's
                    // out of date rather than missing outright — each past model change added a
                    // column to an already-existing table (aug-2026: PurchaseOrder+SupplierOrder
                    // merge added PurchaseOrder.SupplierCode; the DigiKey/Farnell credential storage
                    // added Supplier.ClientIdEncrypted; the MAX PO-write plumbing added
                    // PurchaseRequestLine.MaxLineNumber), so an older database still has the table
                    // but not that column. Checking for the newest column of each catches "never
                    // created" and "needs to be recreated for a reshaped model" with the same query.
                    schemaExists = context.Database.SqlQuery<int>(
                        "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Procurement_PurchaseOrder') AND name = 'SupplierCode'"
                        + " UNION ALL SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Procurement_Supplier') AND name = 'ClientIdEncrypted'"
                        + " UNION ALL SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Procurement_PurchaseRequestLine') AND name = 'MaxLineNumber'")
                        .All(count => count > 0);
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
                    // tooling needed, and guaranteed to match the model exactly. It always emits a
                    // CREATE TABLE for every table in the model though (it's not a diff/ALTER tool),
                    // so every Procurement_-prefixed table needs to be dropped first, even the ones
                    // whose shape didn't actually change — that's what SchemaRecreateCleanupSql does.
                    // The user reviews the full script in SSMS before running it, which is the actual
                    // goal here, not any particular tool.
                    var script = SchemaRecreateCleanupSql + ((IObjectContextAdapter)context).ObjectContext.CreateDatabaseScript();
                    var scriptPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        $"procurement-schema-{DateTime.Now:yyyyMMdd-HHmmss}.sql");
                    File.WriteAllText(scriptPath, script);

                    MessageBox.Show(
                        "De Procurement_-tabellen bestaan nog niet (of niet meer in de huidige vorm) in deze database.\n\n"
                        + $"Er is een script gegenereerd op je bureaublad:\n{scriptPath}\n\n"
                        + "Bekijk het script, voer het uit in SQL Server Management Studio, en start de app daarna opnieuw.\n\n"
                        + "Het script begint met het opruimen van alle bestaande Procurement_-tabellen (schone lei, "
                        + "zoals eerder afgesproken voor deze fase) — dat is veilig om nog een keer te draaien tegen bv. "
                        + "Unitron_test, ook als die tabellen daar niet (allemaal) bestaan.",
                        "Schema aanmaken", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                SeedData.EnsureSeeded(context);
            }

            var composition = new CompositionRoot();

            Application.Run(new MainForm(composition));
        }

        // Every Procurement_-prefixed table this app currently creates — dropped ahead of recreating
        // the schema from the current model. Needed even for tables whose shape didn't change,
        // because CreateDatabaseScript() always emits CREATE TABLE for the whole model (it's not an
        // ALTER/diff tool). Most recently needed by: the aug-2026 PurchaseOrder+SupplierOrder merge
        // (dropped Procurement_SupplierOrder/Procurement_SupplierOrderLine, reshaped
        // Procurement_PurchaseOrder/Procurement_PurchaseOrderLine, added
        // Procurement_PurchaseOrderDelivery); and the DigiKey/Farnell credential storage (added
        // ClientIdEncrypted/ClientSecretEncrypted/ApiKeyEncrypted to Procurement_Supplier) — same
        // clean-slate approach agreed for the earlier Procurement_-prefix rename, now reused for
        // later schema churn during this prototype phase. Drops every foreign key touching one of
        // these tables first (by name, via sys.foreign_keys) instead of hand-ordering the DROP
        // TABLEs around their dependencies. Every statement is guarded so the same script can run
        // against a database that never had some/any of these tables (e.g. Unitron_test) without
        // failing.
        private const string SchemaRecreateCleanupSql = @"
DECLARE @oldTables TABLE (Name NVARCHAR(128));
INSERT INTO @oldTables (Name) VALUES
    ('Procurement_Supplier'), ('Procurement_SupplierCapability'), ('Procurement_PurchaseRequest'),
    ('Procurement_PurchaseRequestLine'), ('Procurement_SupplierOffer'), ('Procurement_SupplierOfferPackaging'),
    ('Procurement_SupplierOrder'), ('Procurement_SupplierOrderLine'), ('Procurement_SupplierProduct'),
    ('Procurement_SupplierProductMapping'), ('Procurement_SupplierSelection'), ('Procurement_ApprovalRequest'),
    ('Procurement_PurchaseOrder'), ('Procurement_PurchaseOrderLine'), ('Procurement_PurchaseOrderDelivery'),
    ('Procurement_Event'), ('Procurement_SupplierPreference'), ('Procurement_PackagingPolicy'),
    ('Procurement_ApprovalPolicy');

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
