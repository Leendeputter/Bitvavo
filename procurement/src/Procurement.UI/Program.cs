using System;
using System.Data.Entity;
using System.Data.SqlClient;
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
                    // PurchaseRequestLine.MaxLineNumber; order-confirmation processing added
                    // PurchaseOrderLine.MaxLineNumber/ConfirmedUnitPrice; sep-2026 added
                    // PurchaseOrder.CompanyId so POs are scoped per MAX company like PurchaseRequest
                    // already was; sep-2026 also added Supplier.AccountId/ContactName/.../CountryCode
                    // for the Ordering API's BuyerContact/ShippingContact, and — in a *later*, separate
                    // commit — Supplier.RefreshTokenEncrypted for that same feature's 3-legged OAuth.
                    // That second column genuinely needs its own probe below: a database that already
                    // had AccountId applied (from the earlier commit) passes the AccountId check and
                    // skips straight to SeedData without ever getting RefreshTokenEncrypted, which
                    // then blows up there with "Invalid column name" instead — CreateDatabaseScript()
                    // recreating the "whole current model" only helps if this check actually decides
                    // to run it. Lesson: every column ever added needs its own probe here, even ones
                    // added to a table another probe already covers, if they didn't ship in the same
                    // commit/schema-run. Checking for the newest column of each catches "never
                    // created" and "needs to be recreated for a reshaped model" with the same query.
                    schemaExists = context.Database.SqlQuery<int>(
                        "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Procurement_PurchaseOrder') AND name = 'CompanyId'"
                        + " UNION ALL SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Procurement_Supplier') AND name = 'AccountId'"
                        + " UNION ALL SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Procurement_Supplier') AND name = 'RefreshTokenEncrypted'"
                        + " UNION ALL SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Procurement_PurchaseRequestLine') AND name = 'MaxLineNumber'"
                        + " UNION ALL SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Procurement_PurchaseOrderLine') AND name = 'MaxLineNumber'")
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

                // Packaging policy per component type + attrition rules (17 nov 2026): an additive
                // change, so instead of the clean-slate recreate above (which would also wipe the
                // supplier preferences/policies set in Settings) this writes a small ALTER/CREATE
                // script that keeps all existing data. Same review-in-SSMS flow.
                bool packagingUpgradeApplied;
                try
                {
                    packagingUpgradeApplied = context.Database.SqlQuery<int>(PackagingUpgradeProbeSql).All(count => count > 0);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Kan het databaseschema niet controleren.\n\n{ex.Message}",
                        "Database-fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!packagingUpgradeApplied)
                {
                    var scriptPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        $"procurement-upgrade-packaging-{DateTime.Now:yyyyMMdd-HHmmss}.sql");
                    File.WriteAllText(scriptPath, PackagingUpgradeSql);

                    MessageBox.Show(
                        "De database mist kolommen of tabellen voor het verpakkingsbeleid, de extra voor uitval, de combinatie-offers (hele reels + rest) het sourcen per artikel of de gewenste leverdatum.\n\n"
                        + $"Er is een upgrade-script gegenereerd op je bureaublad:\n{scriptPath}\n\n"
                        + "Dit script voegt alleen kolommen en een tabel toe (en verwijdert de oude kolom MinimumQuantity); "
                        + "bestaande gegevens blijven staan. Voer het uit in SQL Server Management Studio en start de app daarna opnieuw.",
                        "Schema bijwerken", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                SeedData.EnsureSeeded(context);

                // Supplier credentials live in the shared SharedSecrets database since 13 nov 2026
                // (same as UniPro). That database is created by UniPro's own migration
                // (2026-11-09_create-sharedsecrets-database.sql) — this app doesn't create it, it
                // only checks it's there and reachable, the same way the block above checks its own
                // tables.
                string sharedSecretsProblem = CheckSharedSecrets();
                if (sharedSecretsProblem != null)
                {
                    MessageBox.Show(
                        "De SharedSecrets-database (waar de supplier-credentials staan, gedeeld met UniPro) is niet bereikbaar.\n\n"
                        + "Deze wordt aangemaakt door UniPro's migratie 2026-11-09_create-sharedsecrets-database.sql, "
                        + "met daarna het handmatige rechten-script uit _manual-dba-steps voor de SQL-login van MAX. "
                        + "Zie de README, sectie \"Supplier-credentials\".\n\n"
                        + sharedSecretsProblem,
                        "SharedSecrets ontbreekt", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var migrationMessages = LegacySupplierCredentialMigration.Run(context);
                if (migrationMessages.Count > 0)
                {
                    MessageBox.Show(string.Join("\n\n", migrationMessages),
                        "Supplier-credentials", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            var composition = new CompositionRoot();

            // ShellForm (just a menu bar) is the application root now, not MainForm itself — user
            // request, 20 okt 2026: MainForm being the literal Application.Run() form made it
            // uncloseable/unminimizable as a peer window, and meant it opened automatically instead
            // of only via the menu. See ShellForm's own class comment.
            Application.Run(new ShellForm(composition));
        }

        /// <summary>Null when SharedSecrets.dbo.AppSecrets and dbo.SecretAccess are both reachable, otherwise what went wrong.</summary>
        private static string CheckSharedSecrets()
        {
            try
            {
                using (var cn = new SqlConnection(ProcurementSession.SharedSecretsConnectionString))
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM sys.tables WHERE name IN ('AppSecrets', 'SecretAccess') AND schema_id = SCHEMA_ID('dbo')", cn))
                {
                    cn.Open();
                    var tableCount = (int)cmd.ExecuteScalar();
                    return tableCount == 2 ? null : "De database bestaat, maar dbo.AppSecrets en/of dbo.SecretAccess ontbreken.";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private const string PackagingUpgradeProbeSql =
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Procurement_PackagingPolicy') AND name = 'SurplusPenaltyFactor'"
            + " UNION ALL SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Procurement_SupplierOffer') AND name = 'ComparisonCost'"
            + " UNION ALL SELECT COUNT(*) FROM sys.tables WHERE name = 'Procurement_AttritionRule' AND schema_id = SCHEMA_ID('dbo')"
            + " UNION ALL SELECT CASE WHEN COL_LENGTH('dbo.Procurement_PackagingPolicy', 'MinimumQuantity') IS NULL THEN 1 ELSE 0 END"
            + " UNION ALL SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Procurement_SupplierOffer') AND name = 'RemainderSupplierPartNumber'"
            + " UNION ALL SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Procurement_SupplierOffer') AND name = 'BundleKey'"
            + " UNION ALL SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Procurement_SupplierOffer') AND name = 'RequestedDeliveryDate'";

        // Additive upgrade for the packaging policy per component type (17 nov 2026). Every
        // statement is guarded, so running it twice is harmless. Column types match what EF's
        // CreateDatabaseScript() generates for the same model.
        private const string PackagingUpgradeSql = @"
IF COL_LENGTH('dbo.Procurement_PackagingPolicy', 'ExceptionPriceThreshold') IS NULL
    ALTER TABLE dbo.Procurement_PackagingPolicy ADD ExceptionPriceThreshold DECIMAL(18, 4) NULL;
IF COL_LENGTH('dbo.Procurement_PackagingPolicy', 'ExceptionPackaging') IS NULL
    ALTER TABLE dbo.Procurement_PackagingPolicy ADD ExceptionPackaging NVARCHAR(200) NULL;
IF COL_LENGTH('dbo.Procurement_PackagingPolicy', 'SurplusPenaltyFactor') IS NULL
    ALTER TABLE dbo.Procurement_PackagingPolicy ADD SurplusPenaltyFactor DECIMAL(9, 4) NOT NULL
        CONSTRAINT DF_Procurement_PackagingPolicy_SurplusPenaltyFactor DEFAULT 0.5;
IF COL_LENGTH('dbo.Procurement_PackagingPolicy', 'MinimumQuantity') IS NOT NULL
    ALTER TABLE dbo.Procurement_PackagingPolicy DROP COLUMN MinimumQuantity;
GO

IF COL_LENGTH('dbo.Procurement_SupplierOffer', 'AttritionQuantity') IS NULL
    ALTER TABLE dbo.Procurement_SupplierOffer ADD AttritionQuantity INT NOT NULL
        CONSTRAINT DF_Procurement_SupplierOffer_AttritionQuantity DEFAULT 0;
IF COL_LENGTH('dbo.Procurement_SupplierOffer', 'SurplusQuantity') IS NULL
    ALTER TABLE dbo.Procurement_SupplierOffer ADD SurplusQuantity INT NOT NULL
        CONSTRAINT DF_Procurement_SupplierOffer_SurplusQuantity DEFAULT 0;
IF COL_LENGTH('dbo.Procurement_SupplierOffer', 'ComparisonCost') IS NULL
    ALTER TABLE dbo.Procurement_SupplierOffer ADD ComparisonCost DECIMAL(18, 4) NOT NULL
        CONSTRAINT DF_Procurement_SupplierOffer_ComparisonCost DEFAULT 0;
GO

-- Combination offer: full reels + rest from another variant (18 nov 2026).
IF COL_LENGTH('dbo.Procurement_SupplierOffer', 'RemainderSupplierPartNumber') IS NULL
    ALTER TABLE dbo.Procurement_SupplierOffer ADD RemainderSupplierPartNumber NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.Procurement_SupplierOffer', 'RemainderQuantity') IS NULL
    ALTER TABLE dbo.Procurement_SupplierOffer ADD RemainderQuantity INT NOT NULL
        CONSTRAINT DF_Procurement_SupplierOffer_RemainderQuantity DEFAULT 0;
IF COL_LENGTH('dbo.Procurement_SupplierOffer', 'RemainderUnitPrice') IS NULL
    ALTER TABLE dbo.Procurement_SupplierOffer ADD RemainderUnitPrice DECIMAL(18, 4) NOT NULL
        CONSTRAINT DF_Procurement_SupplierOffer_RemainderUnitPrice DEFAULT 0;
IF COL_LENGTH('dbo.Procurement_SupplierOffer', 'RemainderPackagingType') IS NULL
    ALTER TABLE dbo.Procurement_SupplierOffer ADD RemainderPackagingType INT NULL;
IF COL_LENGTH('dbo.Procurement_SupplierOffer', 'MainUnitPrice') IS NULL
    ALTER TABLE dbo.Procurement_SupplierOffer ADD MainUnitPrice DECIMAL(18, 4) NOT NULL
        CONSTRAINT DF_Procurement_SupplierOffer_MainUnitPrice DEFAULT 0;
GO

-- Sourcing per article: offers of lines sourced together (18 nov 2026).
IF COL_LENGTH('dbo.Procurement_SupplierOffer', 'BundleKey') IS NULL
    ALTER TABLE dbo.Procurement_SupplierOffer ADD BundleKey NVARCHAR(32) NULL;
IF COL_LENGTH('dbo.Procurement_SupplierOffer', 'BundleLineCount') IS NULL
    ALTER TABLE dbo.Procurement_SupplierOffer ADD BundleLineCount INT NOT NULL
        CONSTRAINT DF_Procurement_SupplierOffer_BundleLineCount DEFAULT 0;
IF COL_LENGTH('dbo.Procurement_SupplierOffer', 'BundleNeededQuantity') IS NULL
    ALTER TABLE dbo.Procurement_SupplierOffer ADD BundleNeededQuantity INT NOT NULL
        CONSTRAINT DF_Procurement_SupplierOffer_BundleNeededQuantity DEFAULT 0;
GO

-- Requested delivery date per offer/bundle (18 nov 2026).
IF COL_LENGTH('dbo.Procurement_SupplierOffer', 'RequestedDeliveryDate') IS NULL
    ALTER TABLE dbo.Procurement_SupplierOffer ADD RequestedDeliveryDate DATETIME NULL;
GO

IF OBJECT_ID('dbo.Procurement_AttritionRule', 'U') IS NULL
    CREATE TABLE dbo.Procurement_AttritionRule (
        Id INT IDENTITY(1, 1) NOT NULL,
        AppliesTo NVARCHAR(100) NOT NULL,
        MinUnitPrice DECIMAL(18, 4) NULL,
        MaxUnitPrice DECIMAL(18, 4) NULL,
        Percentage DECIMAL(9, 4) NOT NULL,
        MinimumExtra INT NOT NULL,
        CONSTRAINT [PK_dbo.Procurement_AttritionRule] PRIMARY KEY (Id)
    );
GO
";

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
    ('Procurement_ApprovalPolicy'), ('Procurement_AttritionRule');

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
