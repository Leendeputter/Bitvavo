using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using Dapper;

namespace BitvavoBot.Data.Repositories;

public sealed class PositionRepository : IPositionRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public PositionRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Position?> GetOpenPositionAsync(TradingMode mode, string asset, string? papertradingProfileName = null, CancellationToken cancellationToken = default)
    {
        var sql = "SELECT * FROM Positions WHERE Mode = @mode AND Asset = @asset AND ClosedAt IS NULL";
        sql += mode == TradingMode.Paper ? " AND PapertradingProfileName = @papertradingProfileName" : "";
        sql += ";";
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { mode, asset, papertradingProfileName }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Position>(command);
    }

    public async Task<IReadOnlyList<Position>> GetOpenPositionsAsync(TradingMode mode, string? papertradingProfileName = null, CancellationToken cancellationToken = default)
    {
        var sql = "SELECT * FROM Positions WHERE Mode = @mode AND ClosedAt IS NULL";
        sql += mode == TradingMode.Paper ? " AND PapertradingProfileName = @papertradingProfileName" : "";
        sql += " ORDER BY Asset;";
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { mode, papertradingProfileName }, cancellationToken: cancellationToken);
        var result = await connection.QueryAsync<Position>(command);
        return result.AsList();
    }

    public async Task UpsertAsync(Position position, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        if (position.Id == 0)
        {
            const string insertSql = """
                INSERT INTO Positions
                    (Mode, Market, Asset, PapertradingProfileName, Amount, AverageEntryPrice, Side, Leverage, MarginType,
                     LiquidationPrice, OpenedAt, UpdatedAt, ClosedAt)
                VALUES
                    (@Mode, @Market, @Asset, @PapertradingProfileName, @Amount, @AverageEntryPrice, @Side, @Leverage, @MarginType,
                     @LiquidationPrice, @OpenedAt, @UpdatedAt, @ClosedAt);
                SELECT last_insert_rowid();
                """;
            var command = new CommandDefinition(insertSql, position, cancellationToken: cancellationToken);
            position.Id = await connection.ExecuteScalarAsync<long>(command);
        }
        else
        {
            const string updateSql = """
                UPDATE Positions SET
                    Amount = @Amount, AverageEntryPrice = @AverageEntryPrice, LiquidationPrice = @LiquidationPrice,
                    UpdatedAt = @UpdatedAt, ClosedAt = @ClosedAt
                WHERE Id = @Id;
                """;
            var command = new CommandDefinition(updateSql, position, cancellationToken: cancellationToken);
            await connection.ExecuteAsync(command);
        }
    }
}
