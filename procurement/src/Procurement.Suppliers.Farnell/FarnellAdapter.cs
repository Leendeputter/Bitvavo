using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Procurement.Core.Enums;
using Procurement.Core.Exceptions;
using Procurement.Core.Interfaces;
using Procurement.Core.Mocking;
using Procurement.Core.Models;
using Procurement.Suppliers.Farnell.Http;

namespace Procurement.Suppliers.Farnell
{
    public class FarnellAdapter : ISupplierAdapter
    {
        public const string Code = "FARNELL";

        private readonly FarnellOptions _options;
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
            // Ordering is real in real-mode now (21 okt 2026 — Order API spec pasted by the user,
            // see CreateRealOrderAsync below).
            Ordering = CapabilityStatus.Supported,
            // ManualProcess, not Supported: the Order API spec the user pasted (the full "APIs
            // available for managing orders" list, confirmed by the user to be the complete list)
            // has no order-status/order-history endpoint at all, only cart+submit. Processing Order
            // Confirmations skips an adapter whose OrderStatus isn't Supported, so this keeps that
            // step from attempting (and failing on) a call that doesn't exist, rather than Farnell
            // orders staying mock-only here too.
            OrderStatus = CapabilityStatus.ManualProcess,
            Shipment = CapabilityStatus.ManualProcess,
            Tracking = CapabilityStatus.ManualProcess,
            Invoice = CapabilityStatus.ManualProcess
        };

        /// <summary>
        /// Ship-to address chosen for the order currently being placed — set by the UI (MainForm,
        /// before calling ProcurementEngine.PlaceOrdersAsync) after showing the user a picker built
        /// from GetAvailableShipToAddressesAsync's live list, since Farnell's own default sometimes
        /// isn't the right one (user, 21 okt 2026: "dit gaat bij Farnell wel eens fout"). Null means
        /// "use whatever GetAvailableShipToAddressesAsync returns first" — the fallback a caller with
        /// no picker UI (e.g. a future console tester, mirroring DigiKeyOrderTester) gets for free.
        /// A single mutable property rather than a per-call parameter threaded through
        /// ISupplierAdapter: this app places orders one at a time from a single-user WinForms UI
        /// (same assumption ProcurementDbContext's own long-lived-single-instance design already
        /// makes), so there is never a second order's selection to clash with.
        /// </summary>
        public string SelectedShipToAddressId { get; set; }

