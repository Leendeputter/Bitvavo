using BitvavoBot.App.Views;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Exchange.Bitvavo;
using Microsoft.Extensions.DependencyInjection;

namespace BitvavoBot.App;

/// <summary>
/// Application shell: left navigation menu (functional spec 2.1), a content panel that swaps in
/// the selected screen's UserControl, and a permanent status bar (spec 2.2).
/// </summary>
public sealed class MainForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAppModeService _appModeService;
    private readonly IBotOrchestrator _botOrchestrator;
    private readonly BitvavoExchangeClient _liveMarketData;

    private readonly ListBox _navigation = new();
    private readonly Panel _content = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _modeLabel = new();
    private readonly ToolStripStatusLabel _connectionLabel = new();
    private readonly ToolStripStatusLabel _activeProfileLabel = new();
    private readonly ToolStripStatusLabel _lastSyncLabel = new();

    private readonly Dictionary<string, Control> _viewCache = new();

    private static readonly (string Label, Func<MainForm, Control> Factory)[] MenuItems =
    {
        ("Dashboard", f => f.GetOrCreate<DashboardView>("Dashboard")),
        ("Marktmonitor", f => f.GetOrCreate<MarketMonitorView>("Marktmonitor")),
        ("Trading Bot", f => f.GetOrCreate<TradingBotView>("Trading Bot")),
        ("Open Orders", f => f.GetOrCreate<OpenOrdersView>("Open Orders")),
        ("Posities", f => f.GetOrCreate<PositionsView>("Posities")),
        ("Leverage Trading", f => f.GetOrCreate<LeverageTradingView>("Leverage Trading")),
        ("Strategieën", f => f.GetOrCreate<StrategiesView>("Strategieën")),
        ("Papertrading", f => f.GetOrCreate<PapertradingView>("Papertrading")),
        ("Logging", f => f.GetOrCreate<LoggingView>("Logging")),
        ("Instellingen", f => f.GetOrCreate<SettingsView>("Instellingen")),
    };

    public MainForm(IServiceProvider serviceProvider, IAppModeService appModeService, IBotOrchestrator botOrchestrator, BitvavoExchangeClient liveMarketData)
    {
        this.ApplyStandardAutoScale();

        _serviceProvider = serviceProvider;
        _appModeService = appModeService;
        _botOrchestrator = botOrchestrator;
        _liveMarketData = liveMarketData;

        Text = "Bitvavo Trading Bot";
        Width = 1280;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;

        BuildLayout();
        BuildStatusBar();

        _appModeService.ModeChanged += (_, mode) => UpdateModeLabel(mode);
        _liveMarketData.ConnectionStatusChanged += (_, status) => this.SafeBeginInvoke(() => UpdateConnectionLabel(status));
        UpdateModeLabel(_appModeService.CurrentMode);
        UpdateConnectionLabel(_liveMarketData.ConnectionStatus);

        _ = _liveMarketData.ConnectAsync();

        _navigation.SelectedIndex = 0;
    }

    private void BuildLayout()
    {
        _navigation.Dock = DockStyle.Left;
        _navigation.Width = 200;
        _navigation.Font = new Font(_navigation.Font.FontFamily, 11f);
        _navigation.IntegralHeight = false;
        foreach (var item in MenuItems)
        {
            _navigation.Items.Add(item.Label);
        }
        _navigation.SelectedIndexChanged += (_, _) => ShowSelectedView();

        _content.Dock = DockStyle.Fill;

        Controls.Add(_content);
        Controls.Add(_navigation);
    }

    private void BuildStatusBar()
    {
        _statusStrip.Items.Add(_modeLabel);
        _statusStrip.Items.Add(new ToolStripSeparator());
        _statusStrip.Items.Add(_connectionLabel);
        _statusStrip.Items.Add(new ToolStripSeparator());
        _statusStrip.Items.Add(_activeProfileLabel);
        _statusStrip.Items.Add(new ToolStripSeparator());
        _statusStrip.Items.Add(_lastSyncLabel);

        _botOrchestrator.StateChanged += (_, e) => this.SafeBeginInvoke(UpdateActiveProfileLabel);
        UpdateActiveProfileLabel();

        var syncTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        syncTimer.Tick += (_, _) => _lastSyncLabel.Text = $"Laatste sync: {DateTime.Now:HH:mm:ss}";
        syncTimer.Start();

        Controls.Add(_statusStrip);
    }

    private void UpdateModeLabel(TradingMode mode)
    {
        _modeLabel.Text = mode == TradingMode.Live ? "LIVE" : "PAPERTRADING";
        _modeLabel.ForeColor = Color.White;
        _modeLabel.BackColor = mode == TradingMode.Live ? Color.Firebrick : Color.RoyalBlue;
    }

    private void UpdateConnectionLabel(ConnectionStatus status)
    {
        _connectionLabel.Text = status switch
        {
            ConnectionStatus.Connected => "WebSocket: Verbonden",
            ConnectionStatus.Connecting => "WebSocket: Verbinden...",
            ConnectionStatus.Reconnecting => "WebSocket: Reconnecting...",
            ConnectionStatus.FallbackPolling => "Fallback naar polling — WebSocket verbroken",
            _ => "WebSocket: Losgekoppeld"
        };
    }

    private void UpdateActiveProfileLabel()
    {
        var running = _botOrchestrator.RunningProfiles.Where(kv => kv.Value != BotState.Stopped).ToList();
        _activeProfileLabel.Text = running.Count == 0
            ? "Geen actief profiel"
            : string.Join(", ", running.Select(kv => $"{kv.Key} ({kv.Value})"));
    }

    private void ShowSelectedView()
    {
        var index = _navigation.SelectedIndex;
        if (index < 0) return;

        _content.Controls.Clear();
        var view = MenuItems[index].Factory(this);
        view.Dock = DockStyle.Fill;
        _content.Controls.Add(view);
    }

    private Control GetOrCreate<TView>(string key) where TView : Control
    {
        if (_viewCache.TryGetValue(key, out var cached)) return cached;
        var view = ActivatorUtilities.CreateInstance<TView>(_serviceProvider);
        _viewCache[key] = view;
        return view;
    }
}
