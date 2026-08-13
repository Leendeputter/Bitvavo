using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Interfaces;
using Procurement.Core.Models;

namespace Procurement.Suppliers.Farnell
{
    public class FarnellAdapter : ISupplierAdapter
    {
        public const string Code = "FARNELL";

        private readonly FarnellOptions _options;
        private readonly ISupplierHttpClient _httpClient;

        private static readonly ConcurrentDictionary<string, SupplierOrderResult> OrdersByIdempotencyKey =
            new ConcurrentDictionary<string, SupplierOrderResult>();

        public string SupplierCode => Code;

        public SupplierCapabilities Capabilities { get; } = new SupplierCapabilities
        {
            ProductSearch = CapabilityStatus.Supported,
            Pricing = CapabilityStatus.Supported,
            Availability = CapabilityStatus.Supported,
            Packaging = CapabilityStatus.Supported,
            Ordering = CapabilityStatus.Supported,
            OrderStatus = CapabilityStatus.Supported,
            Shipment = CapabilityStatus.ManualProcess,
            Tracking = CapabilityStatus.ManualProcess,
            Invoice = CapabilityStatus.ManualProcess
        };

        public FarnellAdapter(FarnellOptions options, ISupplierHttpClient httpClient = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClient;
        }

        public Task<IReadOnlyList<SupplierProduct>> SearchProductsAsync(ProductSearchRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            EnsureMockMode();

            if (string.IsNullOrWhiteSpace(request.ManufacturerPartNumber))
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    "ManufacturerPartNumber is required to search Farnell (mock).");
            }

            var product = FarnellMockDataProvider.BuildProduct(request.Manufacturer, request.ManufacturerPartNumber);
            IReadOnlyList<SupplierProduct> result = new List<SupplierProduct> { product };
            return Task.FromResult(result);
        }

        public Task<SupplierProduct> GetProductAsync(string supplierPartNumber)
        {
            EnsureMockMode();
            EnsureKnownPart(supplierPartNumber);

            var isExample = supplierPartNumber == "FN-CRCW060310K0FKEA";
            var product = new SupplierProduct
            {
                SupplierPartNumber = supplierPartNumber,
                Manufacturer = isExample ? FarnellMockDataProvider.ExampleManufacturer : "(onbekend)",
                ManufacturerPartNumber = isExample ? FarnellMockDataProvider.ExampleMpn : supplierPartNumber,
                Description = isExample
                    ? $"{FarnellMockDataProvider.ExampleManufacturer} {FarnellMockDataProvider.ExampleMpn}"
                    : supplierPartNumber,
                MatchConfidence = isExample ? MatchConfidence.Exact : MatchConfidence.Medium,
                DatasheetUrl = $"https://www.farnell.com/datasheets/{supplierPartNumber}.pdf"
            };
            return Task.FromResult(product);
        }

        public Task<SupplierAvailability> GetAvailabilityAsync(string supplierPartNumber)
        {
            EnsureMockMode();
            EnsureKnownPart(supplierPartNumber);
            return Task.FromResult(FarnellMockDataProvider.BuildAvailability(supplierPartNumber));
        }

        public Task<SupplierPricing> GetPricingAsync(string supplierPartNumber, int quantity)
        {
            EnsureMockMode();
            if (quantity <= 0)
                throw new SupplierException(Code, SupplierErrorCode.InvalidQuantity, "Quantity must be positive.");
            EnsureKnownPart(supplierPartNumber);
            return Task.FromResult(FarnellMockDataProvider.BuildPricing(supplierPartNumber, quantity));
        }

        public Task<IReadOnlyList<SupplierPackagingOption>> GetPackagingOptionsAsync(string supplierPartNumber, int quantity)
        {
            EnsureMockMode();
            EnsureKnownPart(supplierPartNumber);
            return Task.FromResult(FarnellMockDataProvider.BuildPackagingOptions(supplierPartNumber, quantity));
        }

        public Task<SupplierOrderResult> CreateOrderAsync(SupplierOrderRequest request, string idempotencyKey)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentNullException(nameof(idempotencyKey));
            EnsureMockMode();

            if (OrdersByIdempotencyKey.TryGetValue(idempotencyKey, out var existing))
                return Task.FromResult(existing);

            if (request.Lines == null || request.Lines.Count == 0)
            {
                throw new SupplierException(Code, SupplierErrorCode.InvalidQuantity, "Order has no lines.");
            }

            foreach (var line in request.Lines)
            {
                var availability = FarnellMockDataProvider.BuildAvailability(line.SupplierPartNumber);
                if (line.Quantity > availability.AvailableQuantity)
                {
                    throw new SupplierException(Code, SupplierErrorCode.InsufficientStock,
                        $"Requested {line.Quantity} of {line.SupplierPartNumber} but only {availability.AvailableQuantity} available.");
                }
            }

            var orderTotal = request.Lines.Sum(l => l.Quantity * l.UnitPrice);
            var result = new SupplierOrderResult
            {
                Success = true,
                SupplierOrderNumber = $"FN{DateTime.UtcNow:yyyyMMddHHmmss}{new Random(idempotencyKey.GetHashCode()).Next(100, 999)}",
                Status = SupplierOrderStatusEnum.Acknowledged,
                OrderTotal = orderTotal,
                Currency = request.Currency ?? "EUR",
                SubmittedAt = DateTime.UtcNow
            };

            OrdersByIdempotencyKey[idempotencyKey] = result;
            return Task.FromResult(result);
        }

        public Task<Core.Models.SupplierOrderStatus> GetOrderStatusAsync(string supplierOrderNumber)
        {
            EnsureMockMode();

            var placed = OrdersByIdempotencyKey.Values.FirstOrDefault(o => o.SupplierOrderNumber == supplierOrderNumber);
            if (placed == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"No mock order found with number {supplierOrderNumber}.");
            }

            return Task.FromResult(new Core.Models.SupplierOrderStatus
            {
                SupplierOrderNumber = supplierOrderNumber,
                Status = SupplierOrderStatusEnum.Confirmed,
                ConfirmedAt = DateTime.UtcNow
            });
        }

        public Task<bool> CancelOrderAsync(string supplierOrderNumber)
        {
            EnsureMockMode();
            var key = OrdersByIdempotencyKey.FirstOrDefault(kv => kv.Value.SupplierOrderNumber == supplierOrderNumber).Key;
            if (key == null) return Task.FromResult(false);
            return Task.FromResult(OrdersByIdempotencyKey.TryRemove(key, out _));
        }

        private void EnsureMockMode()
        {
            if (!_options.UseMockData)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "FarnellAdapter only supports UseMockData=true in this prototype.");
            }
        }

        private static void EnsureKnownPart(string supplierPartNumber)
        {
            if (string.IsNullOrWhiteSpace(supplierPartNumber))
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound, "SupplierPartNumber is required.");
            }
        }
    }
}
