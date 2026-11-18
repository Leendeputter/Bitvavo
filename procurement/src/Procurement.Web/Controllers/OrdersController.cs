using System.Threading.Tasks;
using System.Web.Mvc;
using Procurement.Services.Orders;
using Procurement.Web.Infrastructure;

namespace Procurement.Web.Controllers
{
    /// <summary>Purchase Orders register, read-only, as the desktop's OrdersForm; logic in <see cref="OrdersScreenService"/>.</summary>
    public class OrdersController : ProcurementControllerBase
    {
        private OrdersScreenService Screen => new OrdersScreenService(Services);

        [HttpGet]
        public ActionResult Index() => View();

        [HttpGet]
        public async Task<ActionResult> List(bool includeDeleted = false)
        {
            var result = await Screen.GetOrdersAsync(includeDeleted);
            return new JsonNetResult(new { rows = result.Rows, status = result.StatusText });
        }

        [HttpGet]
        public async Task<ActionResult> Lines(int orderId, bool includeCompleted = false)
        {
            var result = await Screen.GetLinesAsync(orderId, includeCompleted);
            return new JsonNetResult(new { rows = result.Rows, status = result.StatusText });
        }
    }
}
