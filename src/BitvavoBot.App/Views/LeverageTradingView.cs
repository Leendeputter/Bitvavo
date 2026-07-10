using BitvavoBot.App.Composition;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Exchange.Bitvavo;

namespace BitvavoBot.App.Views;

/// <summary>
/// Leverage/futures trading (functional spec 6). Bitvavo itself offers no Live leverage product,
/// so this screen only operates in Papertrading — the mandatory Auto Stop Loss control is
/// permanently checked and disabled, never a toggle the user can turn off.
/// </summary>
public sealed class LeverageTradingView : UserControl
{
    private readonly IAppModeService _appModeService;
    private readonly ExchangeClientFactory _exchangeClientFactory;
    private readonly IPapertradingProfileRepository _papertradingProfileRepository;
    private readonly BitvavoExchangeClient _liveMarketData;

    private readonly Label _unsupportedLabel = new()
    {
        Text = "Bitvavo ondersteunt geen Live leverage/margin trading. Schakel over naar Papertrading om leverage strategieën te testen.",
        AutoSize = false, Dock = DockStyle.Top, Height = 40, ForeColor = Color.DarkOrange
    };

    private readonly ComboBox _paperProfileCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly ComboBox _marketCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly NumericUpDown _leverage = new() { Minimum = 1, Maximum = 20, Value = 5 };
    private readonly RadioButton _longRadio = new() { Text = "Long", Checked = true };
    private readonly RadioButton _shortRadio = new() { Text = "Short" };
    private readonly RadioButton _crossRadio = new() { Text = "Cross", Checked = true };
    private readonly RadioButton _isolatedRadio = new() { Text = "Isolated" };
    private readonly NumericUpDown _positionSize = new() { Maximum = 1_000_000, DecimalPlaces = 2, Value = 100 };
    private readonly CheckBox _autoStopLoss = new() { Text = "Auto Stop Loss (verplicht)", Checked = true, Enabled = false };
    private readonly NumericUpDown _stopLossPct = new() { Maximum = 100, DecimalPlaces = 2, Value = 5 };
    private readonly CheckBox _autoTakeProfit = new() { Text = "Auto Take Profit" };
    private readonly NumericUpDown _takeProfitPct = new() { Maximum = 100, DecimalPlaces = 2, Value = 10, Enabled = false };
    private readonly Label _liquidationPreview = new() { AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Bold) };
    private readonly Button _openButton = new() { Text = "Open positie" };

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true,
        AllowUserToAddRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
    };
    private readonly Button _closeButton = new() { Text = "Sluit positie" };

    public LeverageTradingView(
        IAppModeService appModeService, ExchangeClientFactory exchangeClientFactory,
        IPapertradingProfileRepository papertradingProfileRepository, BitvavoExchangeClient liveMarketData)
    {
        this.ApplyStandardAutoScale();

        _appModeService = appModeService;
        _exchangeClientFactory = exchangeClientFactory;
        _papertradingProfileRepository = papertradingProfileRepository;
        _liveMarketData = liveMarketData;

        BuildLayout();

        _autoTakeProfit.CheckedChanged += (_, _) => _takeProfitPct.Enabled = _autoTakeProfit.Checked;
        _leverage.ValueChanged += async (_, _) => await UpdateLiquidationPreviewAsync();
        _marketCombo.SelectedIndexChanged += async (_, _) => await UpdateLiquidationPreviewAsync();
        _longRadio.CheckedChanged += async (_, _) => await UpdateLiquidationPreviewAsync();
        _crossRadio.CheckedChanged += async (_, _) => await UpdateLiquidationPreviewAsync();
        _openButton.Click += async (_, _) => await OpenPositionAsync();
        _closeButton.Click += async (_, _) => await ClosePositionAsync();

        _appModeService.ModeChanged += (_, mode) => BeginInvoke(new MethodInvoker(UpdateModeVisibility));
        Load += async (_, _) => { await LoadDataAsync(); UpdateModeVisibility(); await UpdateLiquidationPreviewAsync(); };
        VisibleChanged += async (_, _) => { if (Visible) await RefreshPositionsAsync(); };
    }

    private void BuildLayout()
    {
        Dock = DockStyle.Fill;

        var form = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12) };
        form.Controls.Add(Row("Papertrading profiel", _paperProfileCombo));
        form.Controls.Add(Row("Markt", _marketCombo));
        form.Controls.Add(Row("Leverage (1x-20x)", _leverage));
        form.Controls.Add(RowControls(_longRadio, _shortRadio));
        form.Controls.Add(RowControls(_crossRadio, _isolatedRadio));
        form.Controls.Add(Row("Positiegrootte (€)", _positionSize));
        form.Controls.Add(RowControls(_autoStopLoss, _stopLossPct));
        form.Controls.Add(RowControls(_autoTakeProfit, _takeProfitPct));
        form.Controls.Add(_liquidationPreview);
        form.Controls.Add(_openButton);

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Market", HeaderText = "Markt" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Side", HeaderText = "Type" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Leverage", HeaderText = "Leverage" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Aantal" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Entry", HeaderText = "Entry prijs" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Liquidation", HeaderText = "Liquidatieprijs" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ExternalId", HeaderText = "ID", Visible = false });

        Controls.Add(_grid);
        Controls.Add(_closeButton);
        Controls.Add(form);
        Controls.Add(_unsupportedLabel);
    }

    private static Control Row(string label, Control input)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Width = 140, Margin = new Padding(0, 4, 8, 0) });
        panel.Controls.Add(input);
        return panel;
    }

    private static Control RowControls(params Control[] controls)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        foreach (var c in controls) { c.Margin = new Padding(0, 4, 12, 0); panel.Controls.Add(c); }
        return panel;
    }

    private void UpdateModeVisibility()
    {
        var isPaper = _appModeService.CurrentMode == TradingMode.Paper;
        _unsupportedLabel.Visible = !isPaper;
    }

    private async Task LoadDataAsync()
    {
        var profiles = await _papertradingProfileRepository.GetAllAsync();
        _paperProfileCombo.Items.Clear();
        foreach (var p in profiles) _paperProfileCombo.Items.Add(p.Name);
        if (_paperProfileCombo.Items.Count > 0) _paperProfileCombo.SelectedIndex = 0;

        try
        {
            var markets = await _liveMarketData.GetMarketsAsync();
            _marketCombo.Items.Clear();
            foreach (var m in markets.Where(m => m.TradingAllowed).OrderBy(m => m.Market)) _marketCombo.Items.Add(m.Market);
            if (_marketCombo.Items.Count > 0) _marketCombo.SelectedIndex = 0;
        }
        catch
        {
            _marketCombo.Items.Add("BTC-EUR");
            _marketCombo.SelectedIndex = 0;
        }

        await RefreshPositionsAsync();
    }

    private async Task UpdateLiquidationPreviewAsync()
    {
        if (_marketCombo.SelectedItem is not string market) { _liquidationPreview.Text = string.Empty; return; }

        try
        {
            var ticker = await _liveMarketData.GetTickerAsync(market);
            var side = _longRadio.Checked ? PositionSide.Long : PositionSide.Short;
            var marginType = _crossRadio.Checked ? MarginType.Cross : MarginType.Isolated;
            var entry = side == PositionSide.Long ? ticker.Ask : ticker.Bid;

            var liquidationPrice = new Exchange.Paper.PaperLeverageClient(_liveMarketData, NullPositionRepository.Instance, "preview")
                .CalculateLiquidationPrice(entry, _leverage.Value, side, marginType);

            _liquidationPreview.Text = $"Geschatte liquidatieprijs: {liquidationPrice:N4}";
        }
        catch
        {
            _liquidationPreview.Text = string.Empty;
        }
    }

    private async Task OpenPositionAsync()
    {
        if (_paperProfileCombo.SelectedItem is not string profileName)
        {
            MessageBox.Show(this, "Selecteer of maak eerst een papertrading profiel (via het Papertrading-scherm).", "Validatie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_marketCombo.SelectedItem is not string market) return;

        try
        {
            var client = _exchangeClientFactory.GetPaperLeverageClient(profileName);
            var side = _longRadio.Checked ? PositionSide.Long : PositionSide.Short;
            var marginType = _crossRadio.Checked ? MarginType.Cross : MarginType.Isolated;

            var request = new OpenPositionRequest(market, side, _leverage.Value, marginType, _positionSize.Value, _stopLossPct.Value, _autoTakeProfit.Checked ? _takeProfitPct.Value : null);
            await client.OpenPositionAsync(request);
            await RefreshPositionsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ClosePositionAsync()
    {
        if (_grid.SelectedRows.Count == 0 || _paperProfileCombo.SelectedItem is not string profileName) return;
        var row = _grid.SelectedRows[0];
        var market = (string)row.Cells["Market"].Value!;
        var externalId = (string)row.Cells["ExternalId"].Value!;

        try
        {
            var client = _exchangeClientFactory.GetPaperLeverageClient(profileName);
            await client.ClosePositionAsync(market, externalId);
            await RefreshPositionsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RefreshPositionsAsync()
    {
        _grid.Rows.Clear();
        if (_paperProfileCombo.SelectedItem is not string profileName) return;

        try
        {
            var client = _exchangeClientFactory.GetPaperLeverageClient(profileName);
            var positions = await client.GetOpenPositionsAsync();
            foreach (var position in positions)
            {
                _grid.Rows.Add(position.Market, position.Side, $"{position.Leverage}x", position.Amount.ToString("N8"), position.EntryPrice.ToString("N4"), position.LiquidationPrice.ToString("N4"), position.ExternalId);
            }
        }
        catch
        {
            // No profile selected yet, or no positions — leave the grid empty.
        }
    }

    private sealed class NullPositionRepository : IPositionRepository
    {
        public static readonly NullPositionRepository Instance = new();
        public Task<Domain.Entities.Position?> GetOpenPositionAsync(TradingMode mode, string asset, string? papertradingProfileName = null, CancellationToken cancellationToken = default) => Task.FromResult<Domain.Entities.Position?>(null);
        public Task<IReadOnlyList<Domain.Entities.Position>> GetOpenPositionsAsync(TradingMode mode, string? papertradingProfileName = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Domain.Entities.Position>>(Array.Empty<Domain.Entities.Position>());
        public Task UpsertAsync(Domain.Entities.Position position, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
