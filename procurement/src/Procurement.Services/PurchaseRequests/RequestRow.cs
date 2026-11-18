using System;

namespace Procurement.Services.PurchaseRequests
{
    /// <summary>
    /// One row of the Purchase Requests grid. Property order is the column order (desktop binds it
    /// directly); names are the column names the screens format by. Was an anonymous type in
    /// MainForm until 18 nov 2026.
    /// </summary>
    public class RequestRow
    {
        public int Id { get; set; }
        public string Order { get; set; }

        /// <summary>This app's own workflow status (Pending/Sourcing/...), not MAX's STATUS_10 (that's <see cref="Status"/>).</summary>
        public string Workflow { get; set; }
        public string Status { get; set; }
        public bool Firm { get; set; }
        public string Type { get; set; }
        public string PartID { get; set; }
        public string Rev { get; set; }
        public string Desc1 { get; set; }
        public string Desc2 { get; set; }
        public int Quantity { get; set; }
        public decimal? Cost { get; set; }
        public decimal? Cnv { get; set; }

        /// <summary>Quantity * Cost / Cost Conv; blank when either is unknown or Conv is 0.</summary>
        public decimal? ExtCost { get; set; }
        public DateTime? DueDate { get; set; }

        /// <summary>MAX's CalculateMRPDueDateFromPODueDate applied to DueDate: the date to give the supplier.</summary>
        public DateTime? PoDueDate { get; set; }
        public string Reference { get; set; }
        public string ManufacturingPart { get; set; }
        public string Customer { get; set; }
        public string StockID { get; set; }

        /// <summary>Part_Master.RECVEN_01, the article's preferred vendor (live lookup).</summary>
        public string RecVendor { get; set; }

        // What was chosen at sourcing; blank until there is a selection.
        public string SelectedSupplier { get; set; }
        public decimal? SelectedPrice { get; set; }
        public int? SelectedQuantity { get; set; }
        public DateTime? SelectedDueDate { get; set; }
    }
}
