using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Interfaces;
using Procurement.Core.Models;
using Procurement.Suppliers.Mouser.Http;

namespace Procurement.Suppliers.Mouser
{
    public class MouserAdapter : ISupplierAdapter
    {
        public const string Code = "MOUSER";

        private readonly MouserOptions _options;
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
            // Ordering stays mock-only even when UseMockData=false — Mouser's Order API needs a
            // separate, additionally-approved API key from the Search API key used here, and its
            // exact contract is unconfirmed. See CreateOrderAsync below.
            Ordering = CapabilityStatus.Supported,
            OrderStatus = CapabilityStatus.Supported,
            Shipment = CapabilityStatus.ManualProcess,
            Tracking = CapabilityStatus.ManualProcess,
            Invoice = CapabilityStatus.ManualProcess
        };

        public MouserAdapter(MouserOptions options, ISupplierHttpClient httpClient = null)
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
                    "ManufacturerPartNumber is required to search Mouser.");
            }

            if (_options.UseMockData)
            {
                var product = MouserMockDataProvider.BuildProduct(request.Manufacturer, request.ManufacturerPartNumber);
                return new List<SupplierProduct> { product };
            }

            // Mouser has no dedicated "search by manufacturer part number" endpoint — keyword
            // search is the documented way, matched against ManufacturerPartNumber in the results.
            var body = JsonConvert.SerializeObject(new
            {
                SearchByKeywordRequest = new
                {
                    keyword = request.ManufacturerPartNumber,
                    records = 10,
                    startingRecord = 0
                }
            });
            var parts = await SearchAsync("api/v1/search/keyword", body).ConfigureAwait(false);

            if (parts.Count == 0)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"Mouser found no products for '{request.ManufacturerPartNumber}'.");
            }

            return parts.Select(p => MapProduct(p, request)).ToList();
        }

        public async Task<SupplierProduct> GetProductAsync(string supplierPartNumber)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
            {
                var isExample = supplierPartNumber == "MO-CRCW060310K0FKEA";
                return new SupplierProduct
                {
                    SupplierPartNumber = supplierPartNumber,
                    Manufacturer = isExample ? MouserMockDataProvider.ExampleManufacturer : "(onbekend)",
                    ManufacturerPartNumber = isExample ? MouserMockDataProvider.ExampleMpn : supplierPartNumber,
                    Description = isExample
                        ? $"{MouserMockDataProvider.ExampleManufacturer} {MouserMockDataProvider.ExampleMpn}"
                        : supplierPartNumber,
                    MatchConfidence = isExample ? MatchConfidence.Exact : MatchConfidence.Medium,
                    DatasheetUrl = $"https://www.mouser.com/datasheets/{supplierPartNumber}.pdf"
                };
            }

            var dto = await FetchPartAsync(supplierPartNumber).ConfigureAwait(false);
            return MapProduct(dto, null);
        }

        public async Task<SupplierAvailability> GetAvailabilityAsync(string supplierPartNumber)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return MouserMockDataProvider.BuildAvailability(supplierPartNumber);

            var dto = await FetchPartAsync(supplierPartNumber).ConfigureAwait(false);
            return MapAvailability(dto);
        }

        public async Task<SupplierPricing> GetPricingAsync(string supplierPartNumber, int quantity)
        {
            if (quantity <= 0)
                throw new SupplierException(Code, SupplierErrorCode.InvalidQuantity, "Quantity must be positive.");
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return MouserMockDataProvider.BuildPricing(supplierPartNumber, quantity);

            var dto = await FetchPartAsync(supplierPartNumber).ConfigureAwait(false);
            return MapPricing(dto, quantity);
        }

        public async Task<IReadOnlyList<SupplierPackagingOption>> GetPackagingOptionsAsync(string supplierPartNumber, int quantity)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return MouserMockDataProvider.BuildPackagingOptions(supplierPartNumber, quantity);

            var dto = await FetchPartAsync(supplierPartNumber).ConfigureAwait(false);
            return MapPackagingOptions(dto, quantity);
        }

        // Mouser's response for a single part already carries pricing + availability in one
        // object (no separate endpoints), so every real-mode read method above funnels through
        // this single-part lookup by Mouser's own part number.
        private async Task<MouserPartDto> FetchPartAsync(string supplierPartNumber)
        {
            var body = JsonConvert.SerializeObject(new
            {
                SearchByPartRequest = new
                {
                    mouserPartNumber = supplierPartNumber,
                    partSearchOptions = ""
                }
            });
            var parts = await SearchAsync("api/v1/search/partnumber", body).ConfigureAwait(false);
            var dto = parts.FirstOrDefault();
            if (dto == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"Mouser found no part for '{supplierPartNumber}'.");
            }
            return dto;
        }

        private async Task<List<MouserPartDto>> SearchAsync(string relativeUrl, string jsonBody)
        {
            RequireHttpClient();
            var responseJson = await _httpClient.PostAsync(relativeUrl, jsonBody).ConfigureAwait(false);
            var response = Deserialize<MouserSearchResponse>(responseJson);

            if (response?.Errors != null && response.Errors.Count > 0)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    $"Mouser API returned an error: {string.Join("; ", response.Errors.Select(e => e.Message))}");
            }

            return response?.SearchResults?.Parts ?? new List<MouserPartDto>();
        }

        private static SupplierProduct MapProduct(MouserPartDto dto, ProductSearchRequest request)
        {
            var confidence = request != null &&
                              string.Equals(dto.ManufacturerPartNumber, request.ManufacturerPartNumber, StringComparison.OrdinalIgnoreCase)
                ? MatchConfidence.Exact
                : MatchConfidence.Medium;

            return new SupplierProduct
            {
                SupplierPartNumber = dto.MouserPartNumber,
                Manufacturer = dto.Manufacturer,
                ManufacturerPartNumber = dto.ManufacturerPartNumber,
                Description = dto.Description,
                MatchConfidence = confidence,
                DatasheetUrl = dto.DataSheetUrl
            };
        }

        private static SupplierAvailability MapAvailability(MouserPartDto dto)
        {
            var available = ParseLeadingInt(dto.Availability);
            var leadTimeDays = ParseLeadingInt(dto.LeadTime);
            if (leadTimeDays == 0 && available <= 0) leadTimeDays = 14;

            return new SupplierAvailability
            {
                SupplierPartNumber = dto.MouserPartNumber,
                AvailableQuantity = available,
                LeadTimeDays = leadTimeDays,
                EstimatedDeliveryDate = DateTime.UtcNow.AddDays(leadTimeDays),
                OnBackorder = available <= 0
            };
        }

        private static SupplierPricing MapPricing(MouserPartDto dto, int quantity)
        {
            var priceBreaks = (dto.PriceBreaks ?? new List<MouserPriceBreakDto>())
                .Select(pb => new PriceBreak { BreakQuantity = pb.Quantity, UnitPrice = ParseCurrency(pb.Price) })
                .OrderBy(pb => pb.BreakQuantity)
                .ToList();

            var applicable = priceBreaks.LastOrDefault(pb => pb.BreakQuantity <= quantity) ?? priceBreaks.FirstOrDefault();

            int.TryParse(dto.Min, out var min);
            int.TryParse(dto.Mult, out var mult);

            return new SupplierPricing
            {
                SupplierPartNumber = dto.MouserPartNumber,
                RequestedQuantity = quantity,
                UnitPrice = applicable?.UnitPrice ?? 0m,
                Currency = dto.PriceBreaks?.FirstOrDefault()?.Currency ?? "EUR",
                MinimumOrderQuantity = min > 0 ? min : 1,
                OrderMultiple = mult > 0 ? mult : 1,
                PriceBreaks = priceBreaks
            };
        }

        // Mouser's part response doesn't expose distinct reel/cut-tape packaging variants the way
        // DigiKey does — approximated as one OriginalReel option sized to the order multiple, same
        // trade-off as FarnellAdapter.MapPackagingOptions.
        private static IReadOnlyList<SupplierPackagingOption> MapPackagingOptions(MouserPartDto dto, int quantity)
        {
            int.TryParse(dto.Mult, out var mult);
            return new List<SupplierPackagingOption>
            {
                new SupplierPackagingOption
                {
                    SupplierPartNumber = dto.MouserPartNumber,
                    PackagingType = PackagingType.OriginalReel,
                    PackagingQuantity = mult > 0 ? mult : quantity,
                    ReelType = Core.Enums.ReelType.ManufacturerOriginal,
                    ReelingFee = 0m,
                    AvailableQuantity = ParseLeadingInt(dto.Availability)
                }
            };
        }

        // Internal (not private) so MouserAdapterTests can pin down the defensive-parsing
        // behavior directly — mock mode never exercises these, since it never sees Mouser's real
        // free-text Availability/LeadTime/Price fields.
        internal static int ParseLeadingInt(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            var match = Regex.Match(text, @"\d+");
            return match.Success && int.TryParse(match.Value, out var value) ? value : 0;
        }

        internal static decimal ParseCurrency(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0m;
            var cleaned = Regex.Replace(text, @"[^\d.,]", "");
            // Normalize a "0,11" (comma decimal) to "0.11" only when there's no dot already —
            // avoids mangling thousands-separated values like "1,234.56".
            if (cleaned.Contains(",") && !cleaned.Contains("."))
                cleaned = cleaned.Replace(",", ".");
            else
                cleaned = cleaned.Replace(",", "");

            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;
        }

        public Task<SupplierOrderResult> CreateOrderAsync(SupplierOrderRequest request, string idempotencyKey)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentNullException(nameof(idempotencyKey));

            if (!_options.UseMockData)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "Mouser real-mode ordering is not implemented — it needs a separate Order API key and the contract is unconfirmed. " +
                    "Set UseMockData=true for Mouser, or place this order manually.");
            }

            if (OrdersByIdempotencyKey.TryGetValue(idempotencyKey, out var existing))
                return Task.FromResult(existing);

            if (request.Lines == null || request.Lines.Count == 0)
            {
                throw new SupplierException(Code, SupplierErrorCode.InvalidQuantity, "Order has no lines.");
            }

            foreach (var line in request.Lines)
            {
                var availability = MouserMockDataProvider.BuildAvailability(line.SupplierPartNumber);
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
                SupplierOrderNumber = $"MO{DateTime.UtcNow:yyyyMMddHHmmss}{new Random(idempotencyKey.GetHashCode()).Next(100, 999)}",
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
                    "Mouser real-mode order status is not implemented — see CreateOrderAsync.");
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
                    "Mouser real-mode order cancellation is not implemented — see CreateOrderAsync.");
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
                    "MouserAdapter has UseMockData=false but no ISupplierHttpClient was supplied.");
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
                    "Could not parse Mouser API response.", ex);
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
