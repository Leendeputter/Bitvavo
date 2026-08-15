using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Interfaces;
using Procurement.Core.Mocking;
using Procurement.Core.Models;

namespace Procurement.Suppliers.Other
{
    /// <summary>
    /// Mock-only ISupplierAdapter for a distributor with no confirmed public API yet (Arrow,
    /// Rutronik, Avnet/Silica, Karl Kruse, RS Components, Distrelec, Conrad Business Supplies —
    /// see the SupplierCode constants in Procurement.UI.Composition.CompositionRoot). One class
    /// serves all of them, parameterized by supplier code/name, so the sourcing/comparison engine
    /// can already run demos across all configured distributors while each one's real API/EDI
    /// integration is confirmed and built out individually later (following the DigiKeyAdapter/
    /// FarnellAdapter/MouserAdapter/TmeAdapter pattern once that happens).
    /// </summary>
    public class GenericMockSupplierAdapter : ISupplierAdapter
    {
        private readonly string _supplierCode;
        private readonly string _datasheetHost;
        private readonly GenericMockSupplierOptions _options;

        // Lines kept alongside the result so GetOrderStatusAsync can echo a per-line mock
        // confirmation back (MockOrderConfirmationBuilder) — CreateOrderAsync's own result has no
        // line-level detail, only totals.
        private readonly ConcurrentDictionary<string, (SupplierOrderResult Result, List<SupplierOrderRequestLine> Lines)> _ordersByIdempotencyKey =
            new ConcurrentDictionary<string, (SupplierOrderResult Result, List<SupplierOrderRequestLine> Lines)>();

        public string SupplierCode => _supplierCode;

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

        public GenericMockSupplierAdapter(string supplierCode, string datasheetHost, GenericMockSupplierOptions options)
        {
            if (string.IsNullOrWhiteSpace(supplierCode)) throw new ArgumentNullException(nameof(supplierCode));
            _supplierCode = supplierCode;
            _datasheetHost = datasheetHost ?? "example.invalid";
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public Task<IReadOnlyList<SupplierProduct>> SearchProductsAsync(ProductSearchRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            EnsureMockMode();

            if (string.IsNullOrWhiteSpace(request.ManufacturerPartNumber))
            {
                throw new SupplierException(_supplierCode, SupplierErrorCode.ProductNotFound,
                    $"ManufacturerPartNumber is required to search {_supplierCode} (mock).");
            }

            var product = GenericMockDataProvider.BuildProduct(_supplierCode, request.Manufacturer, request.ManufacturerPartNumber, _datasheetHost);
            IReadOnlyList<SupplierProduct> result = new List<SupplierProduct> { product };
            return Task.FromResult(result);
        }

        public Task<SupplierProduct> GetProductAsync(string supplierPartNumber)
        {
            EnsureMockMode();
            EnsureKnownPart(supplierPartNumber);

            var product = new SupplierProduct
            {
                SupplierPartNumber = supplierPartNumber,
                Manufacturer = "(onbekend)",
                ManufacturerPartNumber = supplierPartNumber,
                Description = supplierPartNumber,
                MatchConfidence = MatchConfidence.Medium,
                DatasheetUrl = $"https://{_datasheetHost}/datasheets/{supplierPartNumber}.pdf"
            };
            return Task.FromResult(product);
        }

        public Task<SupplierAvailability> GetAvailabilityAsync(string supplierPartNumber)
        {
            EnsureMockMode();
            EnsureKnownPart(supplierPartNumber);
            return Task.FromResult(GenericMockDataProvider.BuildAvailability(supplierPartNumber));
        }

        public Task<SupplierPricing> GetPricingAsync(string supplierPartNumber, int quantity)
        {
            EnsureMockMode();
            if (quantity <= 0)
                throw new SupplierException(_supplierCode, SupplierErrorCode.InvalidQuantity, "Quantity must be positive.");
            EnsureKnownPart(supplierPartNumber);
            return Task.FromResult(GenericMockDataProvider.BuildPricing(supplierPartNumber, quantity));
        }

        public Task<IReadOnlyList<SupplierPackagingOption>> GetPackagingOptionsAsync(string supplierPartNumber, int quantity)
        {
            EnsureMockMode();
            EnsureKnownPart(supplierPartNumber);
            return Task.FromResult(GenericMockDataProvider.BuildPackagingOptions(supplierPartNumber, quantity));
        }

        public Task<SupplierOrderResult> CreateOrderAsync(SupplierOrderRequest request, string idempotencyKey)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentNullException(nameof(idempotencyKey));
            EnsureMockMode();

            if (_ordersByIdempotencyKey.TryGetValue(idempotencyKey, out var existing))
                return Task.FromResult(existing.Result);

            if (request.Lines == null || request.Lines.Count == 0)
            {
                throw new SupplierException(_supplierCode, SupplierErrorCode.InvalidQuantity, "Order has no lines.");
            }

            foreach (var line in request.Lines)
            {
                var availability = GenericMockDataProvider.BuildAvailability(line.SupplierPartNumber);
                if (line.Quantity > availability.AvailableQuantity)
                {
                    throw new SupplierException(_supplierCode, SupplierErrorCode.InsufficientStock,
                        $"Requested {line.Quantity} of {line.SupplierPartNumber} but only {availability.AvailableQuantity} available.");
                }
            }

            var orderTotal = request.Lines.Sum(l => l.Quantity * l.UnitPrice);
            var result = new SupplierOrderResult
            {
                Success = true,
                SupplierOrderNumber = $"{_supplierCode}{DateTime.UtcNow:yyyyMMddHHmmss}{new Random(idempotencyKey.GetHashCode()).Next(100, 999)}",
                Status = PurchaseOrderStatus.Acknowledged,
                OrderTotal = orderTotal,
                Currency = request.Currency ?? "EUR",
                SubmittedAt = DateTime.UtcNow
            };

            _ordersByIdempotencyKey[idempotencyKey] = (result, request.Lines);
            return Task.FromResult(result);
        }

