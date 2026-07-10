namespace BitvavoBot.Domain.Models;

/// <summary>Maker/taker fee percentages, fetched from the exchange or configured manually in Instellingen.</summary>
public sealed record FeeSchedule(decimal MakerFeePercentage, decimal TakerFeePercentage);
