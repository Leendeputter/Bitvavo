using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Interfaces;
using Dapper;

namespace BitvavoBot.Data.Repositories;

public sealed class BotProfileRepository : IBotProfileRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public BotProfileRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<BotProfile> AddAsync(BotProfile profile, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO BotProfiles
                (Name, TradeAmountMode, TradeAmountValue, Markets, ScrapeIntervalSeconds,
                 BuyMarginPercentage, SellMarginPercentage, MinOpenOrdersPercentage, AutoReplenishOpenOrders,
                 MaxTradesPerDay, MaxLossPerDay, MaxInvestment, StopLossPercentage, TakeProfitPercentage,
                 MaxConcurrentOrders, WebSocketFallbackSeconds, Mode, PapertradingProfileName, State,
                 CreatedAt, UpdatedAt)
            VALUES
                (@Name, @TradeAmountMode, @TradeAmountValue, @Markets, @ScrapeIntervalSeconds,
                 @BuyMarginPercentage, @SellMarginPercentage, @MinOpenOrdersPercentage, @AutoReplenishOpenOrders,
                 @MaxTradesPerDay, @MaxLossPerDay, @MaxInvestment, @StopLossPercentage, @TakeProfitPercentage,
                 @MaxConcurrentOrders, @WebSocketFallbackSeconds, @Mode, @PapertradingProfileName, @State,
                 @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();
            """;

        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, profile, cancellationToken: cancellationToken);
        profile.Id = await connection.ExecuteScalarAsync<long>(command);
        return profile;
    }

    public async Task UpdateAsync(BotProfile profile, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE BotProfiles SET
                TradeAmountMode = @TradeAmountMode, TradeAmountValue = @TradeAmountValue, Markets = @Markets,
                ScrapeIntervalSeconds = @ScrapeIntervalSeconds, BuyMarginPercentage = @BuyMarginPercentage,
                SellMarginPercentage = @SellMarginPercentage, MinOpenOrdersPercentage = @MinOpenOrdersPercentage,
                AutoReplenishOpenOrders = @AutoReplenishOpenOrders, MaxTradesPerDay = @MaxTradesPerDay,
                MaxLossPerDay = @MaxLossPerDay, MaxInvestment = @MaxInvestment, StopLossPercentage = @StopLossPercentage,
                TakeProfitPercentage = @TakeProfitPercentage, MaxConcurrentOrders = @MaxConcurrentOrders,
                WebSocketFallbackSeconds = @WebSocketFallbackSeconds, Mode = @Mode,
                PapertradingProfileName = @PapertradingProfileName, State = @State, UpdatedAt = @UpdatedAt
            WHERE Id = @Id;
            """;

        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, profile, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<BotProfile?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM BotProfiles WHERE Name = @name;";
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { name }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<BotProfile>(command);
    }

    public async Task<IReadOnlyList<BotProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM BotProfiles ORDER BY Name;";
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var result = await connection.QueryAsync<BotProfile>(command);
        return result.AsList();
    }

    public async Task<IReadOnlyList<StrategyAssignment>> GetStrategyAssignmentsAsync(long botProfileId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM StrategyAssignments WHERE BotProfileId = @botProfileId;";
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { botProfileId }, cancellationToken: cancellationToken);
        var result = await connection.QueryAsync<StrategyAssignment>(command);
        return result.AsList();
    }

    public async Task SaveStrategyAssignmentsAsync(long botProfileId, IReadOnlyList<StrategyAssignment> assignments, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM StrategyAssignments WHERE BotProfileId = @botProfileId;",
            new { botProfileId }, transaction, cancellationToken: cancellationToken));

        const string insertSql = """
            INSERT INTO StrategyAssignments (BotProfileId, Market, StrategyType, ParametersJson)
            VALUES (@BotProfileId, @Market, @StrategyType, @ParametersJson);
            """;

        foreach (var assignment in assignments)
        {
            assignment.BotProfileId = botProfileId;
            await connection.ExecuteAsync(new CommandDefinition(insertSql, assignment, transaction, cancellationToken: cancellationToken));
        }

        transaction.Commit();
    }
}
