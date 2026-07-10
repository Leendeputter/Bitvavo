using BitvavoBot.App.Dialogs;
using BitvavoBot.Domain.Entities;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Exchange.Bitvavo;

namespace BitvavoBot.App.Views;

/// <summary>Bot profile management: create/edit via the configuration wizard, start/pause/stop (functional spec 4.1).</summary>
public sealed class TradingBotView : UserControl
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IBotProfileRepository _botProfileRepository;
    private readonly IPapertradingProfileRepository _papertradingProfileRepository;
    private readonly IBotOrchestrator _botOrchestrator;
    private readonly IAppModeService _appModeService;
    private readonly BitvavoExchangeClient _liveMarketData;

    private readonly Button _newButton = new() { Text = "Nieuw profiel" };
    private readonly Button _editButton = new() { Text = "Bewerken" };
    private readonly Button _startButton = new() { Text = "Start" };
    private readonly Button _pauseButton = new() { Text = "Pauzeer" };
    private readonly Button _stopButton = new() { Text = "Stop" };
    private readonly Button _promoteButton = new() { Text = "Promoveer naar Live" };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true,
        AllowUserToAddRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false,
        MultiSelect = false, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
    };

    public TradingBotView(
        IServiceProvider serviceProvider, IBotProfileRepository botProfileRepository,
        IPapertradingProfileRepository papertradingProfileRepository, IBotOrchestrator botOrchestrator,
        IAppModeService appModeService, BitvavoExchangeClient liveMarketData)
    {
        this.ApplyStandardAutoScale();

        _serviceProvider = serviceProvider;
        _botProfileRepository = botProfileRepository;
        _papertradingProfileRepository = papertradingProfileRepository;
        _botOrchestrator = botOrchestrator;
        _appModeService = appModeService;
        _liveMarketData = liveMarketData;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Naam" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mode", HeaderText = "Mode" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Markets", HeaderText = "Markten" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "State", HeaderText = "Status" });

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        toolbar.Controls.Add(_newButton);
        toolbar.Controls.Add(_editButton);
        toolbar.Controls.Add(_startButton);
        toolbar.Controls.Add(_pauseButton);
        toolbar.Controls.Add(_stopButton);
        toolbar.Controls.Add(_promoteButton);

        Dock = DockStyle.Fill;
        Controls.Add(_grid);
        Controls.Add(toolbar);
        this.ApplyReadableButtonSizing();

        _newButton.Click += async (_, _) => await OpenWizardAsync(null);
        _editButton.Click += async (_, _) => await OpenWizardAsync(SelectedProfileName());
        _startButton.Click += async (_, _) => await SafeAsync(async name => await _botOrchestrator.StartAsync(name));
        _pauseButton.Click += async (_, _) => await SafeAsync(async name => await _botOrchestrator.PauseAsync(name));
        _stopButton.Click += async (_, _) => await SafeAsync(async name => await _botOrchestrator.StopAsync(name));
        _promoteButton.Click += async (_, _) => await PromoteToLiveAsync();

        _botOrchestrator.StateChanged += (_, _) => BeginInvoke(new MethodInvoker(async () => await RefreshAsync()));
        VisibleChanged += async (_, _) => { if (Visible) await RefreshAsync(); };
    }

    private string? SelectedProfileName() =>
        _grid.SelectedRows.Count > 0 ? (string)_grid.SelectedRows[0].Cells["Name"].Value! : null;

    private async Task OpenWizardAsync(string? existingProfileName)
    {
        BotProfile? existing = existingProfileName is null ? null : await _botProfileRepository.GetByNameAsync(existingProfileName);
        using var wizard = new BotProfileWizardForm(_botProfileRepository, _papertradingProfileRepository, _liveMarketData, _appModeService.CurrentMode, existing);
        if (wizard.ShowDialog(this) == DialogResult.OK)
        {
            await RefreshAsync();
        }
    }

    private async Task PromoteToLiveAsync()
    {
        var name = SelectedProfileName();
        if (name is null) return;

        var profile = await _botProfileRepository.GetByNameAsync(name);
        if (profile is null || profile.Mode == TradingMode.Live) return;

        var confirm = MessageBox.Show(this,
            $"Profiel '{name}' overnemen als Live bot-profiel met dezelfde instellingen?",
            "Promoveren naar Live", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        profile.Mode = TradingMode.Live;
        profile.PapertradingProfileName = null;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await _botProfileRepository.UpdateAsync(profile);
        await RefreshAsync();
    }

    private async Task SafeAsync(Func<string, Task> action)
    {
        var name = SelectedProfileName();
        if (name is null) return;
        try { await action(name); await RefreshAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task RefreshAsync()
    {
        var selected = SelectedProfileName();
        var profiles = await _botProfileRepository.GetAllAsync();

        _grid.Rows.Clear();
        foreach (var profile in profiles)
        {
            var state = _botOrchestrator.RunningProfiles.GetValueOrDefault(profile.Name, profile.State);
            _grid.Rows.Add(profile.Name, profile.Mode, profile.Markets, state);
        }

        if (selected is not null)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if ((string)row.Cells["Name"].Value! == selected) { row.Selected = true; break; }
            }
        }
    }
}
