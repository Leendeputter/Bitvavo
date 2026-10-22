using System.Threading.Tasks;
using Procurement.Core.Exceptions;
using Procurement.Core.Models;
using Procurement.Suppliers.Other;
using Xunit;

namespace Procurement.Tests.Suppliers
{
    /// <summary>
    /// One adapter class serves five distributors (Avnet/Silica, Karl Kruse, RS Components,
    /// Distrelec, Conrad) that have no confirmed API yet — Arrow and Rutronik used to be on this
    /// list too, but both got dedicated adapters once their contracts were confirmed (22 okt 2026,
    /// see ArrowAdapter/RutronikAdapter and CompositionRoot). These tests just confirm this class
    /// behaves consistently for an arbitrary supplier code, and that it fails loudly instead of
    /// silently pretending to be real when someone flips UseMockData off before a real adapter for
    /// that supplier has actually been built (see CompositionRoot.genericSupplierDefinitions).
    /// </summary>
    public class GenericMockSupplierAdapterTests
    {
        private static GenericMockSupplierAdapter BuildAdapter(string code = "AVNET_SILICA") =>
            new GenericMockSupplierAdapter(code, "www.avnet.com", new GenericMockSupplierOptions { UseMockData = true });

        [Fact]
        public async Task SearchProducts_ReturnsDeterministicMockProduct_TaggedWithSupplierCode()
        {
            var adapter = BuildAdapter("AVNET_SILICA");

            var results = await adapter.SearchProductsAsync(new ProductSearchRequest
            {
                Manufacturer = "Vishay",
                ManufacturerPartNumber = "CRCW060310K0FKEA",
                Quantity = 12000
            });

            var product = Assert.Single(results);
            Assert.StartsWith("AVN-", product.SupplierPartNumber);
            Assert.Equal("AVNET_SILICA", adapter.SupplierCode);
        }

        [Fact]
        public async Task SearchProducts_SameInput_IsDeterministicAcrossCalls()
        {
            var adapter = BuildAdapter("RS_COMPONENTS");
            var request = new ProductSearchRequest { Manufacturer = "Vishay", ManufacturerPartNumber = "CRCW060310K0FKEA" };

            var first = await adapter.SearchProductsAsync(request);
            var second = await adapter.SearchProductsAsync(request);

            Assert.Equal(first[0].SupplierPartNumber, second[0].SupplierPartNumber);
        }

        [Fact]
        public async Task SearchProducts_DifferentSupplierCodes_ProduceDifferentPartNumbers()
        {
            var request = new ProductSearchRequest { Manufacturer = "Vishay", ManufacturerPartNumber = "CRCW060310K0FKEA" };

            var avnetResult = await BuildAdapter("AVNET_SILICA").SearchProductsAsync(request);
            var rsResult = await BuildAdapter("RS_COMPONENTS").SearchProductsAsync(request);

            Assert.NotEqual(avnetResult[0].SupplierPartNumber, rsResult[0].SupplierPartNumber);
        }

        [Fact]
        public async Task CreateOrder_SameIdempotencyKeyTwice_ReturnsSameOrderNumber()
        {
            var adapter = BuildAdapter("KARL_KRUSE");
            var idempotencyKey = "PROC-2026-000001-KARL_KRUSE";
            var request = new SupplierOrderRequest
            {
                SupplierCode = "KARL_KRUSE",
                ErpPoNumber = "PO-TEST-1",
                Currency = "EUR",
                Lines = { new SupplierOrderRequestLine { SupplierPartNumber = "KAR-000001", Quantity = 100, UnitPrice = 0.5m } }
            };

            var first = await adapter.CreateOrderAsync(request, idempotencyKey);
            var second = await adapter.CreateOrderAsync(request, idempotencyKey);

            Assert.Equal(first.SupplierOrderNumber, second.SupplierOrderNumber);
        }

        [Fact]
        public async Task RealMode_ThrowsClearSupplierException_InsteadOfPretendingToBeReal()
        {
            var adapter = new GenericMockSupplierAdapter("DISTRELEC", "www.distrelec.nl",
                new GenericMockSupplierOptions { UseMockData = false });

            var ex = await Assert.ThrowsAsync<SupplierException>(() => adapter.SearchProductsAsync(new ProductSearchRequest
            {
                Manufacturer = "Vishay",
                ManufacturerPartNumber = "CRCW060310K0FKEA"
            }));

            Assert.Equal("DISTRELEC", ex.SupplierCode);
        }
    }
}
