using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Interfaces;
using Procurement.Core.Models;
using Procurement.Suppliers.Rutronik.Http;
using System.Threading.Tasks;

namespace Procurement.Suppliers.Rutronik
{
    /// <summary>
    /// Real Rutronik24 Shop API adapter — confirmed (22 okt 2026, two documents pasted by the
    /// user: a plain-text overview and the official PDF manual v1.3) to be a pure read-only
    /// webshop search API: Search, Pricerequest and Article, nothing else. No order placement, no
    /// order status, no EDI mentioned anywhere — matching the user's own read of it ("het lijkt er
    /// op dat we daar ook alleen kunnen zoeken"). Ordering/OrderStatus/Shipment/Tracking/Invoice
    /// are therefore ManualProcess, same semantic TmeAdapter uses: ProcurementEngine skips these
    /// steps and falls back to "place/track manually" instead of calling a nonexistent endpoint.
    /// </summary>
    public class RutronikAdapter : ISupplierAdapter
    {
        public const string Code = "RUTRONIK";

        private readonly RutronikOptions _options;
        private readonly ISupplierHttpClient _httpClient;

        public string SupplierCode => Code;

        public SupplierCapabilities Capabilities { get; } = new SupplierCapabilities
        {
            ProductSearch = CapabilityStatus.Supported,
            Pricing = CapabilityStatus.Supported,
            Availability = CapabilityStatus.Supported,
            Packaging = CapabilityStatus.Supported,
            Ordering = CapabilityStatus.ManualProcess,
            OrderStatus = CapabilityStatus.ManualProcess,
            Shipment = CapabilityStatus.ManualProcess,
            Tracking = CapabilityStatus.ManualProcess,
            Invoice = CapabilityStatus.ManualProcess
        };

