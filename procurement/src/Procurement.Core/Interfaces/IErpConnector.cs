using System.Collections.Generic;
using System.Threading.Tasks;
using Procurement.Core.Entities;
using Procurement.Core.Models;

namespace Procurement.Core.Interfaces
{
    /// <summary>
    /// ERP-koppelvlak (spec §4). For this prototype the only implementation is
    /// <c>MockErpConnector</c>, reading/writing the prototype's own SQL Server database instead of
    /// real UniPro tables. A later UniProErpConnector implements this same interface.
    /// </summary>
    public interface IErpConnector
    {
        Task<IReadOnlyList<PurchaseRequest>> GetOpenPurchaseRequestsAsync();

        /// <summary>Returns the ErpPoNumber.</summary>
        Task<string> CreatePurchaseOrderAsync(PurchaseOrderDraft draft);

        Task UpdatePurchaseOrderStatusAsync(string erpPoNumber, string status);

        /// <summary>
        /// Applies a fetched supplier order-status/confirmation to a placed PO: local tracking
        /// fields always get updated (ConfirmedQuantity/ConfirmedUnitPrice per line, a
        /// PurchaseOrderDelivery for the estimated ship date). In real MAX mode this also writes the
        /// three MAX fields the business actually uses for this (spec, confirmed directly by the
        /// user rather than guessed): Purchase_Order_Code.CONFRM_16 (supplier's own order/
        /// confirmation number), Order_Master.ORDREF_10 (prefixed with "O "/"OP " — see
        /// MaxPurchaseOrderRepository.BuildConfirmedReference), and Order_Master.CURDUE_10 (updated
        /// to the confirmed ship date when it differs). Never throws for a single line's mismatch —
        /// deviations are reported back via the caller's own comparison against
        /// OrderConfirmationException, not by this method refusing to apply anything.
        /// </summary>
        Task ApplyOrderConfirmationAsync(PurchaseOrder order, SupplierOrderStatus supplierStatus);
    }
}
