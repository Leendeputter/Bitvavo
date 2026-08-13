using System.Threading.Tasks;

namespace Procurement.Core.Interfaces
{
    /// <summary>Writes ProcurementEvent audit rows (spec §9). Implementations must mask/strip credentials and payment data before persisting.</summary>
    public interface IAuditLogger
    {
        Task LogAsync(
            string entityType,
            string entityId,
            string eventType,
            string status,
            string supplierCode = null,
            string requestPayload = null,
            string responsePayload = null,
            string error = null,
            string userOrSystem = "system");
    }
}
