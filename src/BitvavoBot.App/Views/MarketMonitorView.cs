using System.Text;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Models;
using BitvavoBot.Exchange.Bitvavo;

namespace BitvavoBot.App.Views;

/// <summary>Realtime overview of all Bitvavo markets (functional spec 3). Mode-independent — always shows live data.</summary>
public sealed class MarketMonitorView : UserControl
{
    private readonly BitvavoExchangeClient _liveMarketData;
    private readonly Dictionary<string, Ticker> _tickers = new();
    private readonly HashSet<string> _favorites = new();

    private readonly TextBox _searchBox = new() { Width = 220, PlaceholderText = "Filter op marktnaam (bv. BTC)" };
    private readonly CheckBox _favoritesOnly = new() { Text = "Toon alleen favorieten", AutoSize = true };
    private readonly Button _exportButton = new() { Text = "Exporteer naar CSV" };
    private readonly Label _fallbackLabel = new() { AutoSize = true, ForeColor = Color.DarkOrange, Visible = false };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AutoGenerateColumns = false,
        ReadOnly = true,
        AllowUserToAddRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        RowHeadersVisible = false,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
    };

    public MarketMonitorView(BitvavoExchangeClient liveMarketData)
    {
        this.ApplyStandardAutoScale();

        _liveMarketData = liveMarketData;
        BuildLayout();
        this.ApplyReadableButtonSizing();

        _searchBox.TextChanged += (_, _) => RenderGrid();
        _favoritesOnly.CheckedChanged += (_, _) => RenderGrid();
        _exportButton.Click += (_, _) => ExportCsv();
        _grid.ColumnHeaderMouseClick += (_, e) => SortByColumn(e.ColumnIndex);
        _grid.CellClick += OnCellClick;

        _liveMarketData.TickerUpdated += (_, ticker) => BeginInvoke(new MethodInvoker(() => { _tickers[ticker.Market] = ticker; RenderGrid(); }));
        _liveMarketData.ConnectionStatusChanged += (_, status) => BeginInvoke(new MethodInvoker(() => UpdateFallbackLabel(status)));

        Load += async (_, _) => await LoadInitialAsync();
    }

    private void BuildLayout()
    {
        Dock = DockStyle.Fill;

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        toolbar.Controls.Add(_searchBox);
        toolbar.Controls.Add(_favoritesOnly);
        toolbar.Controls.Add(_exportButton);

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Fav", HeaderText = "★", Width = 30 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Market", HeaderText = "Markt", DataPropertyName = "Market" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Bid", HeaderText = "Bid" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ask", HeaderText = "Ask" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Spread", HeaderText = "Spread %" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Last", HeaderText = "Laatste koers" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Change", HeaderText = "24h Change %" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Volume", HeaderText = "24h Volume" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "High", HeaderText = "Hoog 24h" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Low", HeaderText = "Laag 24h" });

        Controls.Add(_grid);
        Controls.Add(_fallbackLabel);
        Controls.Add(toolbar);
    }

    private async Task LoadInitialAsync()
    {
        try
        {
            var tickers = await _liveMarketData.GetAllTickersAsync();
            foreach (var ticker in tickers) _tickers[ticker.Market] = ticker;
            RenderGrid();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Kon markten niet laden: {ex.Message}", "Fout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private int _sortColumn = 1;
    private bool _sortAscending = true;

    private void SortByColumn(int columnIndex)
    {
        if (columnIndex == 0) return;
        if (_sortColumn == columnIndex) _sortAscending = !_sortAscending;
        else { _sortColumn = columnIndex; _sortAscending = true; }
        RenderGrid();
    }

    private void OnCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 0) return;
        var market = _grid.Rows[e.RowIndex].Cells["Market"].Value as string;
        if (market is null) return;
        if (!_favorites.Add(market)) _favorites.Remove(market);
        RenderGrid();
    }

    private void UpdateFallbackLabel(ConnectionStatus status)
    {
        _fallbackLabel.Visible = status is ConnectionStatus.FallbackPolling or ConnectionStatus.Reconnecting;
        _fallbackLabel.Text = "Fallback naar polling — WebSocket verbroken";
    }

    private IEnumerable<Ticker> GetFilteredSorted()
    {
        var query = _tickers.Values.AsEnumerable();

        var filter = _searchBox.Text.Trim();
        if (filter.Length > 0)
        {
            query = query.Where(t => t.Market.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }
        if (_favoritesOnly.Checked)
        {
            query = query.Where(t => _favorites.Contains(t.Market));
        }

        Func<Ticker, object> keySelector = _sortColumn switch
        {
            1 => t => t.Market,
            2 => t => t.Bid,
            3 => t => t.Ask,
            4 => t => t.SpreadPercentage,
            5 => t => t.Last,
            6 => t => t.Change24hPercentage,
            7 => t => t.Volume24h,
            8 => t => t.High24h,
            9 => t => t.Low24h,
            _ => t => t.Market
        };

        query = (_sortAscending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector))
            .ThenBy(t => _favorites.Contains(t.Market) ? 0 : 1);

        return query;
    }

    private void RenderGrid()
    {
        _grid.SuspendLayout();
        _grid.Rows.Clear();

        foreach (var ticker in GetFilteredSorted())
        {
            var isFavorite = _favorites.Contains(ticker.Market);
            var rowIndex = _grid.Rows.Add(
                isFavorite ? "★" : "☆",
                ticker.Market,
                ticker.Bid.ToString("N8"),
                ticker.Ask.ToString("N8"),
                ticker.SpreadPercentage.ToString("N2"),
                ticker.Last.ToString("N8"),
                ticker.Change24hPercentage.ToString("N2"),
                ticker.Volume24h.ToString("N2"),
                ticker.High24h.ToString("N8"),
                ticker.Low24h.ToString("N8"));

            if (ticker.Change24hPercentage < 0)
            {
                _grid.Rows[rowIndex].Cells["Change"].Style.ForeColor = Color.Firebrick;
            }
            else if (ticker.Change24hPercentage > 0)
            {
                _grid.Rows[rowIndex].Cells["Change"].Style.ForeColor = Color.SeaGreen;
            }
        }

        _grid.ResumeLayout();
    }

    private void ExportCsv()
    {
        using var dialog = new SaveFileDialog { Filter = "CSV-bestand|*.csv", FileName = "marktmonitor.csv" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var builder = new StringBuilder();
        var headers = _grid.Columns.Cast<DataGridViewColumn>().Skip(1).Select(c => c.HeaderText);
        builder.AppendLine(string.Join(';', headers));

        foreach (DataGridViewRow row in _grid.Rows)
        {
            var values = row.Cells.Cast<DataGridViewCell>().Skip(1).Select(c => c.Value?.ToString() ?? string.Empty);
            builder.AppendLine(string.Join(';', values));
        }

        File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
    }
}
