namespace BitvavoBot.App.Dialogs;

/// <summary>Small dialog to create a new Papertrading profile (functional spec 8.3).</summary>
public sealed class PapertradingProfileEditForm : Form
{
    private readonly TextBox _nameBox = new() { Width = 220 };
    private readonly NumericUpDown _startingBalance = new() { Maximum = 100_000_000, DecimalPlaces = 2, Value = 10_000, Width = 220 };
    private readonly NumericUpDown _slippage = new() { Maximum = 100, DecimalPlaces = 2, Value = 0, Width = 220 };
    private readonly Button _okButton = new() { Text = "OK", DialogResult = DialogResult.OK };
    private readonly Button _cancelButton = new() { Text = "Annuleren", DialogResult = DialogResult.Cancel };

    public string ProfileName => _nameBox.Text.Trim();
    public decimal StartingBalance => _startingBalance.Value;
    public decimal SlippagePercentage => _slippage.Value;

    public PapertradingProfileEditForm(string? existingName)
    {
        this.ApplyStandardAutoScale();

        Text = "Nieuw papertrading profiel";
        Width = 320;
        Height = 240;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        AcceptButton = _okButton;
        CancelButton = _cancelButton;

        if (existingName is not null) _nameBox.Text = existingName;

        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12), AutoScroll = true };
        layout.Controls.Add(new Label { Text = "Naam", AutoSize = true });
        layout.Controls.Add(_nameBox);
        layout.Controls.Add(new Label { Text = "Startsaldo (€)", AutoSize = true, Margin = new Padding(0, 10, 0, 0) });
        layout.Controls.Add(_startingBalance);
        layout.Controls.Add(new Label { Text = "Slippage % (optioneel)", AutoSize = true, Margin = new Padding(0, 10, 0, 0) });
        layout.Controls.Add(_slippage);

        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 16, 0, 0) };
        buttons.Controls.Add(_okButton);
        buttons.Controls.Add(_cancelButton);
        layout.Controls.Add(buttons);

        Controls.Add(layout);
        this.ApplyReadableButtonSizing();

        _okButton.Click += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show(this, "Vul een naam in.", "Validatie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };
    }
}
