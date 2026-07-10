namespace BitvavoBot.Domain.Models;

public sealed record AssetBalance(string Asset, decimal Available, decimal InOrder)
{
    public decimal Total => Available + InOrder;
}
