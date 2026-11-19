using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Procurement.Services.Options
{
    /// <summary>One audit-log row, the columns of the desktop's AuditLogForm.</summary>
    public class AuditLogRow
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public string EventType { get; set; }
        public string SupplierCode { get; set; }
        public string Status { get; set; }
        public string UserOrSystem { get; set; }
        public string RequestPayload { get; set; }
        public string ResponsePayload { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Audit Log (spec §8.7) for the web (19 nov 2026), same query and columns as the desktop's AuditLogForm. The
    /// repository caps the result at the newest 1000 events.
    /// </summary>
    public class AuditLogScreenService
    {
        private readonly ProcurementServices _services;

        public AuditLogScreenService(ProcurementServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public async Task<IReadOnlyList<AuditLogRow>> SearchAsync(string entityType, string eventType, string supplierCode, DateTime? from, DateTime? to)
        {
            var events = await _services.ProcurementEventRepository.SearchAsync(
                entityType: Blank(entityType),
                eventType: Blank(eventType),
                supplierCode: Blank(supplierCode),
                from: from,
                to: to);

            return events.Select(e => new AuditLogRow
            {
                Id = e.Id,
                Timestamp = e.Timestamp,
                EntityType = e.EntityType,
                EntityId = e.EntityId,
                EventType = e.EventType,
                SupplierCode = e.SupplierCode,
                Status = e.Status,
                UserOrSystem = e.UserOrSystem,
                RequestPayload = e.RequestPayload,
                ResponsePayload = e.ResponsePayload,
                Error = e.Error
            }).ToList();
        }

        private static string Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
