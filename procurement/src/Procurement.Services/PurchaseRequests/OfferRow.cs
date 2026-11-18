using System;

namespace Procurement.Services.PurchaseRequests
{
    /// <summary>
    /// One row of the offers grid under the Purchase Requests grid. Every property has a setter on
    /// purpose: the desktop grid is editable for Select and OfferedQuantity, and DataGridView refuses
    /// a non-read-only column bound to a getter-only property. Was MainForm.OfferGridRow until 18 nov 2026.
    /// </summary>
    public class OfferRow
    {
        public int Id { get; set; }
        public bool Select { get; set; }
        public string SupplierCode { get; set; }
        public string SupplierPartNumber { get; set; }
        public string MatchConfidence { get; set; }
        public int OfferedQuantity { get; set; }
        public int AttritionQuantity { get; set; }
        public int SurplusQuantity { get; set; }
        public string Bundle { get; set; }
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; }
        public string Packaging { get; set; }
        public string Reel { get; set; }
        public decimal ReelingFee { get; set; }
        public decimal LandedCost { get; set; }
        public decimal ComparisonCost { get; set; }
        public int LeadTimeDays { get; set; }
        public int AvailableQuantity { get; set; }
        public DateTime DueDate { get; set; }
    }
}
