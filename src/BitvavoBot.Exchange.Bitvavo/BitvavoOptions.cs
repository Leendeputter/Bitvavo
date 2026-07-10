namespace BitvavoBot.Exchange.Bitvavo;

public sealed class BitvavoOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string RestBaseUrl { get; set; } = "https://api.bitvavo.com/v2";
    public string WebSocketUrl { get; set; } = "wss://ws.bitvavo.com/v2/";
    public int AccessWindowMs { get; set; } = 10_000;
}
