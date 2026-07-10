using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;

namespace BitvavoBot.App.Views;

/// <summary>Realtime overview of open orders, with a strict Live/Papertrading tab split (functional spec 5.1).</summary>
public sealed class OpenOrdersView : UserControl
{
    private readonly IOrderRepository _orderRepository;
    private readonly IExchangeClientFactory _exchangeClientFactory;

    private readonly TabControl _tabs = new() { Dock = DockStyle.Top, Height = 32 };
    private readonly Button _refreshButton = new() { Text = "Vernieuwen" };
    private readonly Button _cancelButton = new() { Text = "Annuleren" };
    private readonly Button _cancelAllButton = new() { Text = "Alles annuleren" };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true,
        AllowUserToAddRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
    };

    private TradingMode SelectedMode => _tabs.SelectedIndex == 0 ? TradingMode.Live : TradingMode.Paper;

    public OpenOrdersView(IOrderRepository orderRepository, IExchangeClientFactory exchangeClientFactory)
    {
        this.ApplyStandardAutoScale();

        _orderRepository = orderRepository;
        _exchangeClientFactory = exchangeClientFactory;

        _tabs.TabPages.Add("Live");
        _tabs.TabPages.Add("Papertrading");
        _tabs.SelectedIndexChanged += async (_, _) => await RefreshAsync();

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Market", HeaderText = "Markt" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Side", HeaderText = "Zijde" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Type" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Price", HeaderText = "Prijs" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Aantal" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Profile", HeaderText = "Bot profiel" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Created", HeaderText = "Aangemaakt" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ExternalId", HeaderText = "Order ID", Visible = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PaperProfile", HeaderText = "Paper profiel", Visible = false });

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        toolbar.Controls.Add(_refreshButton);
        toolbar.Controls.Add(_cancelButton);
        toolbar.Controls.Add(_cancelAllButton);

        Dock = DockStyle.Fill;
        Controls.Add(_grid);
        Controls.Add(toolbar);
        Controls.Add(_tabs);

        _refreshButton.Click += async (_, _) => await RefreshAsync();
        _cancelButton.Click += async (_, _) => await CancelSelectedAsync();
        _cancelAllButton.Click += async (_, _) => await CancelAllAsync();

        VisibleChanged += async (_, _) => { if (Visible) await RefreshAsync(); };
    }

    private async Task RefreshAsync()
    {
        var mode = SelectedMode;
        var orders = await _orderRepository.GetOpenOrdersAsync(mode);

        _grid.Rows.Clear();
        foreach (var order in orders)
        {
            _grid.Rows.Add(
                order.Market, order.Side, order.Type,
                order.Price?.ToString("N8") ?? "market", order.Amount.ToString("N8"),
                order.Status, order.BotProfileName, order.CreatedAt.LocalDateTime, order.ExternalId,
                order.PapertradingProfileName ?? string.Empty);
        }
    }

    private async Task CancelSelectedAsync()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var row = _grid.SelectedRows[0];
        var market = (string)row.Cells["Market"].Value!;
        var externalId = (string)row.Cells["ExternalId"].Value!;
        var paperProfile = row.Cells["PaperProfile"].Value as string;

        try
        {
            var mode = SelectedMode;
            var client = _exchangeClientFactory.GetClient(mode, string.IsNullOrEmpty(paperProfile) ? null : paperProfile);
            await client.CancelOrderAsync(market, externalId);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Fout bij annuleren", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task CancelAllAsync()
    {
        var mode = SelectedMode;
        var confirm = MessageBox.Show(this,
            mode == TradingMode.Live
                ? "Weet u zeker dat u ALLE live orders wilt annuleren? Dit kan niet ongedaan worden gemaakt."
                : "Weet u zeker dat u alle papertrading orders wilt annuleren?",
            "Bevestigen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        try
        {
            var orders = await _orderRepository.GetOpenOrdersAsync(mode);
            foreach (var group in orders.GroupBy(o => o.PapertradingProfileName))
            {
                var client = _exchangeClientFactory.GetClient(mode, group.Key);
                await client.CancelAllOrdersAsync();
            }
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Fout bij annuleren", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
