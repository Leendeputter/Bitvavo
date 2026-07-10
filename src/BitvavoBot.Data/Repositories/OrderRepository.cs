using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using Dapper;

namespace BitvavoBot.Data.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public OrderRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO Orders
                (ExternalId, Mode, Market, Side, Type, Status, Price, Amount, FilledAmount,
                 AverageFillPrice, FeePaid, FeeCurrency, BotProfileName, StrategyName, PapertradingProfileName,
                 CreatedAt, UpdatedAt, FilledAt, CancelledAt)
            VALUES
                (@ExternalId, @Mode, @Market, @Side, @Type, @Status, @Price, @Amount, @FilledAmount,
                 @AverageFillPrice, @FeePaid, @FeeCurrency, @BotProfileName, @StrategyName, @PapertradingProfileName,
                 @CreatedAt, @UpdatedAt, @FilledAt, @CancelledAt);
            SELECT last_insert_rowid();
            """;

        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, order, cancellationToken: cancellationToken);
        order.Id = await connection.ExecuteScalarAsync<long>(command);
        return order;
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Orders SET
                Status = @Status, FilledAmount = @FilledAmount, AverageFillPrice = @AverageFillPrice,
                FeePaid = @FeePaid, UpdatedAt = @UpdatedAt, FilledAt = @FilledAt, CancelledAt = @CancelledAt
            WHERE Id = @Id;
            """;

        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, order, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<Order?> GetByExternalIdAsync(TradingMode mode, string externalId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Orders WHERE Mode = @mode AND ExternalId = @externalId;";
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { mode, externalId }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Order>(command);
    }

    public async Task<IReadOnlyList<Order>> GetOpenOrdersAsync(TradingMode mode, string? botProfileName = null, string? papertradingProfileName = null, CancellationToken cancellationToken = default)
    {
        var sql = "SELECT * FROM Orders WHERE Mode = @mode AND Status IN (0, 1, 2)";
        if (!string.IsNullOrEmpty(botProfileName)) sql += " AND BotProfileName = @botProfileName";
        if (!string.IsNullOrEmpty(papertradingProfileName)) sql += " AND PapertradingProfileName = @papertradingProfileName";
        sql += " ORDER BY CreatedAt DESC;";

        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { mode, botProfileName, papertradingProfileName }, cancellationToken: cancellationToken);
        var result = await connection.QueryAsync<Order>(command);
        return result.AsList();
    }

    public async Task<IReadOnlyList<Order>> GetAllAsync(TradingMode mode, string? botProfileName = null, string? papertradingProfileName = null, CancellationToken cancellationToken = default)
    {
        var sql = "SELECT * FROM Orders WHERE Mode = @mode";
        if (!string.IsNullOrEmpty(botProfileName)) sql += " AND BotProfileName = @botProfileName";
        if (!string.IsNullOrEmpty(papertradingProfileName)) sql += " AND PapertradingProfileName = @papertradingProfileName";
        sql += " ORDER BY CreatedAt DESC;";

        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { mode, botProfileName, papertradingProfileName }, cancellationToken: cancellationToken);
        var result = await connection.QueryAsync<Order>(command);
        return result.AsList();
    }

    public async Task<int> CountActiveForProfileAsync(TradingMode mode, string botProfileName, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM Orders WHERE Mode = @mode AND BotProfileName = @botProfileName;";
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { mode, botProfileName }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<int> CountOpenForProfileAsync(TradingMode mode, string botProfileName, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM Orders WHERE Mode = @mode AND BotProfileName = @botProfileName AND Status IN (0, 1, 2);";
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { mode, botProfileName }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command);
    }
}
