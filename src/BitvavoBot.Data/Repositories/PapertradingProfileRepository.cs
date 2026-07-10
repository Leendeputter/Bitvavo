using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using Dapper;

namespace BitvavoBot.Data.Repositories;

public sealed class PapertradingProfileRepository : IPapertradingProfileRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public PapertradingProfileRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PapertradingProfile> AddAsync(PapertradingProfile profile, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO PapertradingProfiles (Name, QuoteCurrency, StartingBalance, CurrentBalance, SlippagePercentage, CreatedAt, ResetAt)
            VALUES (@Name, @QuoteCurrency, @StartingBalance, @CurrentBalance, @SlippagePercentage, @CreatedAt, @ResetAt);
            SELECT last_insert_rowid();
            """;
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, profile, cancellationToken: cancellationToken);
        profile.Id = await connection.ExecuteScalarAsync<long>(command);
        return profile;
    }

    public async Task UpdateAsync(PapertradingProfile profile, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE PapertradingProfiles SET
                CurrentBalance = @CurrentBalance, SlippagePercentage = @SlippagePercentage, ResetAt = @ResetAt
            WHERE Id = @Id;
            """;
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, profile, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<PapertradingProfile?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM PapertradingProfiles WHERE Name = @name;";
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { name }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<PapertradingProfile>(command);
    }

    public async Task<IReadOnlyList<PapertradingProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM PapertradingProfiles ORDER BY Name;";
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var result = await connection.QueryAsync<PapertradingProfile>(command);
        return result.AsList();
    }

    /// <summary>Resets balance, orders, positions and history of this profile back to its initial state.</summary>
    public async Task ResetAsync(long profileId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        var profile = await connection.QuerySingleAsync<PapertradingProfile>(
            new CommandDefinition("SELECT * FROM PapertradingProfiles WHERE Id = @profileId;", new { profileId }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM Trades WHERE Mode = 1 AND OrderId IN (
                SELECT Id FROM Orders WHERE Mode = 1 AND PapertradingProfileName = @name);
            DELETE FROM Orders WHERE Mode = 1 AND PapertradingProfileName = @name;
            DELETE FROM Positions WHERE Mode = 1 AND PapertradingProfileName = @name;
            """,
            new { profile.Name }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE PapertradingProfiles SET CurrentBalance = StartingBalance, ResetAt = @resetAt WHERE Id = @profileId;",
            new { resetAt = DateTimeOffset.UtcNow, profileId }, transaction, cancellationToken: cancellationToken));

        transaction.Commit();
    }
}
