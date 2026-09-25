using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Interfaces;
using Procurement.Core.Mocking;
using Procurement.Core.Models;
using Procurement.Suppliers.DigiKey.Http;

namespace Procurement.Suppliers.DigiKey
{
    public class DigiKeyAdapter : ISupplierAdapter
    {
        public const string Code = "DIGIKEY";

        private readonly DigiKeyOptions _options;
        private readonly ISupplierHttpClient _httpClient;

        // Keyed by idempotency key so a retried CreateOrderAsync call never places a second mock
        // order. Lines kept alongside the result so GetOrderStatusAsync can echo a per-line mock
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
            // Ordering: real-mode implemented against DigiKey's confirmed Ordering v3 spec
            // (CreateRealOrderAsync below), but never yet run against a live account — the
            // sandbox/production account also needs its own separate approval for this API, on top
            // of a one-time "Ordering autoriseren..." consent flow (3-legged OAuth) in Instellingen.
            // OrderStatus stays mock-only for now: no confirmed contract for that API yet (unlike
            // Ordering, where the user supplied the real Swagger spec directly).
            Ordering = CapabilityStatus.Supported,
            OrderStatus = CapabilityStatus.Supported,
            Shipment = CapabilityStatus.ManualProcess,
            Tracking = CapabilityStatus.ManualProcess,
            Invoice = CapabilityStatus.ManualProcess
        };

