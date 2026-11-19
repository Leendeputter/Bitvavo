using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Models;
using Procurement.Suppliers.Farnell;

namespace Procurement.Services.Orders
{
    /// <summary>One Farnell ship-to address as the picker shows it.</summary>
    public class ShipToAddressOption
    {
        public string AddressId { get; set; }
        public string Text { get; set; }
    }

    /// <summary>One deviating line of an order confirmation, as OrderConfirmationExceptionsForm shows it.</summary>
    public class ConfirmationExceptionRow
    {
        public string ErpPoNumber { get; set; }
        public string SupplierCode { get; set; }
        public string SupplierPartNumber { get; set; }
        public int OrderedQuantity { get; set; }
        public int? ConfirmedQuantity { get; set; }
        public decimal OrderedUnitPrice { get; set; }
        public decimal? ConfirmedUnitPrice { get; set; }
        public DateTime? RequiredDate { get; set; }
        public DateTime? ConfirmedShipDate { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>Outcome of "Process Order Confirmations", with the same texts as the desktop.</summary>
    public class ConfirmationSummary
    {
        public int CheckedOrderCount { get; set; }
        public int SkippedOrderCount { get; set; }
        public IReadOnlyList<ConfirmationExceptionRow> Exceptions { get; set; }

        public string StatusText
        {
            get
            {
                var summary = $"Orderbevestiging verwerkt: {CheckedOrderCount} order(s) gecontroleerd";
                if (SkippedOrderCount > 0) summary += $", {SkippedOrderCount} overgeslagen (zie Audit Log voor de reden per order)";
                if (Exceptions.Count > 0) summary += $", {Exceptions.Count} afwijking(en)";
                return summary + ".";
            }
        }

        /// <summary>The message the desktop shows in a box when there are no deviations; null when there are.</summary>
        public string MessageText
        {
            get
            {
                if (Exceptions.Count > 0) return null;
                if (CheckedOrderCount > 0) return "Alle gecontroleerde orders zijn bevestigd zoals besteld — geen afwijkingen.";
                if (SkippedOrderCount > 0)
                    return $"{SkippedOrderCount} order(s) wachten op een bevestiging, maar konden niet gecontroleerd worden — zie Audit Log voor de reden per order.";
                return "Geen orders gevonden die op een bevestiging wachten.";
            }
        }
    }

    /// <summary>
    /// "Place Order" and "Process Order Confirmations" of the Purchase Requests screen, taken out of
    /// MainForm (web plan, 19 nov 2026) so the web does exactly the same: bundle per supplier via
    /// ProcurementEngine.PlaceOrdersAsync, and ask for Farnell's ship-to address fresh every time when
    /// there is more than one (user request, 21 okt 2026). On the desktop the address is asked in a
    /// dialog in the middle of placing; the web can't pause a request, so it asks first
    /// (<see cref="GetShipToChoicesAsync"/>) and passes the choice along with the order.
    /// </summary>
    public class OrderPlacementService
    {
        private readonly ProcurementServices _services;

        public OrderPlacementService(ProcurementServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public bool OrderingEnabled => _services.OrderingEnabled;

        /// <summary>
        /// The Farnell addresses to choose from for this selection. Empty when Farnell isn't part of
        /// it or there is nothing to choose (zero or one address): then no picker is needed.
        /// </summary>
        public async Task<IReadOnlyList<ShipToAddressOption>> GetShipToChoicesAsync(IReadOnlyList<int> requestIds)
        {
            var addresses = await GetFarnellAddressesIfNeededAsync(requestIds);
            if (addresses == null || addresses.Count <= 1) return Array.Empty<ShipToAddressOption>();
            return addresses.Select(a => new ShipToAddressOption { AddressId = a.AddressId, Text = FormatAddress(a) }).ToList();
        }

        /// <summary>
        /// Places the orders. <paramref name="shipToAddressId"/> is the picker's choice; it is checked
        /// against Farnell's live list again, so a stale or made-up id never reaches an order.
        /// </summary>
        public async Task PlaceOrdersAsync(IReadOnlyList<int> requestIds, string shipToAddressId)
        {
            if (!_services.OrderingEnabled)
                throw new InvalidOperationException("Bestellen staat uit (Ordering.Enabled = false). Er is geen order geplaatst.");

            var addresses = await GetFarnellAddressesIfNeededAsync(requestIds);
            if (addresses != null)
            {
                var farnell = FarnellAdapter();
                if (addresses.Count <= 1)
                {
                    farnell.SelectedShipToAddressId = addresses.FirstOrDefault()?.AddressId;
                }
                else
                {
                    var chosen = addresses.FirstOrDefault(a => string.Equals(a.AddressId, shipToAddressId, StringComparison.Ordinal));
                    if (chosen == null)
                        throw new InvalidOperationException("Kies eerst een Farnell-afleveradres. Er is geen order geplaatst.");
                    farnell.SelectedShipToAddressId = chosen.AddressId;
                }
            }

            await _services.Engine.PlaceOrdersAsync(requestIds, _services.Session.UserName);
        }

        public async Task<ConfirmationSummary> ProcessOrderConfirmationsAsync()
        {
            var result = await _services.Engine.ProcessOrderConfirmationsAsync(_services.Session.UserName);
            return new ConfirmationSummary
            {
                CheckedOrderCount = result.CheckedOrderCount,
                SkippedOrderCount = result.SkippedOrderCount,
                Exceptions = result.Exceptions.Select(e => new ConfirmationExceptionRow
                {
                    ErpPoNumber = e.ErpPoNumber,
                    SupplierCode = e.SupplierCode,
                    SupplierPartNumber = e.SupplierPartNumber,
                    OrderedQuantity = e.OrderedQuantity,
                    ConfirmedQuantity = e.ConfirmedQuantity,
                    OrderedUnitPrice = e.OrderedUnitPrice,
                    ConfirmedUnitPrice = e.ConfirmedUnitPrice,
                    RequiredDate = e.RequiredDate,
                    ConfirmedShipDate = e.ConfirmedShipDate,
                    Reason = DescribeReason(e)
                }).ToList()
            };
        }

        /// <summary>Null when Farnell isn't in this selection (or not configured), otherwise the live list.</summary>
        private async Task<IReadOnlyList<FarnellShipToAddress>> GetFarnellAddressesIfNeededAsync(IReadOnlyList<int> requestIds)
        {
            var supplierCodes = await _services.Engine.PreviewSupplierCodesForOrderAsync(requestIds);
            if (!supplierCodes.Contains("FARNELL", StringComparer.OrdinalIgnoreCase)) return null;

            var farnell = FarnellAdapter();
            if (farnell == null) return null;
            return await farnell.GetAvailableShipToAddressesAsync();
        }

        private FarnellAdapter FarnellAdapter() => _services.Adapters.OfType<FarnellAdapter>().FirstOrDefault();

        public static string FormatAddress(FarnellShipToAddress address)
        {
            var parts = new[]
            {
                address.NickName,
                address.OrganizationName,
                address.AddressLine1,
                address.City,
                address.PostalCode,
                address.Country
            }.Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join(", ", parts);
        }

        public static string DescribeReason(OrderConfirmationException e)
        {
            var reasons = new List<string>();
            if (e.QuantityMismatch) reasons.Add("quantity mismatch");
            if (e.PriceMismatch) reasons.Add("price mismatch");
            if (e.DeliveryIsLate) reasons.Add("delivery late");
            return string.Join(", ", reasons);
        }
    }
}
