using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;
using Procurement.Core.Models;

namespace Procurement.Services.Orders
{
    public class OrderRow
    {
        public int Id { get; set; }
        public string ErpPoNumber { get; set; }
        public string Supplier { get; set; }
        public string SupplierOrderNumber { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public decimal OrderTotal { get; set; }
        public string Currency { get; set; }
        public int Lines { get; set; }
    }

    public class OrderLineRow
    {
        public int Id { get; set; }
        public string ErpRequestNumber { get; set; }
        public string PartID { get; set; }
        public string SupplierPartNumber { get; set; }
        public string MaxStatus { get; set; }
        public bool Confirmed { get; set; }
        public int Quantity { get; set; }
        public int? ConfirmedQuantity { get; set; }
        public bool? QuantityMismatch { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? ConfirmedUnitPrice { get; set; }
        public bool? PriceMismatch { get; set; }
        public DateTime? RequiredDate { get; set; }
        public DateTime? ConfirmedShipDate { get; set; }
        public bool? DeliveryIsLate { get; set; }
        public decimal LineTotal { get; set; }

        /// <summary>Quantity, price or date deviates from the confirmation: shown orange, like the desktop.</summary>
        public bool Deviates => QuantityMismatch == true || PriceMismatch == true || DeliveryIsLate == true;
    }

    public class OrderListResult
    {
        public IReadOnlyList<OrderRow> Rows { get; set; }
        public string StatusText { get; set; }
    }

    public class OrderLinesResult
    {
        public IReadOnlyList<OrderLineRow> Rows { get; set; } = new List<OrderLineRow>();
        public string StatusText { get; set; }
    }

    /// <summary>
    /// The purchase-order register for the web app (18 nov 2026), same rules as the desktop's
    /// OrdersForm: every PO for this company, the MAX line status (Order_Master.STATUS_10) per line,
    /// orders deleted in MAX hidden unless asked for, completed lines ("4") hidden unless asked for,
    /// and deviations from the supplier's confirmation flagged. Read-only.
    /// </summary>
    public class OrdersScreenService
    {
        private readonly ProcurementServices _services;

