using System.Threading.Tasks;
using System.Web.Mvc;
using Procurement.Services.Options;
using Procurement.Web.Infrastructure;

namespace Procurement.Web.Controllers
{
    /// <summary>Supplier Mapping (Options menu), as the desktop's SupplierMappingForm; logic in <see cref="SupplierMappingScreenService"/>.</summary>
    public class SupplierMappingController : ProcurementControllerBase
    {
        private SupplierMappingScreenService Screen => new SupplierMappingScreenService(Services);

        [HttpGet]
        public ActionResult Index()
        {
            ViewBag.FilterChoices = SupplierMappingScreenService.FilterChoices;
            return View();
        }

        [HttpGet]
        public async Task<ActionResult> List(string confidence)
        {
            var rows = await Screen.GetMappingsAsync(confidence);
            return new JsonNetResult(new { rows, status = $"{rows.Count} mapping(s) getoond." });
        }

        [HttpPost]
        public async Task<ActionResult> Verify(int mappingId)
        {
            await Screen.MarkVerifiedAsync(mappingId);
            Log.Info("{0} zette mapping {1} op Verified", CurrentSession.UserName, mappingId);
            return new JsonNetResult(new { ok = true });
        }

        /// <summary>Another order code for a mapping (24 nov 2026).</summary>
        [HttpPost]
        public async Task<ActionResult> ChangePartNumber(int mappingId, string supplierPartNumber)
        {
            await Screen.ChangePartNumberAsync(mappingId, supplierPartNumber);
            Log.Info("{0} zette mapping {1} op ordercode {2}", CurrentSession.UserName, mappingId, supplierPartNumber);
            return new JsonNetResult(new { ok = true });
        }
    }
}
