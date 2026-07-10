using System.Reflection;
using Microsoft.Data.Sqlite;

namespace BitvavoBot.Data.Migrations;

/// <summary>
/// Applies embedded, numbered .sql migration scripts (Sql/NNN_name.sql) in order, tracking which
/// ones already ran in a SchemaMigrations table so restarts are idempotent.
/// </summary>
public sealed class DatabaseMigrator
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public DatabaseMigrator(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Migrate()
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using (var createTable = connection.CreateCommand())
        {
            createTable.CommandText = """
                CREATE TABLE IF NOT EXISTS SchemaMigrations (
                    Name TEXT PRIMARY KEY,
                    AppliedAt TEXT NOT NULL
                );
                """;
            createTable.ExecuteNonQuery();
        }

        var applied = new HashSet<string>();
        using (var select = connection.CreateCommand())
        {
            select.CommandText = "SELECT Name FROM SchemaMigrations;";
            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                applied.Add(reader.GetString(0));
            }
        }

        foreach (var (name, sql) in GetEmbeddedMigrations())
        {
            if (applied.Contains(name)) continue;

            using var transaction = connection.BeginTransaction();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }

            using (var recordApplied = connection.CreateCommand())
            {
                recordApplied.Transaction = transaction;
                recordApplied.CommandText = "INSERT INTO SchemaMigrations (Name, AppliedAt) VALUES ($name, $appliedAt);";
                recordApplied.Parameters.AddWithValue("$name", name);
                recordApplied.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O"));
                recordApplied.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    private static IEnumerable<(string Name, string Sql)> GetEmbeddedMigrations()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Sql.") && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal);

        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded migration resource not found: {resourceName}");
            using var reader = new StreamReader(stream);
            yield return (resourceName, reader.ReadToEnd());
        }
    }
}
