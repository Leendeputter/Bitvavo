using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Procurement.Services.PurchaseRequests;
using Procurement.Web.Infrastructure;
using Procurement.Web.Models;

namespace Procurement.Web.Controllers
{
    /// <summary>
    /// Purchase Requests (spec §8.1), the main screen, same as the desktop's MainForm: filters, the
    /// requests grid, the offers of the selected request, sourcing, choosing an offer and changing
    /// the quantity. All logic is the shared <see cref="PurchaseRequestScreenService"/>; this only
    /// turns it into JSON for the page. Placing orders follows later (Ordering is off for now anyway).
    /// </summary>
    public class PurchaseRequestsController : ProcurementControllerBase
    {
        private PurchaseRequestScreenService Screen => new PurchaseRequestScreenService(Services);

        [HttpGet]
        public ActionResult Index() => View();

        /// <summary>Only what's already synced locally; never touches MAX.</summary>
        [HttpPost]
        public async Task<ActionResult> Load(RequestFilterModel filter)
        {
            var result = await Screen.LoadLocalAsync((filter ?? new RequestFilterModel()).ToFilter());
            return new JsonNetResult(new { rows = result.Rows, status = result.StatusText });
        }

        /// <summary>"Query": fetch open orders from MAX and sync them.</summary>
        [HttpPost]
        public async Task<ActionResult> Query(RequestFilterModel filter)
        {
            var result = await Screen.QueryMaxAsync((filter ?? new RequestFilterModel()).ToFilter());
            return new JsonNetResult(new { rows = result.Rows, status = result.StatusText });
        }

        [HttpGet]
        public async Task<ActionResult> Offers(int requestId)
        {
            var result = await Screen.GetOffersAsync(requestId);
            return new JsonNetResult(new { lineId = result.LineId, rows = result.Rows });
        }

        [HttpPost]
        public async Task<ActionResult> Source(List<int> requestIds)
        {
            var ids = (requestIds ?? new List<int>()).Distinct().ToList();
            if (ids.Count == 0) return new JsonNetResult(new { error = "Selecteer eerst een of meer aanvragen." }, 400);
            var summary = await Screen.SourceRequestsAsync(ids);
            Log.Info("{0} sourcete {1} aanvraag/aanvragen: {2}", CurrentSession.UserName, ids.Count, summary);
            return new JsonNetResult(new { status = summary.ToString() });
        }

        [HttpPost]
        public async Task<ActionResult> Select(int lineId, int offerId, bool selected)
        {
            await Screen.SetOfferSelectedAsync(lineId, offerId, selected);
            return new JsonNetResult(new { ok = true });
        }

        [HttpPost]
        public async Task<ActionResult> Quantity(int lineId, int quantity)
        {
            if (quantity <= 0) return new JsonNetResult(new { error = "Het aantal moet groter dan 0 zijn." }, 400);
            await Screen.ChangeQuantityAsync(lineId, quantity);
            return new JsonNetResult(new { status = $"Opnieuw gesourced voor {quantity:N0} stuks." });
        }
    }
}
