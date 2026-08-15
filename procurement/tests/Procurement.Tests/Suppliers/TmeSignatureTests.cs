using System.Collections.Generic;
using Procurement.Suppliers.TME;
using Procurement.Suppliers.TME.Http;
using Xunit;

namespace Procurement.Tests.Suppliers
{
    /// <summary>
    /// TME's HMAC-SHA1 request signing (TmeHttpClientWrapper.ComputeSignature) is the least
    /// verified piece of the whole supplier layer — reproduced from memory of TME's docs, never
    /// exercised against a real account. This test can't prove TME's server accepts the result,
    /// but it does pin the exact algorithm (RFC3986 percent-encoding, alphabetical param sort,
    /// "POST&amp;{url}&amp;{params}" signature base, HMAC-SHA1, base64) against an expected value
    /// computed independently in Python (hmac/hashlib/urllib.parse — see the commit that added
    /// this test for the exact script), so a future regression here fails loudly instead of
    /// silently producing a different-but-still-base64-shaped signature.
    /// </summary>
    public class TmeSignatureTests
    {
        [Fact]
        public void ComputeSignature_KnownInput_MatchesIndependentlyComputedReference()
        {
            var options = new TmeOptions { Token = "TESTTOKEN", ApiKey = "TESTSECRET", IsSandbox = false };
            var wrapper = new TmeHttpClientWrapper(options);

            var parameters = new Dictionary<string, string>
            {
                ["SearchPlain"] = "CRCW060310K0FKEA",
                ["Token"] = "TESTTOKEN",
                ["Country"] = "NL",
                ["Language"] = "EN",
                ["Currency"] = "EUR"
            };

            var signature = wrapper.ComputeSignature("https://api.tme.eu/Products/Search.json", parameters);

            Assert.Equal("NnIospiqlm4xUJY96e8xvfvviHU=", signature);
        }

        [Fact]
        public void ComputeSignature_IsDeterministic_ForTheSameInput()
        {
            var options = new TmeOptions { Token = "TESTTOKEN", ApiKey = "TESTSECRET" };
            var wrapper = new TmeHttpClientWrapper(options);
            var parameters = new Dictionary<string, string> { ["SymbolList[0]"] = "AT-CRCW0603" };

            var first = wrapper.ComputeSignature("https://apitest.tme.eu/Products/GetPrices.json", parameters);
            var second = wrapper.ComputeSignature("https://apitest.tme.eu/Products/GetPrices.json", parameters);

            Assert.Equal(first, second);
        }

        [Fact]
        public void ComputeSignature_DifferentSecret_ProducesDifferentSignature()
        {
            var parameters = new Dictionary<string, string> { ["SearchPlain"] = "CRCW060310K0FKEA" };
            var signatureA = new TmeHttpClientWrapper(new TmeOptions { ApiKey = "SECRET-A" })
                .ComputeSignature("https://api.tme.eu/Products/Search.json", parameters);
            var signatureB = new TmeHttpClientWrapper(new TmeOptions { ApiKey = "SECRET-B" })
                .ComputeSignature("https://api.tme.eu/Products/Search.json", parameters);

            Assert.NotEqual(signatureA, signatureB);
        }
    }
}
