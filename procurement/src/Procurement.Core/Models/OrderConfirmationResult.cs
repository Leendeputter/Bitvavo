using System;
using System.Collections.Generic;

namespace Procurement.Core.Models
{
    /// <summary>
    /// Result of ProcurementEngine.ProcessOrderConfirmationsAsync — every checked PO gets its
    /// confirmation applied automatically (local tracking updated, MAX Reference/duedate/Confirming
    /// fields written in real mode), regardless of whether it ends up here. This only collects the
    /// lines that need a human look: quantity/price deviating from what was ordered, or a delivery
    /// date confirmed later than requested. MainForm shows only this list, not a report of everything
    /// that went through cleanly.
    /// </summary>
    public class OrderConfirmationResult
    {
        public int CheckedOrderCount { get; set; }
        public int SkippedOrderCount { get; set; }
        public List<OrderConfirmationException> Exceptions { get; } = new List<OrderConfirmationException>();
    }

    public class OrderConfirmationException
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

        public bool QuantityMismatch => ConfirmedQuantity.HasValue && ConfirmedQuantity.Value != OrderedQuantity;
        public bool PriceMismatch => ConfirmedUnitPrice.HasValue && ConfirmedUnitPrice.Value != OrderedUnitPrice;
        public bool DeliveryIsLate => ConfirmedShipDate.HasValue && RequiredDate.HasValue && ConfirmedShipDate.Value.Date > RequiredDate.Value.Date;
    }
}
