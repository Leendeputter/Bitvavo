using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;

namespace BitvavoBot.App.Views;

/// <summary>Queryable log overview (functional spec 2.1 item 9, 10.7).</summary>
public sealed class LoggingView : UserControl
{
    private readonly ILogRepository _logRepository;

    private readonly ComboBox _typeFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly ComboBox _modeFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly Button _refreshButton = new() { Text = "Vernieuwen" };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true,
        AllowUserToAddRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
    };

    public LoggingView(ILogRepository logRepository)
    {
        this.ApplyStandardAutoScale();

        _logRepository = logRepository;

        _typeFilter.Items.Add("Alle types");
        _typeFilter.Items.AddRange(Enum.GetNames<LogEntryType>());
        _typeFilter.SelectedIndex = 0;

        _modeFilter.Items.Add("Alle modes");
        _modeFilter.Items.AddRange(Enum.GetNames<TradingMode>());
        _modeFilter.SelectedIndex = 0;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Timestamp", HeaderText = "Tijdstip" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Type" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mode", HeaderText = "Mode" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source", HeaderText = "Bron" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Message", HeaderText = "Bericht", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        toolbar.Controls.Add(new Label { Text = "Type:", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        toolbar.Controls.Add(_typeFilter);
        toolbar.Controls.Add(new Label { Text = "Mode:", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
        toolbar.Controls.Add(_modeFilter);
        toolbar.Controls.Add(_refreshButton);

        Dock = DockStyle.Fill;
        Controls.Add(_grid);
        Controls.Add(toolbar);
        this.ApplyReadableButtonSizing();

        _refreshButton.Click += async (_, _) => await RefreshAsync();
        _typeFilter.SelectedIndexChanged += async (_, _) => await RefreshAsync();
        _modeFilter.SelectedIndexChanged += async (_, _) => await RefreshAsync();
        VisibleChanged += async (_, _) => { if (Visible) await RefreshAsync(); };
    }

    private async Task RefreshAsync()
    {
        LogEntryType? type = _typeFilter.SelectedIndex > 0 ? Enum.Parse<LogEntryType>((string)_typeFilter.SelectedItem!) : null;
        TradingMode? mode = _modeFilter.SelectedIndex > 0 ? Enum.Parse<TradingMode>((string)_modeFilter.SelectedItem!) : null;

        var entries = await _logRepository.GetRecentAsync(500, type, mode);

        _grid.Rows.Clear();
        foreach (var entry in entries)
        {
            var rowIndex = _grid.Rows.Add(entry.Timestamp.LocalDateTime, entry.Type, entry.Mode?.ToString() ?? "-", entry.Source, entry.Message);
            _grid.Rows[rowIndex].Cells["Type"].Style.ForeColor = entry.Type switch
            {
                LogEntryType.Error => Color.Firebrick,
                LogEntryType.Warning => Color.DarkOrange,
                LogEntryType.Trade => Color.SeaGreen,
                _ => Color.Black
            };
        }
    }
}
