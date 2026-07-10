using BitvavoBot.Domain.Interfaces;

namespace BitvavoBot.App.Views;

/// <summary>Compares strategy run results across profiles/markets (functional spec 7).</summary>
public sealed class StrategiesView : UserControl
{
    private readonly IStrategyRunRepository _strategyRunRepository;

    private readonly Button _refreshButton = new() { Text = "Vernieuwen" };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true,
        AllowUserToAddRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
    };

    public StrategiesView(IStrategyRunRepository strategyRunRepository)
    {
        this.ApplyStandardAutoScale();

        _strategyRunRepository = strategyRunRepository;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mode", HeaderText = "Mode" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Strategy", HeaderText = "Strategie" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Profile", HeaderText = "Bot profiel" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Markets", HeaderText = "Markten" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Started", HeaderText = "Gestart" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ended", HeaderText = "Beëindigd" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Trades", HeaderText = "Aantal trades" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "WinRate", HeaderText = "Winrate %" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Pnl", HeaderText = "Totaal P&L" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Return", HeaderText = "Rendement %" });

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        toolbar.Controls.Add(_refreshButton);

        Dock = DockStyle.Fill;
        Controls.Add(_grid);
        Controls.Add(toolbar);

        _refreshButton.Click += async (_, _) => await RefreshAsync();
        VisibleChanged += async (_, _) => { if (Visible) await RefreshAsync(); };
    }

    private async Task RefreshAsync()
    {
        var runs = await _strategyRunRepository.GetAllAsync();

        _grid.Rows.Clear();
        foreach (var run in runs)
        {
            _grid.Rows.Add(
                run.Mode, run.StrategyName, run.BotProfileName, run.Markets,
                run.StartedAt.LocalDateTime, run.EndedAt?.LocalDateTime.ToString() ?? "actief",
                run.TotalTrades, run.WinRate.ToString("N1"), run.TotalProfitLoss.ToString("N2"), run.ReturnPercentage.ToString("N2"));
        }
    }
}
