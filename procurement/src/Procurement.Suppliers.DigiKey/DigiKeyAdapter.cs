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
            // (CreateRealOrderAsync below) and confirmed working against the real sandbox (19 okt
            // 2026: SalesOrderId 12345, via the standalone Procurement.Tools.DigiKeyOrderTester).
            // OrderStatus: GetOrderStatusAsync actually calls Order Management v1 (3-legged, same
            // token as Ordering) rather than the also-implemented OrderStatus v4 — the latter 403s
            // against the sandbox ("not authorized") despite showing Enabled in DigiKey's portal,
            // the same portal/backend-desync symptom seen earlier for Product Information V4 (fixed
            // there after contacting DigiKey support). See GetOrderStatusViaOrderStatusApiAsync's
            // comment for the trade-off (less data, but confirmed reachable today).
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

        public async Task<Core.Models.SupplierOrderStatus> GetOrderStatusAsync(string supplierOrderNumber)
        {
            if (_options.UseMockData)
            {
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

            return await GetOrderStatusViaOrderManagementApiAsync(supplierOrderNumber).ConfigureAwait(false);
        }

        private static int ParseSingleSalesOrderId(string supplierOrderNumber)
        {
            // salesOrderId is a path-integer per both specs below — a comma-joined multi-ID order
            // number (the "SalesOrderId == 0, fulfilled via Marketplace" case from CreateOrderAsync)
            // has no single ID to query, so that's called out explicitly rather than silently
            // querying the wrong thing.
            if (!int.TryParse(supplierOrderNumber, out var salesOrderId))
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    $"'{supplierOrderNumber}' is geen enkelvoudig DigiKey SalesOrderId (bv. een Marketplace-order met meerdere " +
                    "SalesOrderIds_DKPlus-nummers) — OrderStatus kan daar nog niet op bevraagd worden.");
            }
            return salesOrderId;
        }

        /// <summary>
        /// GET /OrderManagement/v1/SalesOrders/{salesOrderId} against DigiKey's confirmed Order
        /// Management v1 Swagger spec (supplied directly by the user, same batch as OrderStatus v4 —
        /// see DigiKeyDtos.cs). This is the *active* real-mode implementation (see Capabilities'
        /// comment for why, not OrderStatus v4 below) — it uses oauth2AccessCodeSecurity, the same
        /// 3-legged token Ordering already proved works, via IDigiKeyOrderingHttpClient.GetOrderAsync.
        /// Trade-off versus OrderStatus v4: SalesOrderDetailView has no price field at all (so
        /// ConfirmedUnitPrice/price-deviation detection is simply unavailable via this path) and no
        /// order-status enum (PurchaseOrderStatus is inferred from the quantity fields instead of a
        /// real status code DigiKey assigns).
        /// </summary>
        private async Task<Core.Models.SupplierOrderStatus> GetOrderStatusViaOrderManagementApiAsync(string supplierOrderNumber)
        {
            RequireHttpClient();
            if (!(_httpClient is IDigiKeyOrderingHttpClient orderingHttpClient))
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "DigiKeyAdapter's ISupplierHttpClient does not support Order Management (expected a DigiKeyHttpClientWrapper).");
            }

            var salesOrderId = ParseSingleSalesOrderId(supplierOrderNumber);
            var responseJson = await orderingHttpClient.GetOrderAsync($"OrderManagement/v1/SalesOrders/{salesOrderId}").ConfigureAwait(false);
            var dto = Deserialize<DigiKeySalesOrderViewDto>(responseJson);

            if (dto == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"DigiKey found no sales order for SalesOrderId {salesOrderId}.");
            }

            var details = dto.Details ?? new List<DigiKeySalesOrderDetailViewDto>();

            return new Core.Models.SupplierOrderStatus
            {
                SupplierOrderNumber = supplierOrderNumber,
                Status = InferStatusFromOrderManagementDetails(details),
                ConfirmedAt = null, // SalesOrderView carries no entered/confirmed date (unlike OrderStatus v4's DateEntered)
                Lines = details.Select(MapOrderManagementDetail).ToList()
            };
        }

        private static Core.Models.SupplierOrderStatusLine MapOrderManagementDetail(DigiKeySalesOrderDetailViewDto detail)
        {
            return new Core.Models.SupplierOrderStatusLine
            {
                SupplierPartNumber = detail.DigiKeyPartNumber,
                ConfirmedQuantity = detail.Quantity,
                ConfirmedUnitPrice = null, // not present in SalesOrderDetailView
                EstimatedShipDate = detail.Schedules?.FirstOrDefault()?.ScheduleDate
            };
        }

        // No status enum exists in Order Management v1's response at all (unlike OrderStatus v4) —
        // approximated from aggregate quantities across every line. Can never produce Completed
        // (there's no delivery-confirmation signal here, only shipped/backorder/ordered quantities).
        private static PurchaseOrderStatus InferStatusFromOrderManagementDetails(List<DigiKeySalesOrderDetailViewDto> details)
        {
            if (details.Count == 0) return PurchaseOrderStatus.Acknowledged;

            var totalQuantity = details.Sum(d => d.Quantity);
            var totalShipped = details.Sum(d => d.QuantityShipped);
            var totalBackorder = details.Sum(d => d.QuantityBackorder);

            if (totalBackorder > 0) return PurchaseOrderStatus.Backorder;
            if (totalShipped <= 0) return PurchaseOrderStatus.Confirmed;
            return totalShipped < totalQuantity ? PurchaseOrderStatus.PartiallyShipped : PurchaseOrderStatus.Shipped;
        }

        /// <summary>
        /// GET /orderstatus/v4/salesorder/{salesOrderId} against DigiKey's confirmed OrderStatus v4
        /// Swagger spec (supplied directly by the user — see DigiKeyDtos.cs). That spec says this
        /// endpoint "uses both two-legged OAuth and three-legged OAuth", so this goes through the
        /// plain ISupplierHttpClient.GetAsync — the same call path Product Information uses, which
        /// as of DigiKeyOptions.UseThreeLeggedForProductInformation (okt 2026) means this now goes
        /// through the 3-legged Ordering token too, not the 2-legged client-credentials one.
        ///
        /// Currently unused (see Capabilities' comment and GetOrderStatusViaOrderManagementApiAsync
        /// above) — this 403s against the sandbox despite showing Enabled in DigiKey's portal. Kept
        /// rather than deleted: it's already correct against the confirmed spec and gives richer data
        /// (an explicit status code, price) than the Order Management path currently in use, so swap
        /// GetOrderStatusAsync's call back to this once DigiKey resolves the 403 instead of
        /// rewriting it from scratch.
        /// </summary>
        private async Task<Core.Models.SupplierOrderStatus> GetOrderStatusViaOrderStatusApiAsync(string supplierOrderNumber)
        {
            RequireHttpClient();
            var salesOrderId = ParseSingleSalesOrderId(supplierOrderNumber);

            var responseJson = await _httpClient.GetAsync($"orderstatus/v4/salesorder/{salesOrderId}").ConfigureAwait(false);
            var dto = Deserialize<DigiKeySalesOrderDto>(responseJson);

            if (dto == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"DigiKey found no sales order for SalesOrderId {salesOrderId}.");
            }

            return new Core.Models.SupplierOrderStatus
            {
                SupplierOrderNumber = supplierOrderNumber,
                Status = MapSalesOrderStatus(dto.Status?.SalesOrderStatus),
                ConfirmedAt = dto.DateEntered,
                Lines = (dto.LineItems ?? new List<DigiKeyOrderStatusLineItemDto>())
                    .Select(MapOrderStatusLine)
                    .ToList()
            };
        }

        private static Core.Models.SupplierOrderStatusLine MapOrderStatusLine(DigiKeyOrderStatusLineItemDto line)
        {
            // The first Schedule's ScheduledDate ("date the shipment is scheduled to be delivered to
            // the customer", per the spec) is what downstream late-delivery checks compare against
            // the requested due date — ItemShipments is about what has already shipped, not a
            // forecast, so that's not used here even though it also carries dates.
            var estimatedShipDate = line.Schedules?.FirstOrDefault()?.ScheduledDate;

            return new Core.Models.SupplierOrderStatusLine
            {
                SupplierPartNumber = line.DigiKeyProductNumber,
                ConfirmedQuantity = line.QuantityOrdered,
                ConfirmedUnitPrice = (decimal)line.UnitPrice,
                EstimatedShipDate = estimatedShipDate
            };
        }

        // DigiKey's SalesOrderStatus has sixteen specific values with no 1:1 equivalent in this
        // app's own, much coarser PurchaseOrderStatus — best-effort mapping, not a confirmed
        // contract the way the field names themselves are. Re-check against a real response if a
        // mapped value looks wrong in practice.
        private static PurchaseOrderStatus MapSalesOrderStatus(string digiKeyStatus)
        {
            switch (digiKeyStatus)
            {
                case "Received":
                    return PurchaseOrderStatus.Acknowledged;
                case "ProcessingPartialShipment":
                    return PurchaseOrderStatus.PartiallyShipped;
                case "ProcessingAwaitingBackorders":
                case "ProcessingShipBackorder":
                case "GenericDelay":
                    return PurchaseOrderStatus.Backorder;
                case "Processing":
                case "Processing3rdParty":
                case "ProcessingScheduledShipmentMultipleRelease":
                case "ProcessingScheduledShipmentSingleRelease":
                case "ProcessingScheduledShipmentMsc":
                    return PurchaseOrderStatus.Confirmed;
                case "Shipped":
                    return PurchaseOrderStatus.Shipped;
                case "Delivered":
                    return PurchaseOrderStatus.Completed;
                case "Canceled":
                    return PurchaseOrderStatus.Cancelled;
                case "Proforma":
                case "ActionRequiredWireTransfer":
                case "Unknown":
                default:
                    return PurchaseOrderStatus.Submitted;
            }
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
