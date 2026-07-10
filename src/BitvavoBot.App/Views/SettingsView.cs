using BitvavoBot.Domain.Interfaces;

namespace BitvavoBot.App.Views;

/// <summary>
/// Application settings, including Bitvavo API credentials which are encrypted at rest via
/// <see cref="ICredentialProtector"/> (DPAPI) before being written to disk (functional spec 11).
/// Credential changes require an application restart to take effect, since the Bitvavo REST/WS
/// clients are constructed once at startup with the credentials available at that time.
/// </summary>
public sealed class SettingsView : UserControl
{
    private readonly ISettingsService _settingsService;
    private readonly ICredentialProtector _credentialProtector;

    private readonly TextBox _apiKeyBox = new() { Width = 320, UseSystemPasswordChar = true };
    private readonly TextBox _apiSecretBox = new() { Width = 320, UseSystemPasswordChar = true };
    private readonly CheckBox _showSecretsBox = new() { Text = "Toon", AutoSize = true };
    private readonly NumericUpDown _pollingInterval = new() { Minimum = 1, Maximum = 300, Value = 5 };
    private readonly CheckBox _useExchangeFees = new() { Text = "Gebruik Bitvavo fee-structuur (via API)", Checked = true };
    private readonly NumericUpDown _makerFee = new() { Maximum = 100, DecimalPlaces = 3, Value = 0.15m };
    private readonly NumericUpDown _takerFee = new() { Maximum = 100, DecimalPlaces = 3, Value = 0.25m };
    private readonly Button _saveButton = new() { Text = "Opslaan" };
    private readonly Label _restartNotice = new() { AutoSize = true, ForeColor = Color.DarkOrange, Text = "Wijzigingen aan API-credentials vereisen een herstart van de applicatie." };

    public SettingsView(ISettingsService settingsService, ICredentialProtector credentialProtector)
    {
        _settingsService = settingsService;
        _credentialProtector = credentialProtector;

        BuildLayout();

        _useExchangeFees.CheckedChanged += (_, _) => { _makerFee.Enabled = _takerFee.Enabled = !_useExchangeFees.Checked; };
        _showSecretsBox.CheckedChanged += (_, _) =>
        {
            _apiKeyBox.UseSystemPasswordChar = !_showSecretsBox.Checked;
            _apiSecretBox.UseSystemPasswordChar = !_showSecretsBox.Checked;
        };
        _saveButton.Click += async (_, _) => await SaveAsync();

        Load += async (_, _) => await LoadAsync();
    }

    private void BuildLayout()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(16);

        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

        layout.Controls.Add(new Label { Text = "Bitvavo API Key", AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold) });
        layout.Controls.Add(_apiKeyBox);
        layout.Controls.Add(new Label { Text = "Bitvavo API Secret", AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold), Margin = new Padding(0, 10, 0, 0) });
        layout.Controls.Add(_apiSecretBox);
        layout.Controls.Add(_showSecretsBox);
        layout.Controls.Add(_restartNotice);

        layout.Controls.Add(new Label { Text = "Marktmonitor polling-interval bij WebSocket-fallback (sec)", AutoSize = true, Margin = new Padding(0, 16, 0, 0) });
        layout.Controls.Add(_pollingInterval);

        layout.Controls.Add(new Label { Text = "Transactiekosten (Papertrading-simulatie)", AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold), Margin = new Padding(0, 16, 0, 0) });
        layout.Controls.Add(_useExchangeFees);
        layout.Controls.Add(Row("Maker fee %", _makerFee));
        layout.Controls.Add(Row("Taker fee %", _takerFee));

        layout.Controls.Add(_saveButton);

        Controls.Add(layout);
    }

    private static Control Row(string label, Control input)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Width = 100, Margin = new Padding(0, 4, 8, 0) });
        panel.Controls.Add(input);
        return panel;
    }

    private async Task LoadAsync()
    {
        var settings = await _settingsService.LoadAsync();

        _apiKeyBox.Text = string.IsNullOrEmpty(settings.BitvavoApiKeyEncrypted) ? string.Empty : _credentialProtector.Unprotect(settings.BitvavoApiKeyEncrypted);
        _apiSecretBox.Text = string.IsNullOrEmpty(settings.BitvavoApiSecretEncrypted) ? string.Empty : _credentialProtector.Unprotect(settings.BitvavoApiSecretEncrypted);
        _pollingInterval.Value = settings.MarketMonitorPollingIntervalSeconds;
        _useExchangeFees.Checked = settings.UseExchangeFeeSchedule;
        _makerFee.Value = settings.ManualMakerFeePercentage;
        _takerFee.Value = settings.ManualTakerFeePercentage;
        _makerFee.Enabled = _takerFee.Enabled = !_useExchangeFees.Checked;
    }

    private async Task SaveAsync()
    {
        var settings = await _settingsService.LoadAsync();
        settings.BitvavoApiKeyEncrypted = string.IsNullOrEmpty(_apiKeyBox.Text) ? string.Empty : _credentialProtector.Protect(_apiKeyBox.Text);
        settings.BitvavoApiSecretEncrypted = string.IsNullOrEmpty(_apiSecretBox.Text) ? string.Empty : _credentialProtector.Protect(_apiSecretBox.Text);
        settings.MarketMonitorPollingIntervalSeconds = (int)_pollingInterval.Value;
        settings.UseExchangeFeeSchedule = _useExchangeFees.Checked;
        settings.ManualMakerFeePercentage = _makerFee.Value;
        settings.ManualTakerFeePercentage = _takerFee.Value;

        await _settingsService.SaveAsync(settings);
        MessageBox.Show(this, "Instellingen opgeslagen.", "Instellingen", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
