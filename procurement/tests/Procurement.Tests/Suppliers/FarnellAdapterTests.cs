using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Interfaces;
using Procurement.Core.Enums;
using Procurement.Core.Models;
using Procurement.Suppliers.Farnell;
using Xunit;

namespace Procurement.Tests.Suppliers
{
    public class FarnellAdapterTests
    {
        private static FarnellAdapter BuildAdapter() => new FarnellAdapter(new FarnellOptions { UseMockData = true });

        [Fact]
        public async Task SearchProducts_ExamplePart_ReturnsExactMatch()
        {
            var adapter = BuildAdapter();

            var results = await adapter.SearchProductsAsync(new ProductSearchRequest
            {
                Manufacturer = FarnellMockDataProvider.ExampleManufacturer,
                ManufacturerPartNumber = FarnellMockDataProvider.ExampleMpn,
                Quantity = 12000
            });

            var product = Assert.Single(results);
            Assert.Equal(MatchConfidence.Exact, product.MatchConfidence);
            Assert.Equal("FN-CRCW060310K0FKEA", product.SupplierPartNumber);
        }

        [Fact]
        public async Task GetPricing_ExamplePart_MatchesSpecExample()
        {
            var adapter = BuildAdapter();

            var pricing = await adapter.GetPricingAsync("FN-CRCW060310K0FKEA", 12000);

            Assert.Equal(0.115m, pricing.UnitPrice);
            Assert.Equal(2500, pricing.OrderMultiple);
        }

        [Fact]
        public async Task CreateOrder_SameIdempotencyKeyTwice_ReturnsSameOrderNumber()
        {
            var adapter = BuildAdapter();
            var idempotencyKey = "PROC-2026-000001-FARNELL";
            var request = new SupplierOrderRequest
            {
                SupplierCode = "FARNELL",
                ErpPoNumber = "PO-TEST-1",
                Currency = "EUR",
                Lines = { new SupplierOrderRequestLine { SupplierPartNumber = "FN-CRCW060310K0FKEA", Quantity = 2500, UnitPrice = 0.115m } }
            };

            var first = await adapter.CreateOrderAsync(request, idempotencyKey);
            var second = await adapter.CreateOrderAsync(request, idempotencyKey);

            Assert.Equal(first.SupplierOrderNumber, second.SupplierOrderNumber);
        }

        /// <summary>Real-mode JSON for CRCW040210K0FKED (24 nov 2026): full reel, re-reel and cut tape codes.</summary>
        private class FakeFarnellHttp : ISupplierHttpClient
        {
            private const string Reel = "{\"sku\":\"2121730\",\"translatedManufacturerPartNumber\":\"CRCW040210K0FKED\",\"brandName\":\"VISHAY\",\"translatedMinimumOrderQuality\":10000,\"translatedMultipleQuality\":0,\"stock\":{\"level\":24601045},\"prices\":[{\"from\":10000,\"to\":99999,\"cost\":0.0045}]}";
            private const string ReReel = "{\"sku\":\"1469669RL\",\"translatedManufacturerPartNumber\":\"CRCW040210K0FKED\",\"brandName\":\"VISHAY\",\"translatedMinimumOrderQuality\":500,\"translatedMultipleQuality\":10,\"stock\":{\"level\":24601045},\"prices\":[{\"from\":500,\"to\":2499,\"cost\":0.0069},{\"from\":5000,\"to\":99999,\"cost\":0.0052}]}";
            private const string Tape = "{\"sku\":\"1469669\",\"translatedManufacturerPartNumber\":\"CRCW040210K0FKED\",\"brandName\":\"VISHAY\",\"translatedMinimumOrderQuality\":10,\"translatedMultipleQuality\":10,\"stock\":{\"level\":24601045},\"prices\":[{\"from\":10,\"to\":99,\"cost\":0.0124},{\"from\":5000,\"to\":99999,\"cost\":0.0052}]}";

            public Task<string> GetAsync(string relativeUrl)
            {
                var products = relativeUrl.Contains("manuPartNum") ? Reel + "," + ReReel + "," + Tape : Reel;
                return Task.FromResult("{\"premierFarnellPartNumberReturn\":{\"numberOfResults\":1,\"products\":[" + products + "]}}");
            }

            public Task<string> PostAsync(string relativeUrl, string jsonBody) => throw new System.NotSupportedException();
        }

        [Fact]
        public async Task GetPackagingOptions_RealMode_ReturnsEveryOrderCodeOfThePart()
        {
            var adapter = new FarnellAdapter(new FarnellOptions { UseMockData = false }, new FakeFarnellHttp());

            var options = await adapter.GetPackagingOptionsAsync("2121730", 6600);

            Assert.Equal(3, options.Count);
            var reel = options.Single(o => o.SupplierPartNumber == "2121730");
            Assert.Equal(PackagingType.OriginalReel, reel.PackagingType);
            Assert.Equal(10000, reel.PackagingQuantity);
            Assert.Equal(PackagingType.ReReel, options.Single(o => o.SupplierPartNumber == "1469669RL").PackagingType);
            var tape = options.Single(o => o.SupplierPartNumber == "1469669");
            Assert.Equal(PackagingType.CutTape, tape.PackagingType);
            Assert.Equal(10, tape.OrderMultiple);
        }
    }
}
