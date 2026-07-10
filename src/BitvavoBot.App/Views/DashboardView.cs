using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;

namespace BitvavoBot.App.Views;

/// <summary>Startscherm summarizing balance, open orders/positions, daily P&amp;L and bot status (functional spec 2.3).</summary>
public sealed class DashboardView : UserControl
{
    private readonly IBotProfileRepository _botProfileRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IBotOrchestrator _botOrchestrator;
    private readonly IExchangeClientFactory _exchangeClientFactory;

    private readonly ComboBox _profileCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly Button _startButton = new() { Text = "Start" };
    private readonly Button _pauseButton = new() { Text = "Pauzeer" };
    private readonly Button _stopButton = new() { Text = "Stop" };
    private readonly Label _stateLabel = new() { AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold) };

    private readonly Label _liveBalanceValue = new() { AutoSize = true };
    private readonly Label _paperBalanceValue = new() { AutoSize = true };
    private readonly Label _openOrdersValue = new() { AutoSize = true };
    private readonly Label _openPositionsValue = new() { AutoSize = true };
    private readonly Label _livePnlValue = new() { AutoSize = true };
    private readonly Label _paperPnlValue = new() { AutoSize = true };
    private readonly Label _warningsValue = new() { AutoSize = true, ForeColor = Color.DarkOrange };

    public DashboardView(
        IBotProfileRepository botProfileRepository, IOrderRepository orderRepository, IPositionRepository positionRepository,
        ITradeRepository tradeRepository, IBotOrchestrator botOrchestrator, IExchangeClientFactory exchangeClientFactory)
    {
        this.ApplyStandardAutoScale();

        _botProfileRepository = botProfileRepository;
        _orderRepository = orderRepository;
        _positionRepository = positionRepository;
        _tradeRepository = tradeRepository;
        _botOrchestrator = botOrchestrator;
        _exchangeClientFactory = exchangeClientFactory;

        BuildLayout();
        this.ApplyReadableButtonSizing();

        _startButton.Click += async (_, _) => await SafeAsync(async () =>
        {
            if (_profileCombo.SelectedItem is string name) await _botOrchestrator.StartAsync(name);
        });
        _pauseButton.Click += async (_, _) => await SafeAsync(async () =>
        {
            if (_profileCombo.SelectedItem is string name) await _botOrchestrator.PauseAsync(name);
        });
        _stopButton.Click += async (_, _) => await SafeAsync(async () =>
        {
            if (_profileCombo.SelectedItem is string name) await _botOrchestrator.StopAsync(name);
        });
        _profileCombo.SelectedIndexChanged += (_, _) => RefreshStateLabel();
        _botOrchestrator.StateChanged += (_, _) => BeginInvoke(new MethodInvoker(RefreshStateLabel));

        Load += async (_, _) => await RefreshAsync();
        VisibleChanged += async (_, _) => { if (Visible) await RefreshAsync(); };
    }

    private void BuildLayout()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(16);

        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 8 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        void AddRow(string label, Control valueControl)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold) });
            layout.Controls.Add(valueControl);
        }

        AddRow("Live saldo:", _liveBalanceValue);
        AddRow("Papertrading saldo:", _paperBalanceValue);
        AddRow("Open orders / posities:", _openOrdersValue);
        AddRow("Open posities:", _openPositionsValue);
        AddRow("Dagelijkse P&L (Live):", _livePnlValue);
        AddRow("Dagelijkse P&L (Paper):", _paperPnlValue);
        AddRow("Waarschuwingen:", _warningsValue);

        var botPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 16, 0, 0) };
        botPanel.Controls.Add(new Label { Text = "Bot profiel:", AutoSize = true, Margin = new Padding(0, 8, 8, 0) });
        botPanel.Controls.Add(_profileCombo);
        botPanel.Controls.Add(_startButton);
        botPanel.Controls.Add(_pauseButton);
        botPanel.Controls.Add(_stopButton);
        botPanel.Controls.Add(_stateLabel);

        Controls.Add(botPanel);
        Controls.Add(layout);
    }

    private async Task RefreshAsync()
    {
        var profiles = await _botProfileRepository.GetAllAsync();
        var selected = _profileCombo.SelectedItem as string;
        _profileCombo.Items.Clear();
        foreach (var profile in profiles) _profileCombo.Items.Add(profile.Name);
        if (selected is not null && _profileCombo.Items.Contains(selected)) _profileCombo.SelectedItem = selected;
        else if (_profileCombo.Items.Count > 0) _profileCombo.SelectedIndex = 0;

        try
        {
            var liveClient = _exchangeClientFactory.GetClient(TradingMode.Live, null);
            var liveBalances = await liveClient.GetBalanceAsync();
            var eur = liveBalances.FirstOrDefault(b => b.Asset == "EUR");
            _liveBalanceValue.Text = eur is null ? "n.b." : $"€ {eur.Available:N2}";
        }
        catch
        {
            _liveBalanceValue.Text = "n.b. (geen API-koppeling)";
        }

        var openOrdersLive = await _orderRepository.GetOpenOrdersAsync(TradingMode.Live);
        var openOrdersPaper = await _orderRepository.GetOpenOrdersAsync(TradingMode.Paper);
        _openOrdersValue.Text = $"Live: {openOrdersLive.Count}  |  Paper: {openOrdersPaper.Count}";

        var openPositionsLive = await _positionRepository.GetOpenPositionsAsync(TradingMode.Live);
        _openPositionsValue.Text = $"Live: {openPositionsLive.Count} posities";

        var todayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var liveTradesToday = await _tradeRepository.GetAllAsync(TradingMode.Live, todayStart);
        _livePnlValue.Text = $"€ {liveTradesToday.Sum(t => t.ProfitLoss ?? 0m):N2}";

        var paperTradesToday = await _tradeRepository.GetAllAsync(TradingMode.Paper, todayStart);
        _paperPnlValue.Text = $"€ {paperTradesToday.Sum(t => t.ProfitLoss ?? 0m):N2}";

        _warningsValue.Text = "Geen actieve waarschuwingen";

        RefreshStateLabel();
    }

    private void RefreshStateLabel()
    {
        if (_profileCombo.SelectedItem is string name && _botOrchestrator.RunningProfiles.TryGetValue(name, out var state))
        {
            _stateLabel.Text = $"Status: {state}";
        }
        else
        {
            _stateLabel.Text = "Status: Gestopt";
        }
    }

    private async Task SafeAsync(Func<Task> action)
    {
        try { await action(); await RefreshAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
