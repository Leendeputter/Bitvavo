using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BitvavoBot.Exchange.Bitvavo.Dto;

namespace BitvavoBot.Exchange.Bitvavo;

public sealed class BitvavoApiException : Exception
{
    public int ErrorCode { get; }

    public BitvavoApiException(int errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>Thin, typed wrapper around the Bitvavo v2 REST API. Handles signing and JSON (de)serialization.</summary>
public sealed class BitvavoRestClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly BitvavoOptions _options;
    private readonly BitvavoAuthSigner _signer;

    public BitvavoRestClient(HttpClient httpClient, BitvavoOptions options)
    {
        _httpClient = httpClient;
        _options = options;
        _signer = new BitvavoAuthSigner(options);
    }

    public Task<List<MarketDto>> GetMarketsAsync(CancellationToken cancellationToken) =>
        SendAsync<List<MarketDto>>(HttpMethod.Get, "/markets", authenticated: false, cancellationToken: cancellationToken);

    public Task<Ticker24hDto> GetTicker24hAsync(string market, CancellationToken cancellationToken) =>
        SendAsync<Ticker24hDto>(HttpMethod.Get, $"/ticker/24h?market={Uri.EscapeDataString(market)}", authenticated: false, cancellationToken: cancellationToken);

    public Task<List<Ticker24hDto>> GetAllTicker24hAsync(CancellationToken cancellationToken) =>
        SendAsync<List<Ticker24hDto>>(HttpMethod.Get, "/ticker/24h", authenticated: false, cancellationToken: cancellationToken);

    public Task<OrderBookDto> GetOrderBookAsync(string market, int depth, CancellationToken cancellationToken) =>
        SendAsync<OrderBookDto>(HttpMethod.Get, $"/{Uri.EscapeDataString(market)}/book?depth={depth}", authenticated: false, cancellationToken: cancellationToken);

    public Task<AccountFeesDto> GetAccountAsync(CancellationToken cancellationToken) =>
        SendAsync<AccountFeesDto>(HttpMethod.Get, "/account", authenticated: true, cancellationToken: cancellationToken);

    public Task<List<BalanceDto>> GetBalanceAsync(CancellationToken cancellationToken) =>
        SendAsync<List<BalanceDto>>(HttpMethod.Get, "/balance", authenticated: true, cancellationToken: cancellationToken);

    public Task<OrderDto> PlaceOrderAsync(PlaceOrderBody body, CancellationToken cancellationToken) =>
        SendAsync<OrderDto>(HttpMethod.Post, "/order", authenticated: true, body: body, cancellationToken: cancellationToken);

    public Task<OrderDto> CancelOrderAsync(string market, string orderId, CancellationToken cancellationToken) =>
        SendAsync<OrderDto>(HttpMethod.Delete, $"/order?market={Uri.EscapeDataString(market)}&orderId={Uri.EscapeDataString(orderId)}", authenticated: true, cancellationToken: cancellationToken);

    public Task<List<OrderDto>> CancelAllOrdersAsync(string? market, CancellationToken cancellationToken)
    {
        var path = market is null ? "/orders" : $"/orders?market={Uri.EscapeDataString(market)}";
        return SendAsync<List<OrderDto>>(HttpMethod.Delete, path, authenticated: true, cancellationToken: cancellationToken);
    }

    public Task<List<OrderDto>> GetOpenOrdersAsync(string? market, CancellationToken cancellationToken)
    {
        var path = market is null ? "/ordersOpen" : $"/ordersOpen?market={Uri.EscapeDataString(market)}";
        return SendAsync<List<OrderDto>>(HttpMethod.Get, path, authenticated: true, cancellationToken: cancellationToken);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string pathWithQuery, bool authenticated, object? body = null, CancellationToken cancellationToken = default)
    {
        var bodyJson = body is null ? string.Empty : JsonSerializer.Serialize(body, JsonOptions);

        // Built as an absolute URI by string concatenation rather than HttpClient.BaseAddress +
        // relative Uri: when a relative path starts with "/" (as all of ours do), standard URI
        // combination rules treat it as absolute-from-host-root and silently drop the "/v2" base
        // path segment, sending every request to the wrong URL.
        var absoluteUri = new Uri($"{_options.RestBaseUrl.TrimEnd('/')}{pathWithQuery}");
        using var request = new HttpRequestMessage(method, absoluteUri);
        if (body is not null)
        {
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        }

        if (authenticated)
        {
            var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var headers = _signer.CreateAuthHeaders(timestampMs, method.Method, pathWithQuery, bodyJson);
            foreach (var (key, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = TryParseError(responseBody);
            throw new BitvavoApiException(error?.ErrorCode ?? (int)response.StatusCode, error?.Error ?? responseBody);
        }

        return JsonSerializer.Deserialize<T>(responseBody, JsonOptions)
            ?? throw new BitvavoApiException(0, "Empty response body from Bitvavo API.");
    }

    private static ErrorDto? TryParseError(string responseBody)
    {
        try
        {
            return JsonSerializer.Deserialize<ErrorDto>(responseBody, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
