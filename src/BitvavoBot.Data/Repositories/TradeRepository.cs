using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using Dapper;

namespace BitvavoBot.Data.Repositories;

public sealed class TradeRepository : ITradeRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public TradeRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Trade> AddAsync(Trade trade, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO Trades (OrderId, Mode, Market, Side, Price, Amount, Fee, FeeCurrency, ProfitLoss, Timestamp)
            VALUES (@OrderId, @Mode, @Market, @Side, @Price, @Amount, @Fee, @FeeCurrency, @ProfitLoss, @Timestamp);
            SELECT last_insert_rowid();
            """;

        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, trade, cancellationToken: cancellationToken);
        trade.Id = await connection.ExecuteScalarAsync<long>(command);
        return trade;
    }

    public async Task<IReadOnlyList<Trade>> GetAllAsync(TradingMode mode, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default)
    {
        var sql = "SELECT * FROM Trades WHERE Mode = @mode";
        if (from.HasValue) sql += " AND Timestamp >= @from";
        if (to.HasValue) sql += " AND Timestamp <= @to";
        sql += " ORDER BY Timestamp DESC;";

        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { mode, from, to }, cancellationToken: cancellationToken);
        var result = await connection.QueryAsync<Trade>(command);
        return result.AsList();
    }

    public async Task<int> CountTradesTodayAsync(TradingMode mode, string botProfileName, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(*) FROM Trades t
            JOIN Orders o ON o.Id = t.OrderId
            WHERE t.Mode = @mode AND o.BotProfileName = @botProfileName
              AND date(t.Timestamp) = date('now', 'localtime');
            """;
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { mode, botProfileName }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<decimal> GetRealizedPnLTodayAsync(TradingMode mode, string botProfileName, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT t.ProfitLoss FROM Trades t
            JOIN Orders o ON o.Id = t.OrderId
            WHERE t.Mode = @mode AND o.BotProfileName = @botProfileName
              AND date(t.Timestamp) = date('now', 'localtime')
              AND t.ProfitLoss IS NOT NULL;
            """;
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { mode, botProfileName }, cancellationToken: cancellationToken);
        var values = await connection.QueryAsync<decimal>(command);
        return values.Sum();
    }
}
