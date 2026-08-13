using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Enums;
using Procurement.Core.Models;
using Procurement.Suppliers.DigiKey;
using Xunit;

namespace Procurement.Tests.Suppliers
{
    public class DigiKeyAdapterTests
    {
        private static DigiKeyAdapter BuildAdapter() => new DigiKeyAdapter(new DigiKeyOptions { UseMockData = true });

        [Fact]
        public async Task SearchProducts_ExamplePart_ReturnsExactMatch()
        {
            var adapter = BuildAdapter();

            var results = await adapter.SearchProductsAsync(new ProductSearchRequest
            {
                Manufacturer = DigiKeyMockDataProvider.ExampleManufacturer,
                ManufacturerPartNumber = DigiKeyMockDataProvider.ExampleMpn,
                Quantity = 12000
            });

            var product = Assert.Single(results);
            Assert.Equal(MatchConfidence.Exact, product.MatchConfidence);
            Assert.Equal("DK-CRCW060310K0FKEA", product.SupplierPartNumber);
        }

        [Fact]
        public async Task GetPricing_ExamplePart_MatchesSpecExample()
        {
            var adapter = BuildAdapter();

            var pricing = await adapter.GetPricingAsync("DK-CRCW060310K0FKEA", 12000);

            Assert.Equal(0.11m, pricing.UnitPrice);
            Assert.Equal(5000, pricing.OrderMultiple);
        }

        [Fact]
        public async Task GetPackagingOptions_ExamplePart_ReturnsOriginalReel()
        {
            var adapter = BuildAdapter();

            var options = await adapter.GetPackagingOptionsAsync("DK-CRCW060310K0FKEA", 12000);

            var reel = Assert.Single(options);
            Assert.Equal(PackagingType.OriginalReel, reel.PackagingType);
            Assert.Equal(5000, reel.PackagingQuantity);
            Assert.Equal(ReelType.ManufacturerOriginal, reel.ReelType);
        }

        [Fact]
        public async Task CreateOrder_SameIdempotencyKeyTwice_ReturnsSameOrderNumber_NoDuplicate()
        {
            var adapter = BuildAdapter();
            var idempotencyKey = "PROC-2026-000001-DIGIKEY";
            var request = new SupplierOrderRequest
            {
                SupplierCode = "DIGIKEY",
                ErpPoNumber = "PO-TEST-1",
                Currency = "EUR",
                Lines = { new SupplierOrderRequestLine { SupplierPartNumber = "DK-CRCW060310K0FKEA", Quantity = 5000, UnitPrice = 0.11m } }
            };

            var first = await adapter.CreateOrderAsync(request, idempotencyKey);
            var second = await adapter.CreateOrderAsync(request, idempotencyKey);

            Assert.Equal(first.SupplierOrderNumber, second.SupplierOrderNumber);
        }

        [Fact]
        public async Task CreateOrder_QuantityExceedsAvailability_ThrowsInsufficientStock()
        {
            var adapter = BuildAdapter();
            var request = new SupplierOrderRequest
            {
                SupplierCode = "DIGIKEY",
                ErpPoNumber = "PO-TEST-2",
                Currency = "EUR",
                Lines = { new SupplierOrderRequestLine { SupplierPartNumber = "DK-CRCW060310K0FKEA", Quantity = 999999999, UnitPrice = 0.11m } }
            };

            var ex = await Assert.ThrowsAsync<Procurement.Core.Exceptions.SupplierException>(
                () => adapter.CreateOrderAsync(request, "PROC-2026-000002-DIGIKEY"));

            Assert.Equal(SupplierErrorCode.InsufficientStock, ex.ErrorCode);
        }
    }
}
