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
}