        public OrdersScreenService(ProcurementServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public async Task<OrderListResult> GetOrdersAsync(bool includeDeleted)
        {
            var (orders, statuses) = await LoadAsync();
            var visible = orders.Where(po => includeDeleted || !IsNotFoundInMax(po, statuses)).ToList();
            var hidden = orders.Count - visible.Count;
            return new OrderListResult
            {
                Rows = visible.Select(po => new OrderRow
                {
                    Id = po.Id,
                    ErpPoNumber = po.ErpPoNumber,
                    Supplier = po.SupplierCode,
                    SupplierOrderNumber = po.SupplierOrderNumber,
                    Status = po.Status.ToString(),
                    CreatedAt = po.CreatedAt,
                    SubmittedAt = po.SubmittedAt,
                    ConfirmedAt = po.ConfirmedAt,
                    OrderTotal = po.OrderTotal,
                    Currency = po.Currency,
                    Lines = po.Lines.Count
                }).ToList(),
                StatusText = $"{visible.Count} order(s)" + (hidden > 0 ? $", {hidden} verwijderd in MAX verborgen." : ".")
            };
        }

        public async Task<OrderLinesResult> GetLinesAsync(int purchaseOrderId, bool includeCompleted)
        {
            var (orders, statuses) = await LoadAsync();
            var order = orders.FirstOrDefault(o => o.Id == purchaseOrderId);
            if (order == null) return new OrderLinesResult();

            var requestLines = new Dictionary<int, PurchaseRequestLine>();
            foreach (var line in order.Lines)
            {
                if (requestLines.ContainsKey(line.PurchaseRequestLineId)) continue;
                var requestLine = await _services.PurchaseRequestRepository.GetLineByIdAsync(line.PurchaseRequestLineId);
                if (requestLine != null) requestLines[line.PurchaseRequestLineId] = requestLine;
            }

            var visible = order.Lines.Where(l => includeCompleted || MaxStatus(statuses, order.ErpPoNumber, l) != "4").ToList();
            var rows = visible.Select(l =>
            {
                requestLines.TryGetValue(l.PurchaseRequestLineId, out var requestLine);
                var confirmed = l.ConfirmedQuantity.HasValue;
                var confirmedShipDate = l.Deliveries.FirstOrDefault()?.EstimatedShipDate;
                var check = new OrderConfirmationException
                {
                    OrderedQuantity = l.Quantity,
                    ConfirmedQuantity = l.ConfirmedQuantity,
                    OrderedUnitPrice = l.UnitPrice,
                    ConfirmedUnitPrice = l.ConfirmedUnitPrice,
                    RequiredDate = requestLine?.RequiredDate,
                    ConfirmedShipDate = confirmedShipDate
                };
                return new OrderLineRow
                {
                    Id = l.Id,
                    ErpRequestNumber = requestLine?.PurchaseRequest?.ErpRequestNumber,
                    PartID = requestLine?.ErpArticleId,
                    SupplierPartNumber = l.SupplierPartNumber,
                    MaxStatus = MaxStatus(statuses, order.ErpPoNumber, l),
                    Confirmed = confirmed,
                    Quantity = l.Quantity,
                    ConfirmedQuantity = l.ConfirmedQuantity,
                    QuantityMismatch = confirmed ? (bool?)check.QuantityMismatch : null,
                    UnitPrice = l.UnitPrice,
                    ConfirmedUnitPrice = l.ConfirmedUnitPrice,
                    PriceMismatch = confirmed ? (bool?)check.PriceMismatch : null,
                    RequiredDate = requestLine?.RequiredDate,
                    ConfirmedShipDate = confirmedShipDate,
                    DeliveryIsLate = confirmed ? (bool?)check.DeliveryIsLate : null,
                    LineTotal = l.LineTotal
                };
            }).ToList();

            var unconfirmed = visible.Count(l => !l.ConfirmedQuantity.HasValue);
            var hiddenCompleted = order.Lines.Count - visible.Count;
            return new OrderLinesResult
            {
                Rows = rows,
                StatusText = $"{order.SupplierCode}: {order.Status}"
                    + (string.IsNullOrEmpty(order.SupplierOrderNumber) ? string.Empty : $" (ordernummer {order.SupplierOrderNumber})")
                    + $" — {visible.Count} regel(s), {unconfirmed} nog niet bevestigd"
                    + (hiddenCompleted > 0 ? $", {hiddenCompleted} voltooid verborgen." : ".")
            };
        }

        private async Task<(IReadOnlyList<PurchaseOrder> Orders,
            IReadOnlyDictionary<(string OrderNumber, string LineNumber, string DeliveryNumber), string> Statuses)> LoadAsync()
        {
            var orders = await _services.PurchaseOrderRepository.GetAllAsync();
            var keys = orders.SelectMany(po => po.Lines.Select(l => (po.ErpPoNumber, l.MaxLineNumber, l.MaxDeliveryNumber))).ToList();
            var statuses = await _services.MaxOrderRepository.GetLineStatusesAsync(keys);
            return (orders, statuses);
        }

        /// <summary>Raw Order_Master.STATUS_10, or null when MAX has no row (mock PO or not linked).</summary>
        private static string MaxStatus(
            IReadOnlyDictionary<(string OrderNumber, string LineNumber, string DeliveryNumber), string> statuses,
            string erpPoNumber, PurchaseOrderLine line) =>
            statuses.TryGetValue((erpPoNumber, line.MaxLineNumber, line.MaxDeliveryNumber), out var status) ? status : null;

        /// <summary>Every MAX-linked line has no MAX row any more = deleted in MAX (a partly deleted order stays visible).</summary>
        private static bool IsNotFoundInMax(PurchaseOrder order,
            IReadOnlyDictionary<(string OrderNumber, string LineNumber, string DeliveryNumber), string> statuses)
        {
            var linked = order.Lines.Where(l => !string.IsNullOrEmpty(l.MaxLineNumber)).ToList();
            return linked.Count > 0 && linked.All(l => MaxStatus(statuses, order.ErpPoNumber, l) == null);
        }
    }
}
