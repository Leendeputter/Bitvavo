using System.Text;
using BitvavoBot.App.Dialogs;
using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;

namespace BitvavoBot.App.Views;

/// <summary>Papertrading profile overview: balance, results summary, reset and CSV export (functional spec 8.5).</summary>
public sealed class PapertradingView : UserControl
{
    private readonly IPapertradingProfileRepository _papertradingProfileRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly ITradeRepository _tradeRepository;

    private readonly ComboBox _profileCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly Button _newProfileButton = new() { Text = "Nieuw profiel" };
    private readonly Button _resetButton = new() { Text = "Reset profiel" };
    private readonly Button _exportButton = new() { Text = "Exporteer historie (CSV)" };

    private readonly Label _balanceValue = new() { AutoSize = true };
    private readonly Label _openOrdersValue = new() { AutoSize = true };
    private readonly Label _openPositionsValue = new() { AutoSize = true };
    private readonly Label _returnValue = new() { AutoSize = true };
    private readonly Label _tradeCountValue = new() { AutoSize = true };
    private readonly Label _winRateValue = new() { AutoSize = true };
    private readonly Label _avgWinLossValue = new() { AutoSize = true };

    public PapertradingView(
        IPapertradingProfileRepository papertradingProfileRepository, IOrderRepository orderRepository,
        IPositionRepository positionRepository, ITradeRepository tradeRepository)
    {
        this.ApplyStandardAutoScale();

        _papertradingProfileRepository = papertradingProfileRepository;
        _orderRepository = orderRepository;
        _positionRepository = positionRepository;
        _tradeRepository = tradeRepository;

        BuildLayout();
        this.ApplyReadableButtonSizing();

        _profileCombo.SelectedIndexChanged += async (_, _) => await RefreshSummaryAsync();
        _newProfileButton.Click += async (_, _) => await CreateProfileAsync();
        _resetButton.Click += async (_, _) => await ResetProfileAsync();
        _exportButton.Click += async (_, _) => await ExportCsvAsync();

        Load += async (_, _) => await LoadProfilesAsync();
        VisibleChanged += async (_, _) => { if (Visible) await LoadProfilesAsync(); };
    }

