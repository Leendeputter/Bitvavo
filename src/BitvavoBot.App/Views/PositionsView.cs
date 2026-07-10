using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Exchange.Bitvavo;

namespace BitvavoBot.App.Views;

/// <summary>Per-asset holdings with weighted-average entry price and live P&amp;L (functional spec 5.2).</summary>
public sealed class PositionsView : UserControl
{
    private readonly IPositionRepository _positionRepository;
    private readonly BitvavoExchangeClient _liveMarketData;

    private readonly TabControl _tabs = new() { Dock = DockStyle.Top, Height = 32 };
    private readonly Button _refreshButton = new() { Text = "Vernieuwen" };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true,
        AllowUserToAddRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
    };

    private TradingMode SelectedMode => _tabs.SelectedIndex == 0 ? TradingMode.Live : TradingMode.Paper;

    public PositionsView(IPositionRepository positionRepository, BitvavoExchangeClient liveMarketData)
    {
        this.ApplyStandardAutoScale();

        _positionRepository = positionRepository;
        _liveMarketData = liveMarketData;

        _tabs.TabPages.Add("Live");
        _tabs.TabPages.Add("Papertrading");
        _tabs.SelectedIndexChanged += async (_, _) => await RefreshAsync();

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Asset", HeaderText = "Asset" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Aantal" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "AvgPrice", HeaderText = "Gem. aankoopprijs" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CurrentPrice", HeaderText = "Huidige prijs" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PnlEur", HeaderText = "P&L (€)" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PnlPct", HeaderText = "P&L (%)" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Profile", HeaderText = "Paper profiel" });

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        toolbar.Controls.Add(_refreshButton);

        Dock = DockStyle.Fill;
        Controls.Add(_grid);
        Controls.Add(toolbar);
        Controls.Add(_tabs);
        this.ApplyReadableButtonSizing();

        _refreshButton.Click += async (_, _) => await RefreshAsync();
        VisibleChanged += async (_, _) => { if (Visible) await RefreshAsync(); };
    }

    private async Task RefreshAsync()
    {
        var mode = SelectedMode;
        var positions = await _positionRepository.GetOpenPositionsAsync(mode);

        _grid.Rows.Clear();
        foreach (var position in positions)
        {
            decimal currentPrice = 0m;
            try
            {
                var ticker = await _liveMarketData.GetTickerAsync(position.Market);
                currentPrice = ticker.Last;
            }
            catch
            {
                // Leave at 0 if the market ticker cannot be fetched right now; the row still shows entry data.
            }

            var pnlEur = (currentPrice - position.AverageEntryPrice) * position.Amount;
            var pnlPct = position.AverageEntryPrice == 0m ? 0m : (currentPrice - position.AverageEntryPrice) / position.AverageEntryPrice * 100m;

            var rowIndex = _grid.Rows.Add(
                position.Asset, position.Amount.ToString("N8"), position.AverageEntryPrice.ToString("N8"),
                currentPrice.ToString("N8"), pnlEur.ToString("N2"), pnlPct.ToString("N2"),
                position.PapertradingProfileName ?? string.Empty);

            var color = pnlEur >= 0 ? Color.SeaGreen : Color.Firebrick;
            _grid.Rows[rowIndex].Cells["PnlEur"].Style.ForeColor = color;
            _grid.Rows[rowIndex].Cells["PnlPct"].Style.ForeColor = color;
        }
    }
}
