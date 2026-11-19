using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Procurement.Services.Options;
using Procurement.Web.Infrastructure;

namespace Procurement.Web.Controllers
{
    /// <summary>Audit Log (Options menu), read-only, as the desktop's AuditLogForm; logic in <see cref="AuditLogScreenService"/>.</summary>
    public class AuditLogController : ProcurementControllerBase
    {
        [HttpGet]
        public ActionResult Index() => View();

        [HttpGet]
        public async Task<ActionResult> List(string entityType, string eventType, string supplier, DateTime? from, DateTime? to)
        {
            var rows = await new AuditLogScreenService(Services).SearchAsync(entityType, eventType, supplier, from, to);
            var status = rows.Count >= 1000
                ? "1000 gebeurtenissen getoond (de nieuwste; maak het filter kleiner voor oudere)."
                : $"{rows.Count} gebeurtenis(sen) getoond.";
            return new JsonNetResult(new { rows, status });
        }
    }
}