        public DigiKeyAdapter(DigiKeyOptions options, ISupplierHttpClient httpClient = null)
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
                    "ManufacturerPartNumber is required to search DigiKey.");
            }

            if (_options.UseMockData)
            {
                var product = DigiKeyMockDataProvider.BuildProduct(request.Manufacturer, request.ManufacturerPartNumber);
                return new List<SupplierProduct> { product };
            }

            RequireHttpClient();
            var body = JsonConvert.SerializeObject(new
            {
                Keywords = request.ManufacturerPartNumber,
                Limit = 10,
                Offset = 0
            });
            var responseJson = await _httpClient.PostAsync("products/v4/search/keyword", body).ConfigureAwait(false);
            var response = Deserialize<DigiKeyKeywordSearchResponse>(responseJson);

            if (response?.Products == null || response.Products.Count == 0)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"DigiKey found no products for '{request.ManufacturerPartNumber}'.");
            }

            return response.Products.Select(p => MapProduct(p, request)).ToList();
        }

        public async Task<SupplierProduct> GetProductAsync(string supplierPartNumber)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
            {
                var isExample = supplierPartNumber == "DK-CRCW060310K0FKEA";
                return new SupplierProduct
                {
                    SupplierPartNumber = supplierPartNumber,
                    Manufacturer = isExample ? DigiKeyMockDataProvider.ExampleManufacturer : "(onbekend)",
                    ManufacturerPartNumber = isExample ? DigiKeyMockDataProvider.ExampleMpn : supplierPartNumber,
                    Description = isExample
                        ? $"{DigiKeyMockDataProvider.ExampleManufacturer} {DigiKeyMockDataProvider.ExampleMpn}"
                        : supplierPartNumber,
                    MatchConfidence = isExample ? MatchConfidence.Exact : MatchConfidence.Medium,
                    DatasheetUrl = $"https://www.digikey.com/datasheets/{supplierPartNumber}.pdf"
                };
            }

            var dto = await FetchProductDetailsAsync(supplierPartNumber).ConfigureAwait(false);
            return MapProduct(dto, null);
        }

        public async Task<SupplierAvailability> GetAvailabilityAsync(string supplierPartNumber)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return DigiKeyMockDataProvider.BuildAvailability(supplierPartNumber);

            var dto = await FetchProductDetailsAsync(supplierPartNumber).ConfigureAwait(false);
            return MapAvailability(dto);
        }

        public async Task<SupplierPricing> GetPricingAsync(string supplierPartNumber, int quantity)
        {
            if (quantity <= 0)
                throw new SupplierException(Code, SupplierErrorCode.InvalidQuantity, "Quantity must be positive.");
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return DigiKeyMockDataProvider.BuildPricing(supplierPartNumber, quantity);

            var dto = await FetchProductDetailsAsync(supplierPartNumber).ConfigureAwait(false);
            return MapPricing(dto, quantity);
        }

        public async Task<IReadOnlyList<SupplierPackagingOption>> GetPackagingOptionsAsync(string supplierPartNumber, int quantity)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return DigiKeyMockDataProvider.BuildPackagingOptions(supplierPartNumber, quantity);

            var dto = await FetchProductDetailsAsync(supplierPartNumber).ConfigureAwait(false);
            return MapPackagingOptions(dto);
        }

        // DigiKey's V4 API has no separate pricing/availability/packaging endpoints — a single
        // "productdetails" call returns all of it, so every real-mode read method above funnels
        // through here.
        private async Task<DigiKeyProductDto> FetchProductDetailsAsync(string supplierPartNumber)
        {
            RequireHttpClient();
            var responseJson = await _httpClient
                .GetAsync($"products/v4/search/{Uri.EscapeDataString(supplierPartNumber)}/productdetails")
                .ConfigureAwait(false);
            var response = Deserialize<DigiKeyProductDetailsResponse>(responseJson);

            if (response?.Product == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"DigiKey found no product details for '{supplierPartNumber}'.");
            }

            return response.Product;
        }

        private static SupplierProduct MapProduct(DigiKeyProductDto dto, ProductSearchRequest request)
        {
            var confidence = request != null &&
                              string.Equals(dto.ManufacturerPartNumber, request.ManufacturerPartNumber, StringComparison.OrdinalIgnoreCase)
                ? MatchConfidence.Exact
                : MatchConfidence.Medium;

            return new SupplierProduct
            {
                SupplierPartNumber = dto.DigiKeyPartNumber,
                Manufacturer = dto.Manufacturer?.Value,
                ManufacturerPartNumber = dto.ManufacturerPartNumber,
                Description = !string.IsNullOrWhiteSpace(dto.DetailedDescription) ? dto.DetailedDescription : dto.ProductDescription,
                MatchConfidence = confidence,
                DatasheetUrl = dto.DatasheetUrl
            };
        }

        private static SupplierAvailability MapAvailability(DigiKeyProductDto dto)
        {
            // DigiKey's productdetails response has no explicit lead-time-in-days field for
            // in-stock parts; ManufacturerLeadWeeks only applies to backorder situations.
            // Approximated here as 0 when in stock, else parsed from ManufacturerLeadWeeks
            // (weeks * 7) when available, falling back to a conservative default otherwise.
            var inStock = dto.QuantityAvailable > 0;
            int leadTimeDays;
            if (inStock)
            {
                leadTimeDays = 0;
            }
            else if (int.TryParse(dto.ManufacturerLeadWeeks, out var weeks) && weeks > 0)
            {
                leadTimeDays = weeks * 7;
            }
            else
            {
                leadTimeDays = 14;
            }

            return new SupplierAvailability
            {
                SupplierPartNumber = dto.DigiKeyPartNumber,
                AvailableQuantity = dto.QuantityAvailable,
                LeadTimeDays = leadTimeDays,
                EstimatedDeliveryDate = DateTime.UtcNow.AddDays(leadTimeDays),
                OnBackorder = !inStock
            };
        }

        private static SupplierPricing MapPricing(DigiKeyProductDto dto, int quantity)
        {
            var priceBreaks = (dto.StandardPricing ?? new List<DigiKeyPriceBreakDto>())
                .Select(pb => new PriceBreak { BreakQuantity = pb.BreakQuantity, UnitPrice = pb.UnitPrice })
                .OrderBy(pb => pb.BreakQuantity)
                .ToList();

            var applicable = priceBreaks.LastOrDefault(pb => pb.BreakQuantity <= quantity) ?? priceBreaks.FirstOrDefault();

            return new SupplierPricing
            {
                SupplierPartNumber = dto.DigiKeyPartNumber,
                RequestedQuantity = quantity,
                UnitPrice = applicable?.UnitPrice ?? dto.UnitPrice,
                Currency = "EUR",
                MinimumOrderQuantity = priceBreaks.FirstOrDefault()?.BreakQuantity ?? 1,
                OrderMultiple = 1,
                PriceBreaks = priceBreaks
            };
        }

        private static IReadOnlyList<SupplierPackagingOption> MapPackagingOptions(DigiKeyProductDto dto)
        {
            var variations = dto.ProductVariations;
            if (variations == null || variations.Count == 0)
                return new List<SupplierPackagingOption>();

            return variations.Select(v => new SupplierPackagingOption
            {
                SupplierPartNumber = dto.DigiKeyPartNumber,
                PackagingType = MapPackagingType(v.PackageType?.Value),
                PackagingQuantity = v.StandardPackage > 0 ? v.StandardPackage : v.MinimumOrderQuantity,
                ReelType = MapReelType(v.PackageType?.Value),
                ReelingFee = 0m,
                AvailableQuantity = v.QuantityAvailableForPackageType
            }).ToList();
        }

        // DigiKey's PackageType.Value is free text (e.g. "Cut Tape (CT)", "Tape & Reel (TR)",
        // "Digi-Reel®") — mapped on best-effort substring matching, unconfirmed against the
        // full set of real values DigiKey returns.
        private static PackagingType MapPackagingType(string packageTypeValue)
        {
            if (string.IsNullOrEmpty(packageTypeValue)) return PackagingType.Any;
            if (packageTypeValue.IndexOf("Cut Tape", StringComparison.OrdinalIgnoreCase) >= 0) return PackagingType.CutTape;
            if (packageTypeValue.IndexOf("Tray", StringComparison.OrdinalIgnoreCase) >= 0) return PackagingType.Tray;
            if (packageTypeValue.IndexOf("Tube", StringComparison.OrdinalIgnoreCase) >= 0) return PackagingType.Tube;
            if (packageTypeValue.IndexOf("Digi-Reel", StringComparison.OrdinalIgnoreCase) >= 0) return PackagingType.ReReel;
            if (packageTypeValue.IndexOf("Reel", StringComparison.OrdinalIgnoreCase) >= 0) return PackagingType.OriginalReel;
            return PackagingType.Any;
        }

        private static ReelType? MapReelType(string packageTypeValue)
        {
            if (string.IsNullOrEmpty(packageTypeValue)) return null;
            if (packageTypeValue.IndexOf("Digi-Reel", StringComparison.OrdinalIgnoreCase) >= 0) return Core.Enums.ReelType.SupplierReReel;
            if (packageTypeValue.IndexOf("Reel", StringComparison.OrdinalIgnoreCase) >= 0) return Core.Enums.ReelType.ManufacturerOriginal;
            return null;
        }

        public async Task<SupplierOrderResult> CreateOrderAsync(SupplierOrderRequest request, string idempotencyKey)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentNullException(nameof(idempotencyKey));

            // Checked before branching into mock/real: DigiKey's own Ordering spec explicitly warns
            // that resubmitting an order charges and fulfills it a second time ("If we receive your
            // order but are not able to generate a SalesOrderID... If you do resubmit it, both
            // orders will be charged and fulfilled") — there is no idempotency-key deduplication on
            // their end the way there might be elsewhere, so this cache is the only thing standing
            // between a retried call (e.g. after a timeout) and a real duplicate order.
            if (OrdersByIdempotencyKey.TryGetValue(idempotencyKey, out var existing))
                return existing.Result;

            if (request.Lines == null || request.Lines.Count == 0)
            {
                throw new SupplierException(Code, SupplierErrorCode.InvalidQuantity, "Order has no lines.");
            }

            var result = _options.UseMockData
                ? BuildMockOrderResult(request, idempotencyKey)
                : await CreateRealOrderAsync(request).ConfigureAwait(false);

            OrdersByIdempotencyKey[idempotencyKey] = (result, request.Lines);
            return result;
        }

        private SupplierOrderResult BuildMockOrderResult(SupplierOrderRequest request, string idempotencyKey)
        {
            foreach (var line in request.Lines)
            {
                var availability = DigiKeyMockDataProvider.BuildAvailability(line.SupplierPartNumber);
                if (line.Quantity > availability.AvailableQuantity)
                {
                    throw new SupplierException(Code, SupplierErrorCode.InsufficientStock,
                        $"Requested {line.Quantity} of {line.SupplierPartNumber} but only {availability.AvailableQuantity} available.");
                }
            }

            var orderTotal = request.Lines.Sum(l => l.Quantity * l.UnitPrice);
            return new SupplierOrderResult
            {
                Success = true,
                SupplierOrderNumber = $"DK{DateTime.UtcNow:yyyyMMddHHmmss}{new Random(idempotencyKey.GetHashCode()).Next(100, 999)}",
                Status = PurchaseOrderStatus.Acknowledged,
                OrderTotal = orderTotal,
                Currency = request.Currency ?? "EUR",
                SubmittedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// POST /Ordering/v3/Orders against DigiKey's confirmed Ordering v3 Swagger spec (supplied
        /// directly by the user — see DigiKeyDtos.cs) — the first real (not mock) order-submission
        /// this project has ever attempted for any supplier, so treat this as unverified against a
        /// live account until it's actually run once.
        /// </summary>
        private async Task<SupplierOrderResult> CreateRealOrderAsync(SupplierOrderRequest request)
        {
            RequireHttpClient();
            if (!(_httpClient is IDigiKeyOrderingHttpClient orderingHttpClient))
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "DigiKeyAdapter's ISupplierHttpClient does not support Ordering (expected a DigiKeyHttpClientWrapper).");
            }
            EnsureOrderingContactConfigured();

            var contact = new DigiKeyContactDto
            {
                CustomerId = _options.AccountId,
                Name = _options.ContactName,
                Telephone = _options.ContactTelephone,
                Address = new DigiKeyAddressDto
                {
                    FirstName = SplitContactFirstName(_options.ContactName),
                    LastName = SplitContactLastName(_options.ContactName),
                    Email = _options.ContactEmail,
                    AddressLineOne = _options.AddressLine1,
                    AddressLineTwo = _options.AddressLine2,
                    City = _options.City,
                    Province = _options.Province,
                    PostalCode = _options.PostalCode,
                    Country = _options.CountryCode
                }
            };

            var orderRequest = new DigiKeyOrderRequestDto
            {
                PurchaseOrderNumber = request.ErpPoNumber,
                Currency = request.Currency ?? "EUR",
                BuyerContact = contact,
                ShippingContact = contact,
                LineItems = request.Lines.Select(l => new DigiKeyLineItemDto
                {
                    DigiKeyPartNumber = l.SupplierPartNumber,
                    RequestedQuantity = l.Quantity,
                    UnitPrice = (double)l.UnitPrice
                }).ToList()
            };

            var body = JsonConvert.SerializeObject(orderRequest);
            var responseJson = await orderingHttpClient.PostOrderAsync("Ordering/v3/Orders", body).ConfigureAwait(false);
            var response = Deserialize<DigiKeyOrderResponseDto>(responseJson);

            // Three cases per the spec (DigiKeyOrderResponseDto's own comment): a positive
            // SalesOrderId is the normal case; "0" means every line was fulfilled by a DigiKey
            // Marketplace partner instead (SalesOrderIds_DKPlus carries the real number(s) then);
            // "-1" means DigiKey received the order but hadn't minted a number yet when it had to
            // respond — not an error, and explicitly must not be resubmitted (see CreateOrderAsync's
            // own idempotency-cache comment).
            string orderNumber;
            if (response.SalesOrderId > 0)
                orderNumber = response.SalesOrderId.ToString();
            else if (response.SalesOrderIdsDkPlus != null && response.SalesOrderIdsDkPlus.Count > 0)
                orderNumber = string.Join(",", response.SalesOrderIdsDkPlus);
            else
                orderNumber = response.SalesOrderId.ToString(); // "-1": accepted, number not minted yet

            var orderTotal = request.Lines.Sum(l => l.Quantity * l.UnitPrice);
            return new SupplierOrderResult
            {
                Success = true,
                SupplierOrderNumber = orderNumber,
                Status = PurchaseOrderStatus.Acknowledged,
                OrderTotal = orderTotal,
                Currency = request.Currency ?? "EUR",
                SubmittedAt = DateTime.UtcNow
            };
        }

        private void EnsureOrderingContactConfigured()
        {
            if (string.IsNullOrEmpty(_options.AccountId) || string.IsNullOrEmpty(_options.ContactName) ||
                string.IsNullOrEmpty(_options.AddressLine1) || string.IsNullOrEmpty(_options.City) ||
                string.IsNullOrEmpty(_options.PostalCode) || string.IsNullOrEmpty(_options.CountryCode))
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "DigiKey-verzendgegevens zijn niet volledig ingevuld — vul Account ID/contactnaam/adres in via " +
                    "Instellingen -> Suppliers -> \"Verzendgegevens bewerken...\" voordat je een order plaatst.");
            }
        }

        // DigiKey's Address wants FirstName/LastName separately; Supplier.ContactName (and this
        // adapter's own DigiKeyOptions.ContactName) is deliberately one flat field instead of two,
        // matching how the rest of this app names a person — split on the first space as a
        // best-effort mapping rather than adding yet another pair of Supplier columns for this.
        private static string SplitContactFirstName(string contactName)
        {
            if (string.IsNullOrWhiteSpace(contactName)) return null;
            var spaceIndex = contactName.IndexOf(' ');
            return spaceIndex < 0 ? null : contactName.Substring(0, spaceIndex);
        }

        private static string SplitContactLastName(string contactName)
        {
            if (string.IsNullOrWhiteSpace(contactName)) return null;
            var spaceIndex = contactName.IndexOf(' ');
            return spaceIndex < 0 ? contactName : contactName.Substring(spaceIndex + 1);
        }

        public Task<Core.Models.SupplierOrderStatus> GetOrderStatusAsync(string supplierOrderNumber)
        {
            if (!_options.UseMockData)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "DigiKey real-mode order status is not implemented — see CreateOrderAsync.");
            }

            var placed = OrdersByIdempotencyKey.Values.FirstOrDefault(o => o.Result.SupplierOrderNumber == supplierOrderNumber);
            if (placed.Result == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
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
            if (!_options.UseMockData)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "DigiKey real-mode order cancellation is not implemented — see CreateOrderAsync.");
            }

            var key = OrdersByIdempotencyKey.FirstOrDefault(kv => kv.Value.Result.SupplierOrderNumber == supplierOrderNumber).Key;
            if (key == null) return Task.FromResult(false);
            return Task.FromResult(OrdersByIdempotencyKey.TryRemove(key, out _));
        }

        private void RequireHttpClient()
        {
            if (_httpClient == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "DigiKeyAdapter has UseMockData=false but no ISupplierHttpClient was supplied.");
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
                    "Could not parse DigiKey API response.", ex);
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