        public FarnellAdapter(FarnellOptions options, ISupplierHttpClient httpClient = null)
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
                    "ManufacturerPartNumber is required to search Farnell.");
            }

            if (_options.UseMockData)
            {
                var product = FarnellMockDataProvider.BuildProduct(request.Manufacturer, request.ManufacturerPartNumber);
                return new List<SupplierProduct> { product };
            }

            var term = Uri.EscapeDataString($"manuPartNum:{request.ManufacturerPartNumber}");
            var products = await FetchProductsAsync($"catalog/products?term={term}&resultsSettings.responseGroup=large")
                .ConfigureAwait(false);

            if (products.Count == 0)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"Farnell found no products for '{request.ManufacturerPartNumber}'.");
            }

            return products.Select(p => MapProduct(p, request)).ToList();
        }

        public async Task<SupplierProduct> GetProductAsync(string supplierPartNumber)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
            {
                var isExample = supplierPartNumber == "FN-CRCW060310K0FKEA";
                return new SupplierProduct
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
            }

            var dto = await FetchProductBySkuAsync(supplierPartNumber).ConfigureAwait(false);
            return MapProduct(dto, null);
        }

        public async Task<SupplierAvailability> GetAvailabilityAsync(string supplierPartNumber)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return FarnellMockDataProvider.BuildAvailability(supplierPartNumber);

            var dto = await FetchProductBySkuAsync(supplierPartNumber).ConfigureAwait(false);
            return MapAvailability(dto);
        }

        public async Task<SupplierPricing> GetPricingAsync(string supplierPartNumber, int quantity)
        {
            if (quantity <= 0)
                throw new SupplierException(Code, SupplierErrorCode.InvalidQuantity, "Quantity must be positive.");
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return FarnellMockDataProvider.BuildPricing(supplierPartNumber, quantity);

            var dto = await FetchProductBySkuAsync(supplierPartNumber).ConfigureAwait(false);
            return MapPricing(dto, quantity);
        }

        public async Task<IReadOnlyList<SupplierPackagingOption>> GetPackagingOptionsAsync(string supplierPartNumber, int quantity)
        {
            EnsureKnownPart(supplierPartNumber);

            if (_options.UseMockData)
                return FarnellMockDataProvider.BuildPackagingOptions(supplierPartNumber, quantity);

            var dto = await FetchProductBySkuAsync(supplierPartNumber).ConfigureAwait(false);
            return MapPackagingOptions(dto, quantity);
        }

        // element14's catalog/products endpoint returns pricing/stock/datasheets in the same
        // response as the product itself (no separate pricing/availability endpoints), so every
        // real-mode read method above funnels through this single-SKU lookup.
        private async Task<FarnellProductDto> FetchProductBySkuAsync(string supplierPartNumber)
        {
            var term = Uri.EscapeDataString($"id:{supplierPartNumber}");
            var products = await FetchProductsAsync($"catalog/products?term={term}&resultsSettings.responseGroup=large")
                .ConfigureAwait(false);

            var dto = products.FirstOrDefault();
            if (dto == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.ProductNotFound,
                    $"Farnell found no product for '{supplierPartNumber}'.");
            }
            return dto;
        }

        private async Task<List<FarnellProductDto>> FetchProductsAsync(string relativeUrl)
        {
            RequireHttpClient();
            var responseJson = await _httpClient.GetAsync(relativeUrl).ConfigureAwait(false);
            var response = Deserialize<FarnellSearchResponse>(responseJson);

            // element14 wraps the result differently depending on which query type was used
            // (part number vs. keyword vs. manufacturer part number search) — response wrapper
            // name is unconfirmed against a real account, so all three known wrapper names are
            // tried before giving up.
            var result = response?.Result ?? response?.ManufacturerPartNumberResult ?? response?.KeywordResult;
            return result?.Products ?? new List<FarnellProductDto>();
        }

        private static SupplierProduct MapProduct(FarnellProductDto dto, ProductSearchRequest request)
        {
            var confidence = request != null &&
                              string.Equals(dto.ManufacturerPartNumber, request.ManufacturerPartNumber, StringComparison.OrdinalIgnoreCase)
                ? MatchConfidence.Exact
                : MatchConfidence.Medium;

            return new SupplierProduct
            {
                SupplierPartNumber = dto.Sku,
                Manufacturer = dto.BrandName,
                ManufacturerPartNumber = dto.ManufacturerPartNumber,
                Description = dto.DisplayName,
                MatchConfidence = confidence,
                DatasheetUrl = dto.Datasheets?.FirstOrDefault()?.Url
            };
        }

        private static SupplierAvailability MapAvailability(FarnellProductDto dto)
        {
            var available = dto.Stock?.Level ?? 0;
            // element14's product search response has no explicit lead-time field for in-stock
            // parts; approximated the same way as DigiKey (0 when in stock, a conservative
            // fallback otherwise) until a real response shows an actual lead-time field.
            var leadTimeDays = available > 0 ? 0 : 14;

            return new SupplierAvailability
            {
                SupplierPartNumber = dto.Sku,
                AvailableQuantity = available,
                LeadTimeDays = leadTimeDays,
                EstimatedDeliveryDate = DateTime.UtcNow.AddDays(leadTimeDays),
                OnBackorder = available <= 0
            };
        }

        private static SupplierPricing MapPricing(FarnellProductDto dto, int quantity)
        {
            var priceBreaks = (dto.Prices ?? new List<FarnellPriceDto>())
                .Select(p => new PriceBreak { BreakQuantity = p.From, UnitPrice = p.Cost })
                .OrderBy(pb => pb.BreakQuantity)
                .ToList();

            var applicable = priceBreaks.LastOrDefault(pb => pb.BreakQuantity <= quantity) ?? priceBreaks.FirstOrDefault();

            int.TryParse(dto.MinimumOrderQuantityRaw, out var moq);
            int.TryParse(dto.OrderMultipleRaw, out var multiple);

            return new SupplierPricing
            {
                SupplierPartNumber = dto.Sku,
                RequestedQuantity = quantity,
                UnitPrice = applicable?.UnitPrice ?? 0m,
                Currency = dto.Prices?.FirstOrDefault()?.Currency ?? "EUR",
                MinimumOrderQuantity = moq > 0 ? moq : 1,
                OrderMultiple = multiple > 0 ? multiple : 1,
                PriceBreaks = priceBreaks
            };
        }

        // element14's product search response doesn't expose a distinct reel/cut-tape packaging
        // breakdown the way DigiKey's ProductVariations does — it's a single SKU per packaging
        // form. Approximated here as one OriginalReel option sized to the order multiple, until a
        // real response clarifies whether Farnell exposes packaging variants some other way.
        private static IReadOnlyList<SupplierPackagingOption> MapPackagingOptions(FarnellProductDto dto, int quantity)
        {
            int.TryParse(dto.OrderMultipleRaw, out var multiple);
            return new List<SupplierPackagingOption>
            {
                new SupplierPackagingOption
                {
                    SupplierPartNumber = dto.Sku,
                    PackagingType = PackagingType.OriginalReel,
                    PackagingQuantity = multiple > 0 ? multiple : quantity,
                    ReelType = Core.Enums.ReelType.ManufacturerOriginal,
                    ReelingFee = 0m,
                    AvailableQuantity = dto.Stock?.Level ?? 0
                }
            };
        }

        public Task<SupplierOrderResult> CreateOrderAsync(SupplierOrderRequest request, string idempotencyKey)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentNullException(nameof(idempotencyKey));

            if (!_options.UseMockData)
                return CreateRealOrderAsync(request, idempotencyKey);

            if (OrdersByIdempotencyKey.TryGetValue(idempotencyKey, out var existing))
                return Task.FromResult(existing.Result);

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
                Status = PurchaseOrderStatus.Acknowledged,
                OrderTotal = orderTotal,
                Currency = request.Currency ?? "EUR",
                SubmittedAt = DateTime.UtcNow
            };

            OrdersByIdempotencyKey[idempotencyKey] = (result, request.Lines);
            return Task.FromResult(result);
        }

        /// <summary>
        /// Order API spec pasted by the user 21 okt 2026 (partner.element14.com/order, blocked for
        /// this session — see README). Unlike DigiKey's Ordering v3 (one atomic POST), this is a
        /// stateful cart: mint a JWT, add each line, explicitly clear any stale cart state first
        /// (the spec has no "is this cart empty" check, and a previous failed attempt could have
        /// left items behind — same idempotency concern CancelOrderAsync's mock-mode removal
        /// already guards against, just for a different failure mode), pick a shipping address and
        /// method, then review and submit.
        /// </summary>
        private async Task<SupplierOrderResult> CreateRealOrderAsync(SupplierOrderRequest request, string idempotencyKey)
        {
            RequireHttpClient();
            if (!(_httpClient is IFarnellOrderingHttpClient orderingClient))
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "FarnellAdapter's ISupplierHttpClient does not support the Order API (expected a FarnellHttpClientWrapper).");
            }
            if (string.IsNullOrWhiteSpace(_options.AccountId))
            {
                throw new SupplierException(Code, SupplierErrorCode.AuthenticationError,
                    "Farnell AccountId (trade account number) is not configured — set it via Instellingen -> Suppliers.");
            }
            if (request.Lines == null || request.Lines.Count == 0)
            {
                throw new SupplierException(Code, SupplierErrorCode.InvalidQuantity, "Order has no lines.");
            }

            var jwt = await orderingClient.AuthenticateOrderingAsync(_options.AccountId).ConfigureAwait(false);

            await orderingClient.PostOrderingAsync("cart/clear", null, jwt).ConfigureAwait(false);

            var addItemRequest = new FarnellAddCartItemRequest
            {
                OrderItem = request.Lines.Select(l => new FarnellCartItemRequest
                {
                    Quantity = l.Quantity.ToString(),
                    Sku = l.SupplierPartNumber
                }).ToList()
            };
            await orderingClient.PostOrderingAsync("cart/addItem", JsonConvert.SerializeObject(addItemRequest), jwt)
                .ConfigureAwait(false);

            var addresses = await GetAvailableShipToAddressesViaAsync(orderingClient, jwt).ConfigureAwait(false);
            if (addresses.Count == 0)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "Farnell account has no shipping addresses configured (GET /order/shipping_address returned none).");
            }
            var chosenAddressId = !string.IsNullOrEmpty(SelectedShipToAddressId)
                ? SelectedShipToAddressId
                : addresses[0].AddressId;
            await orderingClient.PostOrderingAsync("order/shipping_address",
                JsonConvert.SerializeObject(new FarnellConfirmAddressRequest { AddressId = chosenAddressId }), jwt)
                .ConfigureAwait(false);

            var methodsJson = await orderingClient.GetOrderingAsync("order/shipping_methods", jwt).ConfigureAwait(false);
            var methodsResponse = Deserialize<FarnellShippingMethodsResponse>(methodsJson);
            var methods = methodsResponse?.UsableShippingMode ?? new List<FarnellShippingMethodDto>();
            var chosenMethod = methods.FirstOrDefault(m => string.Equals(m.ShippingPreferredMethod, "true", StringComparison.OrdinalIgnoreCase))
                ?? methods.FirstOrDefault();
            if (chosenMethod == null)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "Farnell returned no usable shipping methods (GET /order/shipping_methods).");
            }
            await orderingClient.PostOrderingAsync("order/shipping_method",
                JsonConvert.SerializeObject(new FarnellConfirmShippingMethodRequest { ShippingMethodId = chosenMethod.ShippingMethodId }), jwt)
                .ConfigureAwait(false);

            var reviewJson = await orderingClient.GetOrderingAsync("order/order_review", jwt).ConfigureAwait(false);
            var review = Deserialize<FarnellOrderReviewResponse>(reviewJson);
            decimal.TryParse(review?.GrandTotal, out var grandTotal);

            var submitJson = await orderingClient.PostOrderingAsync("order/order_submit",
                JsonConvert.SerializeObject(new FarnellOrderSubmitRequest { PoNumberId = request.ErpPoNumber }), jwt)
                .ConfigureAwait(false);
            var submitResponse = Deserialize<FarnellOrderSubmitResponse>(submitJson);
            if (string.IsNullOrEmpty(submitResponse?.OrderReference))
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "Farnell order_submit did not return an orderReference.");
            }

            return new SupplierOrderResult
            {
                Success = true,
                SupplierOrderNumber = submitResponse.OrderReference,
                Status = PurchaseOrderStatus.Acknowledged,
                OrderTotal = grandTotal > 0 ? grandTotal : request.Lines.Sum(l => l.Quantity * l.UnitPrice),
                Currency = request.Currency ?? "EUR",
                SubmittedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Live list of the trade account's saved ship-to addresses — used by MainForm to build the
        /// picker it shows before calling ProcurementEngine.PlaceOrdersAsync (user request, 21 okt
        /// 2026: pick per order, not a stored default, "dit gaat bij Farnell wel eens fout"). In
        /// mock mode there is no real account to ask, so this returns a single synthetic entry —
        /// callers don't need their own mock/real branch, and MainForm's picker still "works"
        /// (effectively a no-op single choice) against a mock-mode Farnell.
        /// </summary>
        public async Task<IReadOnlyList<FarnellShipToAddress>> GetAvailableShipToAddressesAsync()
        {
            if (_options.UseMockData)
            {
                return new List<FarnellShipToAddress>
                {
                    new FarnellShipToAddress { AddressId = "MOCK", NickName = "Mock default address (UseMockData=true)" }
                };
            }

            RequireHttpClient();
            if (!(_httpClient is IFarnellOrderingHttpClient orderingClient))
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "FarnellAdapter's ISupplierHttpClient does not support the Order API (expected a FarnellHttpClientWrapper).");
            }
            if (string.IsNullOrWhiteSpace(_options.AccountId))
            {
                throw new SupplierException(Code, SupplierErrorCode.AuthenticationError,
                    "Farnell AccountId (trade account number) is not configured — set it via Instellingen -> Suppliers.");
            }

            var jwt = await orderingClient.AuthenticateOrderingAsync(_options.AccountId).ConfigureAwait(false);
            return await GetAvailableShipToAddressesViaAsync(orderingClient, jwt).ConfigureAwait(false);
        }

        private async Task<IReadOnlyList<FarnellShipToAddress>> GetAvailableShipToAddressesViaAsync(
            IFarnellOrderingHttpClient orderingClient, string jwt)
        {
            var json = await orderingClient.GetOrderingAsync("order/shipping_address", jwt).ConfigureAwait(false);
            var response = Deserialize<FarnellShippingAddressListResponse>(json);
            return (response?.Address ?? new List<FarnellShippingAddressDto>())
                .Select(a => new FarnellShipToAddress
                {
                    AddressId = a.AddressId,
                    NickName = a.NickName,
                    OrganizationName = a.OrganizationName,
                    Attention = a.Attention,
                    AddressLine1 = a.AddressLine1,
                    AddressLine2 = a.AddressLine2,
                    AddressLine3 = a.AddressLine3,
                    City = a.City,
                    PostalCode = a.PostalCode,
                    Region = a.Region,
                    Country = a.Country
                })
                .ToList();
        }

        public Task<Core.Models.SupplierOrderStatus> GetOrderStatusAsync(string supplierOrderNumber)
        {
            if (!_options.UseMockData)
            {
                throw new SupplierException(Code, SupplierErrorCode.UnknownError,
                    "Farnell real-mode order status is not implemented — see CreateOrderAsync.");
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
                    "Farnell real-mode order cancellation is not implemented — see CreateOrderAsync.");
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
                    "FarnellAdapter has UseMockData=false but no ISupplierHttpClient was supplied.");
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
                    "Could not parse Farnell API response.", ex);
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
