using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using Dapper;

namespace BitvavoBot.Data.Repositories;

public sealed class LogRepository : ILogRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public LogRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO LogEntries (Timestamp, Type, Message, Mode, Market, Source)
            VALUES (@Timestamp, @Type, @Message, @Mode, @Market, @Source);
            SELECT last_insert_rowid();
            """;

        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, entry, cancellationToken: cancellationToken);
        entry.Id = await connection.ExecuteScalarAsync<long>(command);
    }

    public async Task<IReadOnlyList<LogEntry>> GetRecentAsync(int count = 500, LogEntryType? type = null, TradingMode? mode = null, CancellationToken cancellationToken = default)
    {
        var sql = "SELECT * FROM LogEntries WHERE 1 = 1";
        if (type.HasValue) sql += " AND Type = @type";
        if (mode.HasValue) sql += " AND Mode = @mode";
        sql += " ORDER BY Timestamp DESC LIMIT @count;";

        using var connection = _connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { type, mode, count }, cancellationToken: cancellationToken);
        var result = await connection.QueryAsync<LogEntry>(command);
        return result.AsList();
    }
}
