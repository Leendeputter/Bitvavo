using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Procurement.Services.Approvals;
using Procurement.Web.Infrastructure;

namespace Procurement.Web.Controllers
{
    /// <summary>Approvals (spec §8.3), as the desktop's ApprovalForm; logic in <see cref="ApprovalScreenService"/>.</summary>
    public class ApprovalsController : ProcurementControllerBase
    {
        private ApprovalScreenService Screen => new ApprovalScreenService(Services);

        [HttpGet]
        public ActionResult Index() => View();

        [HttpGet]
        public async Task<ActionResult> List()
        {
            var rows = await Screen.GetPendingAsync();
            return new JsonNetResult(new { rows, status = $"{rows.Count} regel(s) wachten op goedkeuring." });
        }

        /// <summary>Approve or reject one or more selected lines with the same comment.</summary>
        [HttpPost]
        public async Task<ActionResult> Decide(List<int> approvalIds, bool approve, string comment)
        {
            var ids = (approvalIds ?? new List<int>()).Distinct().ToList();
            if (ids.Count == 0) return new JsonNetResult(new { error = "Selecteer eerst een of meer regels." }, 400);
            var status = await Screen.DecideManyAsync(ids, approve, comment);
            Log.Info("{0} {1} goedkeuring(en) {2}: {3}", CurrentSession.UserName, approve ? "keurde goed" : "wees af", string.Join(",", ids), status);
            return new JsonNetResult(new { status });
        }
    }
}