        public RutronikAdapter(RutronikOptions options, ISupplierHttpClient httpClient = null)
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
                    "ManufacturerPartNumber is required to search Rutronik.");
            }

            if (_options.UseMockData)
            {
                var product = RutronikMockDataProvider.BuildProduct(request.Manufacturer, request.ManufacturerPartNumber);
                return new List<SupplierProduct> { product };
            }

            // /api/article/?mpn=... is "designed for search engines" and matches an exact MPN —
            // a better fit for "search by manufacturer part number" than the fuzzy keyword Search
            // endpoint, so it's tried first; falls back to Search only if Article finds nothing
            // (e.g. the mpn doesn't match exactly, which Article requires per its own doc).
            RequireHttpClient();
            var articleJson = await _httpClient
                .GetAsync($"api/article/?mpn={Uri.EscapeDataString(request.ManufacturerPartNumber)}")
                .ConfigureAwait(false);
            var article = DeserializeOrNull<RutronikArticleResultDto>(articleJson);
            if (article != null && !string.IsNullOrEmpty(article.Sku))
            {
                return new List<SupplierProduct> { MapArticleToProduct(article, request) };
            }

            var searchJson = await _httpClient
                .GetAsync($"api/search/?searchterm={Uri.EscapeDataString(request.ManufacturerPartNumber)}")
                .ConfigureAwait(false);
            var results = Deserialize<List<RutronikSearchResultDto>>(searchJson) ?? new List<RutronikSearchResultDto>();
            if (results.Count == 0)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"Rutronik found no products for '{request.ManufacturerPartNumber}'.");
            }

            return results.Select(r => MapSearchResultToProduct(r, request)).ToList();
        }

        public async Task<SupplierProduct> GetProductAsync(string supplierPartNumber)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
            {
                var isExample = supplierPartNumber == "ICMCU22246";
                return new SupplierProduct
                {
                    SupplierPartNumber = supplierPartNumber,
                    Manufacturer = isExample ? RutronikMockDataProvider.ExampleManufacturer : "(onbekend)",
                    ManufacturerPartNumber = isExample ? RutronikMockDataProvider.ExampleMpn : supplierPartNumber,
                    Description = isExample
                        ? $"{RutronikMockDataProvider.ExampleManufacturer} {RutronikMockDataProvider.ExampleMpn}"
                        : supplierPartNumber,
                    MatchConfidence = isExample ? MatchConfidence.Exact : MatchConfidence.Medium,
                    DatasheetUrl = $"https://www.rutronik24.com/produkt/{supplierPartNumber}.html"
                };
            }

            var dto = await FetchBySkuAsync(supplierPartNumber).ConfigureAwait(false);
            return MapPriceRequestToProduct(dto);
        }

        public async Task<SupplierAvailability> GetAvailabilityAsync(string supplierPartNumber)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return RutronikMockDataProvider.BuildAvailability(supplierPartNumber);

            var dto = await FetchBySkuAsync(supplierPartNumber).ConfigureAwait(false);
            return MapAvailability(dto);
        }

        public async Task<SupplierPricing> GetPricingAsync(string supplierPartNumber, int quantity)
        {
            if (quantity <= 0)
                throw new SupplierException(Code, SupplierErrorCode.InvalidQuantity, "Quantity must be positive.");
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return RutronikMockDataProvider.BuildPricing(supplierPartNumber, quantity);

            var dto = await FetchBySkuAsync(supplierPartNumber).ConfigureAwait(false);
            return MapPricing(dto, quantity);
        }

        public async Task<IReadOnlyList<SupplierPackagingOption>> GetPackagingOptionsAsync(string supplierPartNumber, int quantity)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return RutronikMockDataProvider.BuildPackagingOptions(supplierPartNumber, quantity);

            var dto = await FetchBySkuAsync(supplierPartNumber).ConfigureAwait(false);
            return MapPackagingOptions(dto, quantity);
        }

        // Pricerequest by Rutronik's own sku is "intended to return customer specific prices and
        // delivery times" per the doc — the right call for every per-part lookup once a sku is
        // already known from SearchProductsAsync.
        private async Task<RutronikPriceRequestResultDto> FetchBySkuAsync(string sku)
        {
            RequireHttpClient();
            var json = await _httpClient.GetAsync($"api/pricerequest/?sku={Uri.EscapeDataString(sku)}").ConfigureAwait(false);
            var dto = DeserializeOrNull<RutronikPriceRequestResultDto>(json);
            if (dto == null || string.IsNullOrEmpty(dto.Sku))
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"Rutronik found no part for sku '{sku}'.");
            }
            return dto;
        }

        private static SupplierProduct MapSearchResultToProduct(RutronikSearchResultDto dto, ProductSearchRequest request)
        {
            var confidence = request != null &&
                              string.Equals(dto.Mpn, request.ManufacturerPartNumber, StringComparison.OrdinalIgnoreCase)
                ? MatchConfidence.Exact
                : MatchConfidence.Medium;

            return new SupplierProduct
            {
                SupplierPartNumber = dto.Sku,
                Manufacturer = dto.Manufacturer,
                ManufacturerPartNumber = dto.Mpn,
                Description = dto.Description,
                MatchConfidence = confidence,
                DatasheetUrl = dto.Url
            };
        }

        private static SupplierProduct MapArticleToProduct(RutronikArticleResultDto dto, ProductSearchRequest request)
        {
            return new SupplierProduct
            {
                SupplierPartNumber = dto.Sku,
                Manufacturer = dto.Manufacturer,
                ManufacturerPartNumber = dto.Mpn,
                Description = dto.Description,
                // Article matches mpn exactly by design ("exactly one article returned if the mpn
                // matches exactly"), so a hit here is always an exact match, unlike Search's fuzzy
                // keyword lookup.
                MatchConfidence = MatchConfidence.Exact,
                DatasheetUrl = dto.Url
            };
        }

        private static SupplierProduct MapPriceRequestToProduct(RutronikPriceRequestResultDto dto)
        {
            return new SupplierProduct
            {
                SupplierPartNumber = dto.Sku,
                Manufacturer = dto.Manufacturer,
                ManufacturerPartNumber = dto.Mpn,
                Description = dto.Description,
                MatchConfidence = MatchConfidence.Exact,
                DatasheetUrl = dto.Url
            };
        }

        private static SupplierAvailability MapAvailability(RutronikPriceRequestResultDto dto)
        {
            // "leadtime" is documented as "standard delivery time in weeks" — converted to days
            // for SupplierAvailability.LeadTimeDays, same unit every other adapter in this app uses.
            var leadTimeDays = dto.LeadTime * 7;
            return new SupplierAvailability
            {
                SupplierPartNumber = dto.Sku,
                AvailableQuantity = dto.Stock,
                LeadTimeDays = leadTimeDays,
                EstimatedDeliveryDate = DateTime.UtcNow.AddDays(leadTimeDays),
                OnBackorder = dto.Stock <= 0
            };
        }

        private static SupplierPricing MapPricing(RutronikPriceRequestResultDto dto, int quantity)
        {
            var priceBreaks = (dto.PriceBreaks ?? new List<RutronikPriceBreakDto>())
                .Select(pb => new PriceBreak { BreakQuantity = pb.Quantity, UnitPrice = pb.Price })
                .OrderBy(pb => pb.BreakQuantity)
                .ToList();

            if (priceBreaks.Count == 0)
                priceBreaks.Add(new PriceBreak { BreakQuantity = dto.Moq > 0 ? dto.Moq : 1, UnitPrice = dto.Price });

            var applicable = priceBreaks.LastOrDefault(pb => pb.BreakQuantity <= quantity) ?? priceBreaks.FirstOrDefault();

            return new SupplierPricing
            {
                SupplierPartNumber = dto.Sku,
                RequestedQuantity = quantity,
                UnitPrice = applicable?.UnitPrice ?? dto.Price,
                Currency = dto.Currency ?? "EUR",
                MinimumOrderQuantity = dto.Moq > 0 ? dto.Moq : 1,
                OrderMultiple = dto.Pu > 0 ? dto.Pu : 1,
                PriceBreaks = priceBreaks
            };
        }

        // Rutronik's "packaging" is a free-text enum (TRAY / BULK / REEL / TUBE per the doc) —
        // mapped to this app's PackagingType as closely as the four observed values allow; "BULK"
        // has no direct equivalent in PackagingType, so it maps to Any (no specific packaging)
        // rather than a guessed match.
        private static IReadOnlyList<SupplierPackagingOption> MapPackagingOptions(RutronikPriceRequestResultDto dto, int quantity)
        {
            var packagingType = MapPackagingType(dto.Packaging);
            return new List<SupplierPackagingOption>
            {
                new SupplierPackagingOption
                {
                    SupplierPartNumber = dto.Sku,
                    PackagingType = packagingType,
                    PackagingQuantity = dto.Pu > 0 ? dto.Pu : quantity,
                    ReelType = packagingType == PackagingType.OriginalReel ? Core.Enums.ReelType.ManufacturerOriginal : (Core.Enums.ReelType?)null,
                    ReelingFee = 0m,
                    AvailableQuantity = dto.Stock
                }
            };
        }

        private static PackagingType MapPackagingType(string packaging)
        {
            if (string.IsNullOrWhiteSpace(packaging)) return PackagingType.Any;
            switch (packaging.Trim().ToUpperInvariant())
            {
                case "REEL": return PackagingType.OriginalReel;
                case "TRAY": return PackagingType.Tray;
                case "TUBE": return PackagingType.Tube;
                default: return PackagingType.Any; // e.g. "BULK" — no matching PackagingType value
            }
        }

        public Task<SupplierOrderResult> CreateOrderAsync(SupplierOrderRequest request, string idempotencyKey)
        {
            throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                "Rutronik heeft geen order-API — de Rutronik24 Shop API is uitsluitend search/prijzen/voorraad (bevestigd, 22 okt 2026). " +
                "Plaats deze order handmatig bij Rutronik.");
        }

        public Task<Core.Models.SupplierOrderStatus> GetOrderStatusAsync(string supplierOrderNumber)
        {
            throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                "Rutronik heeft geen orderstatus-API — volg deze order handmatig op bij Rutronik.");
        }

        public Task<bool> CancelOrderAsync(string supplierOrderNumber)
        {
            throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                "Rutronik heeft geen order-API om te annuleren — neem handmatig contact op met Rutronik.");
        }

        private void RequireHttpClient()
        {
            if (_httpClient == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "RutronikAdapter has UseMockData=false but no ISupplierHttpClient was supplied.");
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
                    "Could not parse Rutronik API response.", ex);
            }
        }

        private static T DeserializeOrNull<T>(string json) where T : class
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (JsonException)
            {
                return null;
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
