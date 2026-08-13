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
    }
}