        public Task<Core.Models.SupplierOrderStatus> GetOrderStatusAsync(string supplierOrderNumber)
        {
            EnsureMockMode();

            var placed = _ordersByIdempotencyKey.Values.FirstOrDefault(o => o.Result.SupplierOrderNumber == supplierOrderNumber);
            if (placed.Result == null)
            {
                throw new SupplierException(_supplierCode, SupplierErrorCode.ProductNotFound,
                    $"No mock order found with number {supplierOrderNumber}.");
            }

            return Task.FromResult(new Core.Models.SupplierOrderStatus
            {
                SupplierOrderNumber = supplierOrderNumber,
                Status = PurchaseOrderStatus.Confirmed,
                ConfirmedAt = DateTime.UtcNow,
                Lines = placed.Lines.Select(l => MockOrderConfirmationBuilder.BuildLine(supplierOrderNumber, l)).ToList()
            });
        }

        public Task<bool> CancelOrderAsync(string supplierOrderNumber)
        {
            EnsureMockMode();
            var key = _ordersByIdempotencyKey.FirstOrDefault(kv => kv.Value.Result.SupplierOrderNumber == supplierOrderNumber).Key;
            if (key == null) return Task.FromResult(false);
            return Task.FromResult(_ordersByIdempotencyKey.TryRemove(key, out _));
        }

        private void EnsureMockMode()
        {
            if (!_options.UseMockData)
            {
                throw new SupplierException(_supplierCode, SupplierErrorCode.UnknownError,
                    $"{_supplierCode} has no confirmed API/EDI integration built yet — GenericMockSupplierAdapter only supports UseMockData=true. " +
                    "Set UseMockData back to true, or build a dedicated adapter+HTTP layer for this supplier first (see DigiKeyAdapter for the pattern).");
            }
        }

        private void EnsureKnownPart(string supplierPartNumber)
        {
            if (string.IsNullOrWhiteSpace(supplierPartNumber))
            {
                throw new SupplierException(_supplierCode, SupplierErrorCode.ProductNotFound, "SupplierPartNumber is required.");
            }
        }
    }
}
