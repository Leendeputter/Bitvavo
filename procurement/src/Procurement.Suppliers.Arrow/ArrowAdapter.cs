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
using Procurement.Suppliers.Arrow.Http;

namespace Procurement.Suppliers.Arrow
{
    /// <summary>
    /// MyArrow (my.arrow.com/api/) adapter — "voorbereid" per the user's own framing (22 okt 2026:
    /// "Arrow graag voorbereiden ... maar die is al 4 jaar niet geupdate, dat vind ik een beetje
    /// verdacht"), not fully wired up for real use yet. OAuth2 authentication
    /// (ArrowHttpClientWrapper) and the Orders Service's top-level request/response envelopes are
    /// fully confirmed from the spec and implemented for real. UseMockData defaults to true, same
    /// as every other adapter, so nothing here calls MyArrow until the user both gets real
    /// ClientId/ClientSecret from their sales rep AND flips that flag explicitly.
    ///
    /// One real, confirmed gap remains even then: the Price/Avail API's "pricingResponse" and the
    /// Orders API's "cart.lineItems"/"order.orderLines" are shown in every example in the spec as
    /// empty "{}"/"[]" placeholders — the prose describes what kind of data they carry (pricing,
    /// stock, lead time, RoHS/REACH, ...) but never names the actual JSON keys. Per this app's own
    /// rule (never guess external field names), real-mode SearchProductsAsync/GetProductAsync/
    /// GetAvailabilityAsync/GetPricingAsync/GetPackagingOptionsAsync/CreateOrderAsync all throw a
    /// clear, specific SupplierException instead of inventing a shape that could silently place a
    /// wrong-quantity or wrong-part real order. GetOrderStatusAsync is the one exception — its
    /// response's top-level fields (orderStatus/orderId/salesOrderNumber/origSysDocumentRef) ARE
    /// named explicitly in the spec, so that one is implemented for real. This also matches the
    /// user's own skepticism about the Overview section's stale "soon to come" ordering language
    /// sitting right above a fully-dated 2022 Orders Service section: build what's concretely
    /// confirmed, refuse to guess the rest, verify the live state with a sales rep before trusting
    /// any of it further.
    /// </summary>
    public class ArrowAdapter : ISupplierAdapter
    {
        public const string Code = "ARROW";

        private readonly ArrowOptions _options;
        private readonly ISupplierHttpClient _httpClient;

        // Lines kept alongside the result so GetOrderStatusAsync can echo a per-line mock
        // confirmation back (MockOrderConfirmationBuilder) — CreateOrderAsync's own result has no
        // line-level detail, only totals.
        private static readonly ConcurrentDictionary<string, (SupplierOrderResult Result, List<SupplierOrderRequestLine> Lines)> OrdersByIdempotencyKey =
            new ConcurrentDictionary<string, (SupplierOrderResult Result, List<SupplierOrderRequestLine> Lines)>();

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

        public ArrowAdapter(ArrowOptions options, ISupplierHttpClient httpClient = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClient;
        }

