using System.Text.Json;
using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Exchange.Bitvavo;
using BitvavoBot.Trading.Strategies;

namespace BitvavoBot.App.Dialogs;

/// <summary>
/// Bot profile configuration, implemented as one scrollable form with sections rather than a
/// literal multi-step wizard (functional spec 4.1 explicitly allows either). v1 simplification:
/// one strategy + parameter set is applied to every selected market (per-market overrides are not
/// exposed in this dialog, though the data model in <see cref="StrategyAssignment"/> supports it).
/// </summary>
public sealed class BotProfileWizardForm : Form
{
    private readonly IBotProfileRepository _botProfileRepository;
    private readonly IPapertradingProfileRepository _papertradingProfileRepository;
    private readonly BotProfile? _existing;

    private readonly TextBox _nameBox = new() { Width = 300 };
    private readonly RadioButton _fixedAmountRadio = new() { Text = "Vast bedrag (€)", Checked = true };
    private readonly RadioButton _pctAmountRadio = new() { Text = "% van saldo" };
    private readonly NumericUpDown _amountValue = new() { Maximum = 1_000_000, DecimalPlaces = 2, Value = 25 };

    private readonly CheckedListBox _marketsList = new() { Height = 140, Width = 300, CheckOnClick = true };
    private readonly Button _selectAllButton = new() { Text = "Alles selecteren" };

    private readonly NumericUpDown _scrapeInterval = new() { Minimum = 1, Maximum = 3600, Value = 30 };
    private readonly NumericUpDown _buyMargin = new() { Maximum = 100, DecimalPlaces = 2, Value = 1 };
    private readonly NumericUpDown _sellMargin = new() { Maximum = 100, DecimalPlaces = 2, Value = 2 };
    private readonly NumericUpDown _minOpenOrdersPct = new() { Maximum = 100, DecimalPlaces = 1, Value = 80 };
    private readonly CheckBox _autoReplenish = new() { Text = "Automatisch aanvullen bij onderschrijding" };

    private readonly NumericUpDown _maxTradesPerDay = new() { Maximum = 10_000, Value = 20 };
    private readonly NumericUpDown _maxLossPerDay = new() { Maximum = 1_000_000, DecimalPlaces = 2, Value = 100 };
    private readonly NumericUpDown _maxInvestment = new() { Maximum = 10_000_000, DecimalPlaces = 2, Value = 1000 };
    private readonly NumericUpDown _stopLoss = new() { Maximum = 100, DecimalPlaces = 2, Value = 5 };
    private readonly NumericUpDown _takeProfit = new() { Maximum = 100, DecimalPlaces = 2, Value = 10 };
    private readonly NumericUpDown _maxConcurrentOrders = new() { Maximum = 1000, Value = 10 };

    private readonly ComboBox _strategyCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly TextBox _strategyParamsBox = new() { Width = 300, Height = 60, Multiline = true };

    private readonly RadioButton _liveRadio = new() { Text = "Live" };
    private readonly RadioButton _paperRadio = new() { Text = "Papertrading", Checked = true };
    private readonly ComboBox _paperProfileCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly Button _newPaperProfileButton = new() { Text = "Nieuw papertrading profiel..." };

    private readonly Button _saveButton = new() { Text = "Opslaan" };
    private readonly Button _cancelButton = new() { Text = "Annuleren" };

    public BotProfileWizardForm(
        IBotProfileRepository botProfileRepository, IPapertradingProfileRepository papertradingProfileRepository,
        BitvavoExchangeClient liveMarketData, TradingMode currentAppMode, BotProfile? existing)
    {
        _botProfileRepository = botProfileRepository;
        _papertradingProfileRepository = papertradingProfileRepository;
        _existing = existing;

        Text = existing is null ? "Nieuw bot profiel" : $"Bot profiel bewerken — {existing.Name}";
        Width = 560;
        Height = 780;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;

        foreach (var type in Enum.GetValues<StrategyType>()) _strategyCombo.Items.Add(type);
        _strategyCombo.SelectedIndex = 0;

        BuildLayout();

        _selectAllButton.Click += (_, _) =>
        {
            for (var i = 0; i < _marketsList.Items.Count; i++) _marketsList.SetItemChecked(i, true);
        };
        _liveRadio.CheckedChanged += (_, _) => _paperProfileCombo.Enabled = _newPaperProfileButton.Enabled = !_liveRadio.Checked;
        _newPaperProfileButton.Click += async (_, _) => await CreatePapertradingProfileAsync();
        _saveButton.Click += async (_, _) => await SaveAsync();
        _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        if (existing is null)
        {
            (currentAppMode == TradingMode.Live ? _liveRadio : _paperRadio).Checked = true;
        }
        _paperProfileCombo.Enabled = _newPaperProfileButton.Enabled = !_liveRadio.Checked;

        Load += async (_, _) =>
        {
            await LoadMarketsAsync(liveMarketData);
            await LoadPapertradingProfilesAsync();
            if (_existing is not null) await PopulateFromExistingAsync();
        };
    }

