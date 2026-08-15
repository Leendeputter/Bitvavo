using System.Threading.Tasks;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Models;
using Procurement.Suppliers.TME;
using Xunit;

namespace Procurement.Tests.Suppliers
{
    public class TmeAdapterTests
    {
        private static TmeAdapter BuildAdapter() => new TmeAdapter(new TmeOptions { UseMockData = true });

        [Fact]
        public async Task SearchProducts_ExamplePart_ReturnsExactMatch()
        {
            var adapter = BuildAdapter();

            var results = await adapter.SearchProductsAsync(new ProductSearchRequest
            {
                Manufacturer = TmeMockDataProvider.ExampleManufacturer,
                ManufacturerPartNumber = TmeMockDataProvider.ExampleMpn,
                Quantity = 12000
            });

            var product = Assert.Single(results);
            Assert.Equal(MatchConfidence.Exact, product.MatchConfidence);
            Assert.Equal("TME-CRCW060310K0FKEA", product.SupplierPartNumber);
        }

        [Fact]
        public async Task GetPricing_ExamplePart_ReturnsFixedMockPrice()
        {
            var adapter = BuildAdapter();

            var pricing = await adapter.GetPricingAsync("TME-CRCW060310K0FKEA", 12000);

            Assert.Equal(0.118m, pricing.UnitPrice);
            Assert.Equal(3000, pricing.OrderMultiple);
        }

        [Fact]
        public async Task CreateOrder_SameIdempotencyKeyTwice_ReturnsSameOrderNumber()
        {
            var adapter = BuildAdapter();
            var idempotencyKey = "PROC-2026-000001-TME";
            var request = new SupplierOrderRequest
            {
                SupplierCode = "TME",
                ErpPoNumber = "PO-TEST-1",
                Currency = "EUR",
                Lines = { new SupplierOrderRequestLine { SupplierPartNumber = "TME-CRCW060310K0FKEA", Quantity = 3000, UnitPrice = 0.118m } }
            };

            var first = await adapter.CreateOrderAsync(request, idempotencyKey);
            var second = await adapter.CreateOrderAsync(request, idempotencyKey);

            Assert.Equal(first.SupplierOrderNumber, second.SupplierOrderNumber);
        }

        [Fact]
        public async Task RealMode_Ordering_ThrowsInsteadOfPlacingRealOrder()
        {
            var adapter = new TmeAdapter(new TmeOptions { UseMockData = false });
            var request = new SupplierOrderRequest
            {
                SupplierCode = "TME",
                ErpPoNumber = "PO-TEST-2",
                Currency = "EUR",
                Lines = { new SupplierOrderRequestLine { SupplierPartNumber = "TME-CRCW060310K0FKEA", Quantity = 1, UnitPrice = 0.118m } }
            };

            var ex = await Assert.ThrowsAsync<SupplierException>(() => adapter.CreateOrderAsync(request, "PROC-2026-000003-TME"));

            Assert.Equal("TME", ex.SupplierCode);
        }
    }
}
