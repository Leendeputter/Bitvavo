using System.Threading.Tasks;
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
    }
}
