using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using Dapper;

namespace BitvavoBot.Data.Repositories;

public sealed class StrategyRunRepository : IStrategyRunRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public StrategyRunRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<StrategyRun> AddAsync(StrategyRun run, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO StrategyRuns
                (Mode, StrategyType, StrategyName, BotProfileName, ParametersJson, Markets,
                 StartedAt, EndedAt, TotalTrades, WinningTrades, TotalProfitLoss, ReturnPercentage)
            VALUES
                (@Mode, @StrategyType, @StrategyName, @BotProfileName, @ParametersJson, @Markets,
                 @StartedAt, @EndedAt, @TotalTrades, @WinningTrades, @TotalProfitLoss, @ReturnPercentage);
            SELECT last_insert_rowid();
            """;

        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, run, cancellationToken: cancellationToken);
        run.Id = await connection.ExecuteScalarAsync<long>(command);
        return run;
    }

    public async Task UpdateAsync(StrategyRun run, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE StrategyRuns SET
                EndedAt = @EndedAt, TotalTrades = @TotalTrades, WinningTrades = @WinningTrades,
                TotalProfitLoss = @TotalProfitLoss, ReturnPercentage = @ReturnPercentage
            WHERE Id = @Id;
            """;
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, run, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<IReadOnlyList<StrategyRun>> GetAllAsync(TradingMode? mode = null, CancellationToken cancellationToken = default)
    {
        var sql = "SELECT * FROM StrategyRuns";
        if (mode.HasValue) sql += " WHERE Mode = @mode";
        sql += " ORDER BY StartedAt DESC;";

        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { mode }, cancellationToken: cancellationToken);
        var result = await connection.QueryAsync<StrategyRun>(command);
        return result.AsList();
    }
}
