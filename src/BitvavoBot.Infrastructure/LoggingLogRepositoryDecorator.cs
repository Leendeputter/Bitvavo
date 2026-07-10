using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using Serilog;

namespace BitvavoBot.Infrastructure;

/// <summary>
/// Wraps the database-backed <see cref="ILogRepository"/> so every business log entry (order
/// placed, risk warning, daily-limit stop, ...) is also mirrored to the Serilog file sink,
/// giving both a queryable "Logging" screen and a durable technical log trail.
/// </summary>
public sealed class LoggingLogRepositoryDecorator : ILogRepository
{
    private readonly ILogRepository _inner;
    private readonly ILogger _logger;

    public LoggingLogRepositoryDecorator(ILogRepository inner, ILogger logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task AddAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        await _inner.AddAsync(entry, cancellationToken);

        var modeTag = entry.Mode?.ToString() ?? "-";
        switch (entry.Type)
        {
            case LogEntryType.Error:
                _logger.Error("[{Mode}] [{Source}] {Message}", modeTag, entry.Source, entry.Message);
                break;
            case LogEntryType.Warning:
                _logger.Warning("[{Mode}] [{Source}] {Message}", modeTag, entry.Source, entry.Message);
                break;
            default:
                _logger.Information("[{Mode}] [{Source}] {Message}", modeTag, entry.Source, entry.Message);
                break;
        }
    }

    public Task<IReadOnlyList<LogEntry>> GetRecentAsync(int count = 500, LogEntryType? type = null, TradingMode? mode = null, CancellationToken cancellationToken = default) =>
        _inner.GetRecentAsync(count, type, mode, cancellationToken);
}
