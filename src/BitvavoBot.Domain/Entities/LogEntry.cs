using BitvavoBot.Domain.Enums;

namespace BitvavoBot.Domain.Entities;

public class LogEntry
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public LogEntryType Type { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>Set when the log line is trade/order related; null for general Info/Warning/Error lines.</summary>
    public TradingMode? Mode { get; set; }
    public string? Market { get; set; }
    public string? Source { get; set; }
}
