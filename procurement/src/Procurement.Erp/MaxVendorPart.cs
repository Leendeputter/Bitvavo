namespace Procurement.Erp
{
    /// <summary>
    /// One row from the user-supplied MAX Part_Vendor query — a known cross-reference between an
    /// internal article number and a supplier's own part code, maintained directly in MAX. Used to
    /// seed SupplierProductMapping with Verified confidence instead of relying on fuzzy matching
    /// (see MaxErpConnector.SyncVendorPartMappingsAsync).
    /// </summary>
    public class MaxVendorPart
    {
        /// <summary>Part_Vendor.PRTNUM_07 — our internal article number, maps to PurchaseRequestLine/SupplierProductMapping.ErpArticleId.</summary>
        public string PartId { get; set; }

        /// <summary>Part_Vendor.VENID_07 — MAX's own vendor code, e.g. '0349' for Farnell or '10194' for DigiKey. Resolved to a SupplierCode via Supplier.VendorId.</summary>
        public string VendorId { get; set; }

        /// <summary>Part_Vendor.VENPRT_07 — the supplier's own part number for this article.</summary>
        public string VendorPart { get; set; }
    }
}
