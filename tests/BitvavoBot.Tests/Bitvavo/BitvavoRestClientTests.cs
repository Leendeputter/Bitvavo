using System.Net;
using BitvavoBot.Exchange.Bitvavo;
using Xunit;

namespace BitvavoBot.Tests.Bitvavo;

public sealed class BitvavoRestClientTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public CapturingHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        public Uri? CapturedRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequestUri = request.RequestUri;
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_responseJson) };
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Regression test: HttpClient.BaseAddress combined with a relative path that starts with "/"
    /// silently drops the "/v2" base path segment (standard URI combination rules treat a
    /// leading-slash relative path as absolute-from-host-root), sending every request to the
    /// wrong URL. This broke every REST call — including the ones Marktmonitor depends on — with
    /// a silent 404. Requests must always resolve to the full "/v2/..." path.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_RequestsFullV2PathIncludingBasePathSegment()
    {
        var handler = new CapturingHandler("[]");
        var client = new BitvavoRestClient(new HttpClient(handler), new BitvavoOptions { RestBaseUrl = "https://api.bitvavo.com/v2" });

        await client.GetMarketsAsync(CancellationToken.None);

        Assert.Equal("https://api.bitvavo.com/v2/markets", handler.CapturedRequestUri?.ToString());
    }

    [Fact]
    public async Task GetTicker24hAsync_IncludesQueryStringAndBasePath()
    {
        const string json = """
            {"market":"BTC-EUR","last":"100","bid":"99","ask":"101","volume":"10","high":"105","low":"95","open":"98","timestamp":1700000000000}
            """;
        var handler = new CapturingHandler(json);
        var client = new BitvavoRestClient(new HttpClient(handler), new BitvavoOptions { RestBaseUrl = "https://api.bitvavo.com/v2" });

        var ticker = await client.GetTicker24hAsync("BTC-EUR", CancellationToken.None);

        Assert.Equal("https://api.bitvavo.com/v2/ticker/24h?market=BTC-EUR", handler.CapturedRequestUri?.ToString());
        Assert.Equal("BTC-EUR", ticker.Market);
    }

    [Fact]
    public async Task GetOrderBookAsync_BuildsMarketScopedBookPath()
    {
        const string json = """{"market":"BTC-EUR","bids":[],"asks":[]}""";
        var handler = new CapturingHandler(json);
        var client = new BitvavoRestClient(new HttpClient(handler), new BitvavoOptions { RestBaseUrl = "https://api.bitvavo.com/v2" });

        await client.GetOrderBookAsync("BTC-EUR", 25, CancellationToken.None);

        Assert.Equal("https://api.bitvavo.com/v2/BTC-EUR/book?depth=25", handler.CapturedRequestUri?.ToString());
    }

    /// <summary>
    /// Regression test: Bitvavo's live response for "pricePrecision" is not guaranteed to be a
    /// plain JSON integer — a decimal-formatted number (5.0) or a numeric string ("5") both make
    /// the default System.Text.Json int conversion throw ("The JSON value could not be converted
    /// to System.Int32"), which broke loading markets for the bot wizard entirely.
    /// </summary>
    [Theory]
    [InlineData("5")]
    [InlineData("5.0")]
    [InlineData("\"5\"")]
    public async Task GetMarketsAsync_AcceptsPricePrecisionInAnyReasonableJsonForm(string pricePrecisionJson)
    {
        var json = $$"""
            [{"market":"BTC-EUR","status":"trading","base":"BTC","quote":"EUR","pricePrecision":{{pricePrecisionJson}},"minOrderInBaseAsset":"0.001","minOrderInQuoteAsset":"5"}]
            """;
        var handler = new CapturingHandler(json);
        var client = new BitvavoRestClient(new HttpClient(handler), new BitvavoOptions { RestBaseUrl = "https://api.bitvavo.com/v2" });

        var markets = await client.GetMarketsAsync(CancellationToken.None);

        var market = Assert.Single(markets);
        Assert.Equal(5, market.PricePrecision);
    }

    /// <summary>
    /// Regression test: Bitvavo returns "pricePrecision": null for at least some markets. That
    /// must fall back to a safe default rather than throwing (which broke loading markets a
    /// second time after the decimal/string-form fix) or defaulting to 0 (which would round every
    /// price for that market down to a whole number).
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_TreatsNullPricePrecisionAsSafeDefaultInsteadOfThrowing()
    {
        const string json = """
            [{"market":"BTC-EUR","status":"trading","base":"BTC","quote":"EUR","pricePrecision":null,"minOrderInBaseAsset":"0.001","minOrderInQuoteAsset":"5"}]
            """;
        var handler = new CapturingHandler(json);
        var client = new BitvavoRestClient(new HttpClient(handler), new BitvavoOptions { RestBaseUrl = "https://api.bitvavo.com/v2" });

        var markets = await client.GetMarketsAsync(CancellationToken.None);

        var market = Assert.Single(markets);
        Assert.True(market.PricePrecision > 0);
    }
}
