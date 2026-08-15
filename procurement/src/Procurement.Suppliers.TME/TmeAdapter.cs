using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Interfaces;
using Procurement.Core.Models;
using Procurement.Suppliers.TME.Http;

namespace Procurement.Suppliers.TME
{
    public class TmeAdapter : ISupplierAdapter
    {
        public const string Code = "TME";

        private readonly TmeOptions _options;
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
            // Ordering stays mock-only even when UseMockData=false — see CreateOrderAsync below.
            Ordering = CapabilityStatus.Supported,
            OrderStatus = CapabilityStatus.Supported,
            Shipment = CapabilityStatus.ManualProcess,
            Tracking = CapabilityStatus.ManualProcess,
            Invoice = CapabilityStatus.ManualProcess
        };

        public TmeAdapter(TmeOptions options, ISupplierHttpClient httpClient = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClient;
        }

        public async Task<IReadOnlyList<SupplierProduct>> SearchProductsAsync(ProductSearchRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.ManufacturerPartNumber))
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    "ManufacturerPartNumber is required to search TME.");
            }

            if (_options.UseMockData)
            {
                var product = TmeMockDataProvider.BuildProduct(request.Manufacturer, request.ManufacturerPartNumber);
                return new List<SupplierProduct> { product };
            }

            var results = await SearchAsync(request.ManufacturerPartNumber).ConfigureAwait(false);
            if (results.Count == 0)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"TME found no products for '{request.ManufacturerPartNumber}'.");
            }

            return results.Select(p => MapSearchProduct(p, request)).ToList();
        }

        public async Task<SupplierProduct> GetProductAsync(string supplierPartNumber)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
            {
                var isExample = supplierPartNumber == "TME-CRCW060310K0FKEA";
                return new SupplierProduct
                {
                    SupplierPartNumber = supplierPartNumber,
                    Manufacturer = isExample ? TmeMockDataProvider.ExampleManufacturer : "(onbekend)",
                    ManufacturerPartNumber = isExample ? TmeMockDataProvider.ExampleMpn : supplierPartNumber,
                    Description = isExample
                        ? $"{TmeMockDataProvider.ExampleManufacturer} {TmeMockDataProvider.ExampleMpn}"
                        : supplierPartNumber,
                    MatchConfidence = isExample ? MatchConfidence.Exact : MatchConfidence.Medium,
                    DatasheetUrl = $"https://www.tme.eu/datasheets/{supplierPartNumber}.pdf"
                };
            }

            var dto = await FetchProductDetailAsync(supplierPartNumber).ConfigureAwait(false);
            return MapProduct(dto);
        }

        public async Task<SupplierAvailability> GetAvailabilityAsync(string supplierPartNumber)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return TmeMockDataProvider.BuildAvailability(supplierPartNumber);

            var dto = await FetchProductDetailAsync(supplierPartNumber).ConfigureAwait(false);
            return MapAvailability(dto);
        }

        public async Task<SupplierPricing> GetPricingAsync(string supplierPartNumber, int quantity)
        {
            if (quantity <= 0)
                throw new SupplierException(Code, SupplierErrorCode.InvalidQuantity, "Quantity must be positive.");
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return TmeMockDataProvider.BuildPricing(supplierPartNumber, quantity);

            var priceDto = await FetchPricesAsync(supplierPartNumber).ConfigureAwait(false);
            return MapPricing(priceDto, quantity);
        }

        public async Task<IReadOnlyList<SupplierPackagingOption>> GetPackagingOptionsAsync(string supplierPartNumber, int quantity)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return TmeMockDataProvider.BuildPackagingOptions(supplierPartNumber, quantity);

            var dto = await FetchProductDetailAsync(supplierPartNumber).ConfigureAwait(false);
            return MapPackagingOptions(dto, quantity);
        }

        // TME's documented API splits search, product details/stock and pricing across three
        // separate actions (unlike DigiKey/Mouser's single combined "product details" call) — so
        // real-mode read methods above call different endpoints depending on what they need.
        private async Task<List<TmeSearchProductDto>> SearchAsync(string keyword)
        {
            RequireHttpClient();
            var body = JsonConvert.SerializeObject(new Dictionary<string, string> { ["SearchPlain"] = keyword });
            var responseJson = await _httpClient.PostAsync("Products/Search.json", body).ConfigureAwait(false);
            var response = Deserialize<TmeEnvelope<TmeSearchData>>(responseJson);
            EnsureOk(response?.Status, response?.Error);
            return response.Data?.ProductList ?? new List<TmeSearchProductDto>();
        }

        private async Task<TmeProductDetailDto> FetchProductDetailAsync(string symbol)
        {
            RequireHttpClient();
            var body = JsonConvert.SerializeObject(new Dictionary<string, string> { ["SymbolList[0]"] = symbol });
            var responseJson = await _httpClient.PostAsync("Products/GetProducts.json", body).ConfigureAwait(false);
            var response = Deserialize<TmeEnvelope<TmeProductsData>>(responseJson);
            EnsureOk(response?.Status, response?.Error);

            var dto = response.Data?.ProductList?.FirstOrDefault();
            if (dto == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"TME found no product for '{symbol}'.");
            }
            return dto;
        }

        private async Task<TmePriceProductDto> FetchPricesAsync(string symbol)
        {
            RequireHttpClient();
            var body = JsonConvert.SerializeObject(new Dictionary<string, string> { ["SymbolList[0]"] = symbol });
            var responseJson = await _httpClient.PostAsync("Products/GetPrices.json", body).ConfigureAwait(false);
            var response = Deserialize<TmeEnvelope<TmePricesData>>(responseJson);
            EnsureOk(response?.Status, response?.Error);

            var dto = response.Data?.ProductList?.FirstOrDefault();
            if (dto == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"TME found no prices for '{symbol}'.");
            }
            return dto;
        }

        private static void EnsureOk(string status, TmeErrorDto error)
        {
            if (string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase)) return;
            throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                $"TME API returned status '{status}': {error?.Message}");
        }

        private static SupplierProduct MapSearchProduct(TmeSearchProductDto dto, ProductSearchRequest request)
        {
            var confidence = request != null &&
                              string.Equals(dto.OriginalSymbol, request.ManufacturerPartNumber, StringComparison.OrdinalIgnoreCase)
                ? MatchConfidence.Exact
                : MatchConfidence.Medium;

            return new SupplierProduct
            {
                SupplierPartNumber = dto.Symbol,
                Manufacturer = dto.Producer,
                ManufacturerPartNumber = dto.OriginalSymbol,
                Description = dto.Description,
                MatchConfidence = confidence,
                DatasheetUrl = null
            };
        }

        private static SupplierProduct MapProduct(TmeProductDetailDto dto)
        {
            return new SupplierProduct
            {
                SupplierPartNumber = dto.Symbol,
                Manufacturer = dto.Producer,
                ManufacturerPartNumber = dto.OriginalSymbol,
                Description = dto.Description,
                MatchConfidence = MatchConfidence.Medium,
                DatasheetUrl = null
            };
        }

        private static SupplierAvailability MapAvailability(TmeProductDetailDto dto)
        {
            // TME's product response has no explicit lead-time field for in-stock parts —
            // approximated the same way as the other real-mode adapters in this project.
            var leadTimeDays = dto.InStock > 0 ? 0 : 14;
            return new SupplierAvailability
            {
                SupplierPartNumber = dto.Symbol,
                AvailableQuantity = dto.InStock,
                LeadTimeDays = leadTimeDays,
                EstimatedDeliveryDate = DateTime.UtcNow.AddDays(leadTimeDays),
                OnBackorder = dto.InStock <= 0
            };
        }

        private static IReadOnlyList<SupplierPackagingOption> MapPackagingOptions(TmeProductDetailDto dto, int quantity)
        {
            return new List<SupplierPackagingOption>
            {
                new SupplierPackagingOption
                {
                    SupplierPartNumber = dto.Symbol,
                    PackagingType = PackagingType.OriginalReel,
                    PackagingQuantity = dto.Multiplier > 0 ? dto.Multiplier : quantity,
                    ReelType = Core.Enums.ReelType.ManufacturerOriginal,
                    ReelingFee = 0m,
                    AvailableQuantity = dto.InStock
                }
            };
        }

        private static SupplierPricing MapPricing(TmePriceProductDto dto, int quantity)
        {
            var priceBreaks = (dto.PriceList ?? new List<TmePriceBreakDto>())
                .Select(pb => new PriceBreak { BreakQuantity = pb.Amount, UnitPrice = pb.PriceValue })
                .OrderBy(pb => pb.BreakQuantity)
                .ToList();

            var applicable = priceBreaks.LastOrDefault(pb => pb.BreakQuantity <= quantity) ?? priceBreaks.FirstOrDefault();

            return new SupplierPricing
            {
                SupplierPartNumber = dto.Symbol,
                RequestedQuantity = quantity,
                UnitPrice = applicable?.UnitPrice ?? 0m,
                Currency = "EUR",
                MinimumOrderQuantity = priceBreaks.FirstOrDefault()?.BreakQuantity ?? 1,
                OrderMultiple = 1,
                PriceBreaks = priceBreaks
            };
        }

        public Task<SupplierOrderResult> CreateOrderAsync(SupplierOrderRequest request, string idempotencyKey)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentNullException(nameof(idempotencyKey));

            if (!_options.UseMockData)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "TME real-mode ordering is not implemented — the Ordering API contract is unconfirmed. " +
                    "Set UseMockData=true for TME, or place this order manually.");
            }

            if (OrdersByIdempotencyKey.TryGetValue(idempotencyKey, out var existing))
                return Task.FromResult(existing);

            if (request.Lines == null || request.Lines.Count == 0)
            {
                throw new SupplierException(Code, SupplierErrorCode.InvalidQuantity, "Order has no lines.");
            }

            foreach (var line in request.Lines)
            {
                var availability = TmeMockDataProvider.BuildAvailability(line.SupplierPartNumber);
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
                SupplierOrderNumber = $"TME{DateTime.UtcNow:yyyyMMddHHmmss}{new Random(idempotencyKey.GetHashCode()).Next(100, 999)}",
                Status = PurchaseOrderStatus.Acknowledged,
                OrderTotal = orderTotal,
                Currency = request.Currency ?? "EUR",
                SubmittedAt = DateTime.UtcNow
            };

            OrdersByIdempotencyKey[idempotencyKey] = result;
            return Task.FromResult(result);
        }

        public Task<Core.Models.SupplierOrderStatus> GetOrderStatusAsync(string supplierOrderNumber)
        {
            if (!_options.UseMockData)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "TME real-mode order status is not implemented — see CreateOrderAsync.");
            }

            var placed = OrdersByIdempotencyKey.Values.FirstOrDefault(o => o.SupplierOrderNumber == supplierOrderNumber);
            if (placed == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"No mock order found with number {supplierOrderNumber}.");
            }

            return Task.FromResult(new Core.Models.SupplierOrderStatus
            {
                SupplierOrderNumber = supplierOrderNumber,
                Status = PurchaseOrderStatus.Confirmed,
                ConfirmedAt = DateTime.UtcNow
            });
        }

        public Task<bool> CancelOrderAsync(string supplierOrderNumber)
        {
            if (!_options.UseMockData)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "TME real-mode order cancellation is not implemented — see CreateOrderAsync.");
            }

            var key = OrdersByIdempotencyKey.FirstOrDefault(kv => kv.Value.SupplierOrderNumber == supplierOrderNumber).Key;
            if (key == null) return Task.FromResult(false);
            return Task.FromResult(OrdersByIdempotencyKey.TryRemove(key, out _));
        }

        private void RequireHttpClient()
        {
            if (_httpClient == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "TmeAdapter has UseMockData=false but no ISupplierHttpClient was supplied.");
            }
        }

        private static T Deserialize<T>(string json) where T : class
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (JsonException ex)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "Could not parse TME API response.", ex);
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
