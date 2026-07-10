using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Interfaces;
using Dapper;

namespace BitvavoBot.Data.Repositories;

public sealed class CandleRepository : ICandleRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public CandleRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddRangeAsync(IEnumerable<Candle> candles, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO Candles (Market, Timeframe, OpenTime, Open, High, Low, Close, Volume)
            VALUES (@Market, @Timeframe, @OpenTime, @Open, @High, @Low, @Close, @Volume)
            ON CONFLICT(Market, Timeframe, OpenTime) DO UPDATE SET
                Open = excluded.Open, High = excluded.High, Low = excluded.Low,
                Close = excluded.Close, Volume = excluded.Volume;
            """;

        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();
        var command = new CommandDefinition(sql, candles, transaction, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
        transaction.Commit();
    }

    public async Task<IReadOnlyList<Candle>> GetAsync(string market, string timeframe, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT * FROM Candles
            WHERE Market = @market AND Timeframe = @timeframe AND OpenTime >= @from AND OpenTime <= @to
            ORDER BY OpenTime;
            """;
        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { market, timeframe, from, to }, cancellationToken: cancellationToken);
        var result = await connection.QueryAsync<Candle>(command);
        return result.AsList();
    }
}