    private void BuildLayout()
    {
        var root = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(12) };

        root.Controls.Add(Section("Naam", _nameBox));
        root.Controls.Add(Section("1. Handelsbedrag", Row(_fixedAmountRadio, _pctAmountRadio, _amountValue)));
        root.Controls.Add(Section("2. Handelsparen", Column(_marketsList, _selectAllButton)));
        root.Controls.Add(Section("3. Scrape/analyse-interval (sec)", _scrapeInterval));
        root.Controls.Add(Section("4. Limiet order instellingen", Row(Labeled("Koopmarge %", _buyMargin), Labeled("Verkoopmarge %", _sellMargin))));
        root.Controls.Add(Section("5. Open orders controle", Row(Labeled("Min. open orders %", _minOpenOrdersPct), _autoReplenish)));
        root.Controls.Add(Section("6. Risicobeheer", Column(
            Row(Labeled("Max trades/dag", _maxTradesPerDay), Labeled("Max verlies/dag €", _maxLossPerDay)),
            Row(Labeled("Max investering €", _maxInvestment), Labeled("Max gelijkt. orders", _maxConcurrentOrders)),
            Row(Labeled("Stop loss %", _stopLoss), Labeled("Take profit %", _takeProfit)))));
        root.Controls.Add(Section("7. Strategie", Column(_strategyCombo, _strategyParamsBox)));
        root.Controls.Add(Section("8. Modus", Column(Row(_liveRadio, _paperRadio), Row(_paperProfileCombo, _newPaperProfileButton))));
        root.Controls.Add(Row(_saveButton, _cancelButton));

