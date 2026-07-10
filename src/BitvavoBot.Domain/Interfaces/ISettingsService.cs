namespace BitvavoBot.Domain.Interfaces;

public sealed record AppSettings
{
    public string BitvavoApiKeyEncrypted { get; set; } = string.Empty;
    public string BitvavoApiSecretEncrypted { get; set; } = string.Empty;
    public int MarketMonitorPollingIntervalSeconds { get; set; } = 5;
    public decimal ManualMakerFeePercentage { get; set; } = 0.15m;
    public decimal ManualTakerFeePercentage { get; set; } = 0.25m;
    public bool UseExchangeFeeSchedule { get; set; } = true;
}

/// <summary>Loads/saves application settings; secrets are stored encrypted (see <see cref="ICredentialProtector"/>).</summary>
public interface ISettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>Encrypts/decrypts secrets at rest. The Windows implementation uses DPAPI; secrets are never stored in plain text.</summary>
public interface ICredentialProtector
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
}
