using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Procurement.Core.Entities;
using Procurement.Core.Interfaces;

namespace Procurement.Data.Repositories
{
    /// <summary>
    /// DB-backed implementation of IAuditLogger (spec §9). Masks obvious credential/secret
    /// key-value pairs before persisting — RequestPayload/ResponsePayload must never contain
    /// plaintext secrets, even in mock mode.
    /// </summary>
    public class DbAuditLogger : IAuditLogger
    {
        private static readonly Regex SecretPattern = new Regex(
            "(?i)(\"?(api[_-]?key|secret|password|token|client[_-]?secret|authorization)\"?\\s*[:=]\\s*\"?)([^\",}\\s]+)",
            RegexOptions.Compiled);

        private readonly ProcurementDbContext _context;

        public DbAuditLogger(ProcurementDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(
            string entityType,
            string entityId,
            string eventType,
            string status,
            string supplierCode = null,
            string requestPayload = null,
            string responsePayload = null,
            string error = null,
            string userOrSystem = "system")
        {
            var evt = new ProcurementEvent
            {
                EntityType = entityType,
                EntityId = entityId,
                EventType = eventType,
                Status = status,
                SupplierCode = supplierCode,
                RequestPayload = Mask(requestPayload),
                ResponsePayload = Mask(responsePayload),
                Error = error,
                UserOrSystem = userOrSystem
            };

            _context.ProcurementEvents.Add(evt);
            await _context.SaveChangesAsync();
        }

        private static string Mask(string payload)
        {
            return string.IsNullOrEmpty(payload) ? payload : SecretPattern.Replace(payload, "$1***MASKED***");
        }
    }
}
