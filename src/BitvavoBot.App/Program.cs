using BitvavoBot.App.Composition;
using BitvavoBot.Data;
using BitvavoBot.Data.Migrations;
using BitvavoBot.Data.Repositories;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Exchange.Bitvavo;
using BitvavoBot.Infrastructure;
using BitvavoBot.Trading;
using BitvavoBot.Trading.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace BitvavoBot.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        Directory.CreateDirectory(AppPaths.DataDirectory);
        DapperTypeHandlers.RegisterAll();

        var connectionFactory = new SqliteConnectionFactory(AppPaths.DatabasePath);
        new DatabaseMigrator(connectionFactory).Migrate();

        var logger = SerilogConfigurator.CreateLogger(AppPaths.LogDirectory);
        Serilog.Log.Logger = logger;
        var credentialProtector = new DpapiCredentialProtector();
        var settingsService = new JsonSettingsService(AppPaths.SettingsPath);
        var settings = settingsService.LoadAsync().GetAwaiter().GetResult();

        var bitvavoOptions = new BitvavoOptions
        {
            ApiKey = string.IsNullOrEmpty(settings.BitvavoApiKeyEncrypted) ? string.Empty : credentialProtector.Unprotect(settings.BitvavoApiKeyEncrypted),
            ApiSecret = string.IsNullOrEmpty(settings.BitvavoApiSecretEncrypted) ? string.Empty : credentialProtector.Unprotect(settings.BitvavoApiSecretEncrypted)
        };

        var services = new ServiceCollection();

        services.AddSingleton(connectionFactory);
        services.AddSingleton<ISettingsService>(settingsService);
        services.AddSingleton<ICredentialProtector>(credentialProtector);
        services.AddSingleton(logger);

        services.AddSingleton<IOrderRepository, OrderRepository>();
        services.AddSingleton<ITradeRepository, TradeRepository>();
        services.AddSingleton<IPositionRepository, PositionRepository>();
        services.AddSingleton<ICandleRepository, CandleRepository>();
        services.AddSingleton<IStrategyRunRepository, StrategyRunRepository>();
        services.AddSingleton<IPapertradingProfileRepository, PapertradingProfileRepository>();
        services.AddSingleton<IBotProfileRepository, BotProfileRepository>();
        services.AddSingleton<ILogRepository>(sp => new LoggingLogRepositoryDecorator(new LogRepository(connectionFactory), logger));

        services.AddSingleton(bitvavoOptions);
        services.AddSingleton(sp => new BitvavoRestClient(new HttpClient(), sp.GetRequiredService<BitvavoOptions>()));
        services.AddSingleton(sp => new BitvavoWebSocketClient(sp.GetRequiredService<BitvavoOptions>()));
        services.AddSingleton(sp => new BitvavoExchangeClient(
            sp.GetRequiredService<BitvavoRestClient>(), sp.GetRequiredService<BitvavoWebSocketClient>(),
            settings.MarketMonitorPollingIntervalSeconds));

        services.AddSingleton<ExchangeClientFactory>();
        services.AddSingleton<IExchangeClientFactory>(sp => sp.GetRequiredService<ExchangeClientFactory>());

        services.AddSingleton<IRiskEngine, RiskEngine>();
        services.AddSingleton<ITradingEngine, TradingEngine>();
        services.AddSingleton<IStrategyFactory, StrategyFactory>();
        services.AddSingleton<IBotOrchestrator, BotOrchestrator>();
        services.AddSingleton<IAppModeService>(sp => new AppModeService(sp.GetRequiredService<IBotOrchestrator>()));

        services.AddSingleton<MainForm>();

        using var provider = services.BuildServiceProvider();

        try
        {
            Application.Run(provider.GetRequiredService<MainForm>());
        }
        finally
        {
            Serilog.Log.CloseAndFlush();
        }
    }
}
