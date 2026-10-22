using System.Threading.Tasks;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Models;
using Procurement.Suppliers.Rutronik;
using Xunit;

namespace Procurement.Tests.Suppliers
{
    public class RutronikAdapterTests
    {
        private static RutronikAdapter BuildAdapter() => new RutronikAdapter(new RutronikOptions { UseMockData = true });

        [Fact]
        public async Task SearchProducts_ExamplePart_ReturnsExactMatch()
        {
            var adapter = BuildAdapter();

            var results = await adapter.SearchProductsAsync(new ProductSearchRequest
            {
                Manufacturer = RutronikMockDataProvider.ExampleManufacturer,
                ManufacturerPartNumber = RutronikMockDataProvider.ExampleMpn,
                Quantity = 100
            });

            var product = Assert.Single(results);
            Assert.Equal(MatchConfidence.Exact, product.MatchConfidence);
            Assert.Equal("ICMCU22246", product.SupplierPartNumber);
        }

        [Fact]
        public async Task GetPricing_ExamplePart_ReturnsFixedMockPrice()
        {
            var adapter = BuildAdapter();

            var pricing = await adapter.GetPricingAsync("ICMCU22246", 10);

            Assert.Equal(4.12m, pricing.UnitPrice);
            Assert.Equal("EUR", pricing.Currency);
        }

        [Fact]
        public async Task RealMode_Ordering_ThrowsClearSupplierException_NoOrderApiExists()
        {
            var adapter = new RutronikAdapter(new RutronikOptions { UseMockData = false });
            var request = new SupplierOrderRequest
            {
                SupplierCode = "RUTRONIK",
                ErpPoNumber = "PO-TEST-1",
                Currency = "EUR",
                Lines = { new SupplierOrderRequestLine { SupplierPartNumber = "ICMCU22246", Quantity = 10, UnitPrice = 4.12m } }
            };

            var ex = await Assert.ThrowsAsync<SupplierException>(() => adapter.CreateOrderAsync(request, "PROC-2026-000001-RUTRONIK"));

            Assert.Equal("RUTRONIK", ex.SupplierCode);
        }

        [Fact]
        public void Capabilities_OrderingIsManualProcess_NotSupported()
        {
            var adapter = BuildAdapter();

            Assert.Equal(CapabilityStatus.ManualProcess, adapter.Capabilities.Ordering);
            Assert.Equal(CapabilityStatus.ManualProcess, adapter.Capabilities.OrderStatus);
            Assert.Equal(CapabilityStatus.Supported, adapter.Capabilities.ProductSearch);
        }
    }
}