        Controls.Add(root);
    }

    private static Control Section(string title, Control content)
    {
        var label = new Label { Text = title, AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 4) };
        content.Margin = new Padding(0, 0, 0, 0);
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0, 0, 0, 16) };
        panel.Controls.Add(label);
        panel.Controls.Add(content);
        return panel;
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        foreach (var c in controls) { c.Margin = new Padding(0, 2, 12, 2); row.Controls.Add(c); }
        return row;
    }

    private static FlowLayoutPanel Column(params Control[] controls)
    {
        var col = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.TopDown };
        foreach (var c in controls) { c.Margin = new Padding(0, 2, 0, 6); col.Controls.Add(c); }
        return col;
    }

    private static Control Labeled(string label, Control input)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        panel.Controls.Add(new Label { Text = label, AutoSize = true });
        panel.Controls.Add(input);
        return panel;
    }

    private async Task LoadMarketsAsync(BitvavoExchangeClient liveMarketData)
    {
        try
        {
            var markets = await liveMarketData.GetMarketsAsync();
            _marketsList.Items.Clear();
            foreach (var market in markets.Where(m => m.TradingAllowed).OrderBy(m => m.Market))
            {
                _marketsList.Items.Add(market.Market);
            }
        }
        catch
        {
            _marketsList.Items.Add("BTC-EUR");
            _marketsList.Items.Add("ETH-EUR");
        }
    }

    private async Task LoadPapertradingProfilesAsync()
    {
        var profiles = await _papertradingProfileRepository.GetAllAsync();
        _paperProfileCombo.Items.Clear();
        foreach (var profile in profiles) _paperProfileCombo.Items.Add(profile.Name);
        if (_paperProfileCombo.Items.Count > 0) _paperProfileCombo.SelectedIndex = 0;
    }

    private async Task CreatePapertradingProfileAsync()
    {
        using var dialog = new PapertradingProfileEditForm(null);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        await _papertradingProfileRepository.AddAsync(new Domain.Entities.PapertradingProfile
        {
            Name = dialog.ProfileName,
            QuoteCurrency = "EUR",
            StartingBalance = dialog.StartingBalance,
            CurrentBalance = dialog.StartingBalance,
            SlippagePercentage = dialog.SlippagePercentage,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await LoadPapertradingProfilesAsync();
        _paperProfileCombo.SelectedItem = dialog.ProfileName;
    }

    private async Task PopulateFromExistingAsync()
    {
        var profile = _existing!;
        _nameBox.Text = profile.Name;
        _nameBox.Enabled = false;
        (profile.TradeAmountMode == TradeAmountMode.FixedAmount ? _fixedAmountRadio : _pctAmountRadio).Checked = true;
        _amountValue.Value = profile.TradeAmountValue;

        var selectedMarkets = new HashSet<string>(profile.GetMarketList());
        for (var i = 0; i < _marketsList.Items.Count; i++)
        {
            _marketsList.SetItemChecked(i, selectedMarkets.Contains((string)_marketsList.Items[i]));
        }

        _scrapeInterval.Value = profile.ScrapeIntervalSeconds;
        _buyMargin.Value = profile.BuyMarginPercentage;
        _sellMargin.Value = profile.SellMarginPercentage;
        _minOpenOrdersPct.Value = profile.MinOpenOrdersPercentage;
        _autoReplenish.Checked = profile.AutoReplenishOpenOrders;
        _maxTradesPerDay.Value = profile.MaxTradesPerDay;
        _maxLossPerDay.Value = profile.MaxLossPerDay;
        _maxInvestment.Value = profile.MaxInvestment;
        _stopLoss.Value = profile.StopLossPercentage;
        _takeProfit.Value = profile.TakeProfitPercentage;
        _maxConcurrentOrders.Value = profile.MaxConcurrentOrders;

        var assignments = await _botProfileRepository.GetStrategyAssignmentsAsync(profile.Id);
        var first = assignments.FirstOrDefault();
        if (first is not null)
        {
            _strategyCombo.SelectedItem = first.StrategyType;
            _strategyParamsBox.Text = first.ParametersJson;
        }

        (profile.Mode == TradingMode.Live ? _liveRadio : _paperRadio).Checked = true;
        if (profile.PapertradingProfileName is not null && _paperProfileCombo.Items.Contains(profile.PapertradingProfileName))
        {
            _paperProfileCombo.SelectedItem = profile.PapertradingProfileName;
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show(this, "Vul een naam in voor dit bot profiel.", "Validatie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedMarkets = _marketsList.CheckedItems.Cast<string>().ToList();
        if (selectedMarkets.Count == 0)
        {
            MessageBox.Show(this, "Selecteer minimaal één handelspaar.", "Validatie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_paperRadio.Checked && _paperProfileCombo.SelectedItem is null)
        {
            MessageBox.Show(this, "Selecteer of maak een papertrading profiel.", "Validatie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var mode = _liveRadio.Checked ? TradingMode.Live : TradingMode.Paper;
        var now = DateTimeOffset.UtcNow;

        var profile = _existing ?? new BotProfile { Name = _nameBox.Text.Trim(), CreatedAt = now };
        profile.TradeAmountMode = _fixedAmountRadio.Checked ? TradeAmountMode.FixedAmount : TradeAmountMode.PercentageOfBalance;
        profile.TradeAmountValue = _amountValue.Value;
        profile.Markets = string.Join(",", selectedMarkets);
        profile.ScrapeIntervalSeconds = (int)_scrapeInterval.Value;
        profile.BuyMarginPercentage = _buyMargin.Value;
        profile.SellMarginPercentage = _sellMargin.Value;
        profile.MinOpenOrdersPercentage = _minOpenOrdersPct.Value;
        profile.AutoReplenishOpenOrders = _autoReplenish.Checked;
        profile.MaxTradesPerDay = (int)_maxTradesPerDay.Value;
        profile.MaxLossPerDay = _maxLossPerDay.Value;
        profile.MaxInvestment = _maxInvestment.Value;
        profile.StopLossPercentage = _stopLoss.Value;
        profile.TakeProfitPercentage = _takeProfit.Value;
        profile.MaxConcurrentOrders = (int)_maxConcurrentOrders.Value;
        profile.Mode = mode;
        profile.PapertradingProfileName = mode == TradingMode.Paper ? (string)_paperProfileCombo.SelectedItem! : null;
        profile.UpdatedAt = now;

        if (_existing is null)
        {
            await _botProfileRepository.AddAsync(profile);
        }
        else
        {
            await _botProfileRepository.UpdateAsync(profile);
        }

        var strategyType = (StrategyType)_strategyCombo.SelectedItem!;
        var parametersJson = string.IsNullOrWhiteSpace(_strategyParamsBox.Text) ? "{}" : _strategyParamsBox.Text.Trim();
        try { using var _ = JsonDocument.Parse(parametersJson); }
        catch (JsonException)
        {
            MessageBox.Show(this, "De strategie-parameters zijn geen geldige JSON.", "Validatie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var assignments = selectedMarkets.Select(market => new StrategyAssignment
        {
            Market = market,
            StrategyType = strategyType,
            ParametersJson = parametersJson
        }).ToList();
        await _botProfileRepository.SaveStrategyAssignmentsAsync(profile.Id, assignments);

        DialogResult = DialogResult.OK;
        Close();
    }
}