    private void BuildLayout()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(16);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        toolbar.Controls.Add(new Label { Text = "Profiel:", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        toolbar.Controls.Add(_profileCombo);
        toolbar.Controls.Add(_newProfileButton);
        toolbar.Controls.Add(_resetButton);
        toolbar.Controls.Add(_exportButton);

        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 16, 0, 0) };
        void AddRow(string label, Control value)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold) });
            layout.Controls.Add(value);
        }
        AddRow("Huidig gesimuleerd saldo:", _balanceValue);
        AddRow("Open paper-orders:", _openOrdersValue);
        AddRow("Open paper-posities:", _openPositionsValue);
        AddRow("Totaal rendement %:", _returnValue);
        AddRow("Aantal trades:", _tradeCountValue);
        AddRow("Winrate:", _winRateValue);
        AddRow("Gem. winst/verlies per trade:", _avgWinLossValue);

        Controls.Add(layout);
        Controls.Add(toolbar);
    }

    private async Task LoadProfilesAsync()
    {
        var selected = _profileCombo.SelectedItem as string;
        var profiles = await _papertradingProfileRepository.GetAllAsync();

        _profileCombo.Items.Clear();
        foreach (var profile in profiles) _profileCombo.Items.Add(profile.Name);

        if (selected is not null && _profileCombo.Items.Contains(selected)) _profileCombo.SelectedItem = selected;
        else if (_profileCombo.Items.Count > 0) _profileCombo.SelectedIndex = 0;
        else await RefreshSummaryAsync();
    }

    private async Task CreateProfileAsync()
    {
        using var dialog = new PapertradingProfileEditForm(null);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        await _papertradingProfileRepository.AddAsync(new PapertradingProfile
        {
            Name = dialog.ProfileName,
            QuoteCurrency = "EUR",
            StartingBalance = dialog.StartingBalance,
            CurrentBalance = dialog.StartingBalance,
            SlippagePercentage = dialog.SlippagePercentage,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await LoadProfilesAsync();
        _profileCombo.SelectedItem = dialog.ProfileName;
    }

    private async Task ResetProfileAsync()
    {
        if (_profileCombo.SelectedItem is not string name) return;
        var profile = await _papertradingProfileRepository.GetByNameAsync(name);
        if (profile is null) return;

        var confirm = MessageBox.Show(this,
            $"Weet u zeker dat u profiel '{name}' wilt resetten? Saldo, orders, posities en historie gaan terug naar de beginstaat.",
            "Reset papertrading profiel", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        await _papertradingProfileRepository.ResetAsync(profile.Id);
        await RefreshSummaryAsync();
    }

    private async Task RefreshSummaryAsync()
    {
        if (_profileCombo.SelectedItem is not string name)
        {
            _balanceValue.Text = _openOrdersValue.Text = _openPositionsValue.Text = _returnValue.Text =
                _tradeCountValue.Text = _winRateValue.Text = _avgWinLossValue.Text = "-";
            return;
        }

        var profile = await _papertradingProfileRepository.GetByNameAsync(name);
        if (profile is null) return;

        _balanceValue.Text = $"€ {profile.CurrentBalance:N2}";

        var openOrders = (await _orderRepository.GetOpenOrdersAsync(TradingMode.Paper, papertradingProfileName: name)).Count;
        _openOrdersValue.Text = openOrders.ToString();

        var openPositions = (await _positionRepository.GetOpenPositionsAsync(TradingMode.Paper, name)).Count;
        _openPositionsValue.Text = openPositions.ToString();

        var profileOrderIds = (await _orderRepository.GetAllAsync(TradingMode.Paper, papertradingProfileName: name))
            .Select(o => o.Id).ToHashSet();
        var trades = (await _tradeRepository.GetAllAsync(TradingMode.Paper))
            .Where(t => profileOrderIds.Contains(t.OrderId)).ToList();

        var closedTrades = trades.Where(t => t.ProfitLoss.HasValue).ToList();
        var winning = closedTrades.Count(t => t.ProfitLoss!.Value > 0);
        var returnPct = profile.StartingBalance == 0m ? 0m : (profile.CurrentBalance - profile.StartingBalance) / profile.StartingBalance * 100m;

        _returnValue.Text = $"{returnPct:N2}%";
        _tradeCountValue.Text = trades.Count.ToString();
        _winRateValue.Text = closedTrades.Count == 0 ? "n.b." : $"{(decimal)winning / closedTrades.Count * 100m:N1}% ({winning}/{closedTrades.Count})";
        _avgWinLossValue.Text = closedTrades.Count == 0 ? "n.b." : $"€ {closedTrades.Average(t => t.ProfitLoss!.Value):N2}";
    }

    private async Task ExportCsvAsync()
    {
        if (_profileCombo.SelectedItem is not string name) return;

        using var dialog = new SaveFileDialog { Filter = "CSV-bestand|*.csv", FileName = $"papertrading-{name}.csv" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var profileOrderIds = (await _orderRepository.GetAllAsync(TradingMode.Paper, papertradingProfileName: name))
            .Select(o => o.Id).ToHashSet();
        var trades = (await _tradeRepository.GetAllAsync(TradingMode.Paper))
            .Where(t => profileOrderIds.Contains(t.OrderId));

        var builder = new StringBuilder();
        builder.AppendLine("Timestamp;Market;Side;Price;Amount;Fee;ProfitLoss");
        foreach (var trade in trades)
        {
            builder.AppendLine($"{trade.Timestamp:O};{trade.Market};{trade.Side};{trade.Price};{trade.Amount};{trade.Fee};{trade.ProfitLoss}");
        }

        File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
    }
}
