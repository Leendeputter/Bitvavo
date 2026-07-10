using Serilog;
using Serilog.Events;

namespace BitvavoBot.Infrastructure;

public static class SerilogConfigurator
{
    /// <summary>Builds the application's diagnostic (technical) logger: rolling daily files plus console in Debug builds.</summary>
    public static ILogger CreateLogger(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);

        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logDirectory, "bitvavobot-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                restrictedToMinimumLevel: LogEventLevel.Information);

#if DEBUG
        configuration = configuration.WriteTo.Console();
#endif

        return configuration.CreateLogger();
    }
}
