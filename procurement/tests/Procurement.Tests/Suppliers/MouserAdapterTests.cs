using System.Threading.Tasks;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Models;
using Procurement.Suppliers.Mouser;
using Xunit;

namespace Procurement.Tests.Suppliers
{
    public class MouserAdapterTests
    {
        private static MouserAdapter BuildAdapter() => new MouserAdapter(new MouserOptions { UseMockData = true });

        [Fact]
        public async Task SearchProducts_ExamplePart_ReturnsExactMatch()
        {
            var adapter = BuildAdapter();

            var results = await adapter.SearchProductsAsync(new ProductSearchRequest
            {
                Manufacturer = MouserMockDataProvider.ExampleManufacturer,
                ManufacturerPartNumber = MouserMockDataProvider.ExampleMpn,
                Quantity = 12000
            });

            var product = Assert.Single(results);
            Assert.Equal(MatchConfidence.Exact, product.MatchConfidence);
            Assert.Equal("MO-CRCW060310K0FKEA", product.SupplierPartNumber);
        }

        [Fact]
        public async Task GetPricing_ExamplePart_ReturnsFixedMockPrice()
        {
            var adapter = BuildAdapter();

            var pricing = await adapter.GetPricingAsync("MO-CRCW060310K0FKEA", 12000);

            Assert.Equal(0.112m, pricing.UnitPrice);
            Assert.Equal(4000, pricing.OrderMultiple);
        }

        [Fact]
        public async Task CreateOrder_SameIdempotencyKeyTwice_ReturnsSameOrderNumber()
        {
            var adapter = BuildAdapter();
            var idempotencyKey = "PROC-2026-000001-MOUSER";
            var request = new SupplierOrderRequest
            {
                SupplierCode = "MOUSER",
                ErpPoNumber = "PO-TEST-1",
                Currency = "EUR",
                Lines = { new SupplierOrderRequestLine { SupplierPartNumber = "MO-CRCW060310K0FKEA", Quantity = 4000, UnitPrice = 0.112m } }
            };

            var first = await adapter.CreateOrderAsync(request, idempotencyKey);
            var second = await adapter.CreateOrderAsync(request, idempotencyKey);

            Assert.Equal(first.SupplierOrderNumber, second.SupplierOrderNumber);
        }

        [Fact]
        public async Task RealMode_WithoutHttpClient_ThrowsInsteadOfCrashing()
        {
            var adapter = new MouserAdapter(new MouserOptions { UseMockData = false });

            var ex = await Assert.ThrowsAsync<SupplierException>(() => adapter.SearchProductsAsync(new ProductSearchRequest
            {
                Manufacturer = "Vishay",
                ManufacturerPartNumber = "CRCW060310K0FKEA"
            }));

            Assert.Equal("MOUSER", ex.SupplierCode);
        }

        // Mouser's real Search API returns Availability/LeadTime/Price as free text
        // (e.g. "48000 In Stock", "$0,11") rather than plain numbers — mock mode never exercises
        // this parsing, so it's tested directly here against representative real-world formats.
        [Theory]
        [InlineData("48000 In Stock", 48000)]
        [InlineData("On Order", 0)]
        [InlineData("", 0)]
        [InlineData(null, 0)]
        public void ParseLeadingInt_HandlesMouserAvailabilityFormats(string input, int expected)
        {
            Assert.Equal(expected, MouserAdapter.ParseLeadingInt(input));
        }

        [Theory]
        [InlineData("$0.11", 0.11)]
        [InlineData("€0,11", 0.11)]
        [InlineData("$1,234.56", 1234.56)]
        [InlineData("", 0)]
        [InlineData(null, 0)]
        public void ParseCurrency_HandlesMouserPriceFormats(string input, double expected)
        {
            Assert.Equal((decimal)expected, MouserAdapter.ParseCurrency(input));
        }
    }
}
