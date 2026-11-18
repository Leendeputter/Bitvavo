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

        [HttpPost]
        public async Task<ActionResult> Decide(int approvalId, bool approve, string comment)
        {
            await Screen.DecideAsync(approvalId, approve, comment);
            Log.Info("{0} {1} goedkeuring {2}", CurrentSession.UserName, approve ? "keurde goed" : "wees af", approvalId);
            return new JsonNetResult(new { ok = true });
        }
    }
}
