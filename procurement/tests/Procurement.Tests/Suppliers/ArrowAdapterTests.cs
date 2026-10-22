using System.Threading.Tasks;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Models;
using Procurement.Suppliers.Arrow;
using Xunit;

namespace Procurement.Tests.Suppliers
{
    public class ArrowAdapterTests
    {
        private static ArrowAdapter BuildAdapter() => new ArrowAdapter(new ArrowOptions { UseMockData = true });

        [Fact]
        public async Task SearchProducts_ExamplePart_ReturnsExactMatch()
        {
            var adapter = BuildAdapter();

            var results = await adapter.SearchProductsAsync(new ProductSearchRequest
            {
                Manufacturer = ArrowMockDataProvider.ExampleManufacturer,
                ManufacturerPartNumber = ArrowMockDataProvider.ExampleMpn,
                Quantity = 12000
            });

            var product = Assert.Single(results);
            Assert.Equal(MatchConfidence.Exact, product.MatchConfidence);
            Assert.Equal("ARW-CRCW060310K0FKEA", product.SupplierPartNumber);
        }

        [Fact]
        public async Task CreateOrder_SameIdempotencyKeyTwice_ReturnsSameOrderNumber()
        {
            var adapter = BuildAdapter();
            var idempotencyKey = "PROC-2026-000001-ARROW";
            var request = new SupplierOrderRequest
            {
                SupplierCode = "ARROW",
                ErpPoNumber = "PO-TEST-1",
                Currency = "EUR",
                Lines = { new SupplierOrderRequestLine { SupplierPartNumber = "ARW-CRCW060310K0FKEA", Quantity = 3000, UnitPrice = 0.102m } }
            };

            var first = await adapter.CreateOrderAsync(request, idempotencyKey);
            var second = await adapter.CreateOrderAsync(request, idempotencyKey);

            Assert.Equal(first.SupplierOrderNumber, second.SupplierOrderNumber);
        }

        [Fact]
        public async Task RealMode_Search_ThrowsClearSupplierException_UnconfirmedResponseShape()
        {
            var adapter = new ArrowAdapter(new ArrowOptions { UseMockData = false });

            var ex = await Assert.ThrowsAsync<SupplierException>(() => adapter.SearchProductsAsync(new ProductSearchRequest
            {
                Manufacturer = "Vishay",
                ManufacturerPartNumber = "CRCW060310K0FKEA"
            }));

            Assert.Equal("ARROW", ex.SupplierCode);
        }

        [Fact]
        public async Task RealMode_CreateOrder_ThrowsClearSupplierException_UnconfirmedRequestShape()
        {
            var adapter = new ArrowAdapter(new ArrowOptions { UseMockData = false });
            var request = new SupplierOrderRequest
            {
                SupplierCode = "ARROW",
                ErpPoNumber = "PO-TEST-2",
                Currency = "EUR",
                Lines = { new SupplierOrderRequestLine { SupplierPartNumber = "ARW-CRCW060310K0FKEA", Quantity = 1, UnitPrice = 0.102m } }
            };

            var ex = await Assert.ThrowsAsync<SupplierException>(() => adapter.CreateOrderAsync(request, "PROC-2026-000002-ARROW"));

            Assert.Equal("ARROW", ex.SupplierCode);
        }
    }
}