        public Task<IReadOnlyList<SupplierProduct>> SearchProductsAsync(ProductSearchRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.ManufacturerPartNumber))
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    "ManufacturerPartNumber is required to search Arrow.");
            }

            if (_options.UseMockData)
            {
                var product = ArrowMockDataProvider.BuildProduct(request.Manufacturer, request.ManufacturerPartNumber);
                IReadOnlyList<SupplierProduct> result = new List<SupplierProduct> { product };
                return Task.FromResult(result);
            }

            throw RealModeNotConfirmedException("priceandavail/parts (pricingResponse)");
        }

        public Task<SupplierProduct> GetProductAsync(string supplierPartNumber)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
            {
                var isExample = supplierPartNumber == "ARW-CRCW060310K0FKEA";
                return Task.FromResult(new SupplierProduct
                {
                    SupplierPartNumber = supplierPartNumber,
                    Manufacturer = isExample ? ArrowMockDataProvider.ExampleManufacturer : "(onbekend)",
                    ManufacturerPartNumber = isExample ? ArrowMockDataProvider.ExampleMpn : supplierPartNumber,
                    Description = isExample
                        ? $"{ArrowMockDataProvider.ExampleManufacturer} {ArrowMockDataProvider.ExampleMpn}"
                        : supplierPartNumber,
                    MatchConfidence = isExample ? MatchConfidence.Exact : MatchConfidence.Medium,
                    DatasheetUrl = $"https://www.arrow.com/datasheets/{supplierPartNumber}.pdf"
                });
            }

            throw RealModeNotConfirmedException("priceandavail/parts/{docId} (pricingResponse)");
        }

        public Task<SupplierAvailability> GetAvailabilityAsync(string supplierPartNumber)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return Task.FromResult(ArrowMockDataProvider.BuildAvailability(supplierPartNumber));

            throw RealModeNotConfirmedException("priceandavail/parts/{docId} (pricingResponse)");
        }

        public Task<SupplierPricing> GetPricingAsync(string supplierPartNumber, int quantity)
        {
            if (quantity <= 0)
                throw new SupplierException(Code, SupplierErrorCode.InvalidQuantity, "Quantity must be positive.");
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return Task.FromResult(ArrowMockDataProvider.BuildPricing(supplierPartNumber, quantity));

            throw RealModeNotConfirmedException("priceandavail/parts/{docId} (pricingResponse)");
        }

        public Task<IReadOnlyList<SupplierPackagingOption>> GetPackagingOptionsAsync(string supplierPartNumber, int quantity)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return Task.FromResult(ArrowMockDataProvider.BuildPackagingOptions(supplierPartNumber, quantity));

            throw RealModeNotConfirmedException("priceandavail/parts/{docId} (pricingResponse)");
        }

        public Task<SupplierOrderResult> CreateOrderAsync(SupplierOrderRequest request, string idempotencyKey)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentNullException(nameof(idempotencyKey));

            if (!_options.UseMockData)
                throw RealModeNotConfirmedException("external/orders (cart.lineItems / order.orderLines)");

            if (OrdersByIdempotencyKey.TryGetValue(idempotencyKey, out var existing))
                return Task.FromResult(existing.Result);

            if (request.Lines == null || request.Lines.Count == 0)
            {
                throw new SupplierException(Code, SupplierErrorCode.InvalidQuantity, "Order has no lines.");
            }

            foreach (var line in request.Lines)
            {
                var availability = ArrowMockDataProvider.BuildAvailability(line.SupplierPartNumber);
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
                SupplierOrderNumber = $"ARW{DateTime.UtcNow:yyyyMMddHHmmss}{new Random(idempotencyKey.GetHashCode()).Next(100, 999)}",
                Status = PurchaseOrderStatus.Acknowledged,
                OrderTotal = orderTotal,
                Currency = request.Currency ?? _options.Currency ?? "EUR",
                SubmittedAt = DateTime.UtcNow
            };

            OrdersByIdempotencyKey[idempotencyKey] = (result, request.Lines);
            return Task.FromResult(result);
        }

        public async Task<Core.Models.SupplierOrderStatus> GetOrderStatusAsync(string supplierOrderNumber)
        {
            if (!_options.UseMockData)
                return await GetRealOrderStatusAsync(supplierOrderNumber).ConfigureAwait(false);

            var placed = OrdersByIdempotencyKey.Values.FirstOrDefault(o => o.Result.SupplierOrderNumber == supplierOrderNumber);
            if (placed.Result == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"No mock order found with number {supplierOrderNumber}.");
            }

            return new Core.Models.SupplierOrderStatus
            {
                SupplierOrderNumber = supplierOrderNumber,
                Status = PurchaseOrderStatus.Confirmed,
                ConfirmedAt = DateTime.UtcNow,
                Lines = placed.Lines.Select(l => MockOrderConfirmationBuilder.BuildLine(supplierOrderNumber, l)).ToList()
            };
        }

        /// <summary>
        /// GET /orders/{orderId}/updatedStatus — unlike every other real-mode read here, this one
        /// IS implemented: its top-level fields (orderStatus/orderId/salesOrderNumber/
        /// origSysDocumentRef) are named explicitly in the spec, only its "messages"/
        /// "orderLineItems" arrays are unconfirmed placeholders — so Lines stays empty rather than
        /// guessing those. Reaching this method at all still requires an order to already exist in
        /// MyArrow (placed through some other channel, since CreateOrderAsync's real mode refuses
        /// to guess the line-item request shape), so this is mainly useful for checking the status
        /// of an order a human placed directly on the MyArrow portal.
        /// </summary>
        private async Task<Core.Models.SupplierOrderStatus> GetRealOrderStatusAsync(string supplierOrderNumber)
        {
            RequireHttpClient();
            var json = await _httpClient.GetAsync($"orders/{Uri.EscapeDataString(supplierOrderNumber)}/updatedStatus").ConfigureAwait(false);
            var dto = Deserialize<ArrowOrderStatusResponse>(json);
            if (dto == null || string.IsNullOrEmpty(dto.OrderId))
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"Arrow found no order for '{supplierOrderNumber}'.");
            }

            return new Core.Models.SupplierOrderStatus
            {
                SupplierOrderNumber = dto.OrderId,
                Status = MapOrderStatus(dto.OrderStatus),
                Lines = new List<SupplierOrderStatusLine>()
            };
        }

        /// <summary>
        /// MyArrow's spec names four order statuses in prose ("1. Pending Review, 2. Accepted,
        /// 3. Rejected ..., 4. Not Confirmed ...") without ever showing the literal JSON string —
        /// mapped by keyword, same defensive approach MouserAdapter.MapOrderStatus uses for its own
        /// undocumented status text; re-check once a real response has actually been seen.
        /// </summary>
        private static PurchaseOrderStatus MapOrderStatus(string orderStatus)
        {
            if (string.IsNullOrWhiteSpace(orderStatus)) return PurchaseOrderStatus.Submitted;
            var text = orderStatus.ToLowerInvariant();
            if (text.Contains("reject")) return PurchaseOrderStatus.Rejected;
            if (text.Contains("accept")) return PurchaseOrderStatus.Acknowledged;
            if (text.Contains("not confirmed")) return PurchaseOrderStatus.Submitted;
            if (text.Contains("pending")) return PurchaseOrderStatus.Submitted;
            return PurchaseOrderStatus.Submitted;
        }

        public Task<bool> CancelOrderAsync(string supplierOrderNumber)
        {
            if (!_options.UseMockData)
            {
                // No cancel endpoint is documented anywhere in MyArrow's spec at all (not even as
                // an unconfirmed placeholder) — unlike the other real-mode gaps above, this isn't
                // "unconfirmed shape", it's "doesn't exist", so NotSupported-style, not the same
                // RealModeNotConfirmedException wording.
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "MyArrow's API documents no order-cancellation endpoint — cancel this order manually via the MyArrow portal or your Arrow sales rep.");
            }

            var key = OrdersByIdempotencyKey.FirstOrDefault(kv => kv.Value.Result.SupplierOrderNumber == supplierOrderNumber).Key;
            if (key == null) return Task.FromResult(false);
            return Task.FromResult(OrdersByIdempotencyKey.TryRemove(key, out _));
        }

        private static SupplierException RealModeNotConfirmedException(string endpoint) =>
            new SupplierException(Code, SupplierErrorCode.UnknownError,
                $"Arrow real-mode via {endpoint} is niet geïmplementeerd: de door de gebruiker aangeleverde MyArrow-documentatie toont voor dit " +
                "onderdeel uitsluitend lege \"{}\"/\"[]\"-placeholders, nooit een echt voorbeeld — de exacte veldnamen zijn dus niet bevestigd. " +
                "Zet UseMockData terug op true, of vraag de Arrow sales rep om een echt voorbeeldresponse (of test het rechtstreeks tegen " +
                "een sandbox/account) voordat dit stuk wordt afgemaakt.");

        private void RequireHttpClient()
        {
            if (_httpClient == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "ArrowAdapter has UseMockData=false but no ISupplierHttpClient was supplied.");
            }
        }

        private static T Deserialize<T>(string json) where T : class
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "Could not parse MyArrow API response.", ex);
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
