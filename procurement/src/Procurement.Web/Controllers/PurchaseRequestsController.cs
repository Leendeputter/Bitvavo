using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Procurement.Services.Orders;
using Procurement.Services.PurchaseRequests;
using Procurement.Web.Infrastructure;
using Procurement.Web.Models;

namespace Procurement.Web.Controllers
{
    /// <summary>
    /// Purchase Requests (spec §8.1), the main screen, same as the desktop's MainForm: filters, the
    /// requests grid, the offers of the selected request, sourcing, choosing an offer and changing
    /// the quantity. All logic is the shared <see cref="PurchaseRequestScreenService"/>; this only
    /// turns it into JSON for the page. Placing orders and processing confirmations use the shared <see cref="OrderPlacementService"/> (19 nov 2026).
    /// </summary>
    public class PurchaseRequestsController : ProcurementControllerBase
    {
        private PurchaseRequestScreenService Screen => new PurchaseRequestScreenService(Services);
        private OrderPlacementService Placement => new OrderPlacementService(Services);

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

        /// <summary>
        /// Step 1 of "Place Order": the Farnell addresses to pick from. Empty when no picker is needed.
        /// Reads only; nothing is ordered yet.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult> ShipToChoices(List<int> requestIds)
        {
            if (!Placement.OrderingEnabled)
                return new JsonNetResult(new { error = "Bestellen staat uit (Ordering.Enabled = false). Er is geen order geplaatst." }, 400);
            var ids = (requestIds ?? new List<int>()).Distinct().ToList();
            if (ids.Count == 0) return new JsonNetResult(new { error = "Selecteer eerst een of meer aanvragen." }, 400);
            return new JsonNetResult(new { addresses = await Placement.GetShipToChoicesAsync(ids) });
        }

        /// <summary>Step 2 of "Place Order": places the orders, one PO per supplier.</summary>
        [HttpPost]
        public async Task<ActionResult> PlaceOrders(List<int> requestIds, string shipToAddressId)
        {
            var ids = (requestIds ?? new List<int>()).Distinct().ToList();
            if (ids.Count == 0) return new JsonNetResult(new { error = "Selecteer eerst een of meer aanvragen." }, 400);
            Log.Info("{0} plaatst orders voor {1} aanvraag/aanvragen", CurrentSession.UserName, ids.Count);
            await Placement.PlaceOrdersAsync(ids, shipToAddressId);
            return new JsonNetResult(new { status = "Order(s) geplaatst." });
        }

        [HttpPost]
        public async Task<ActionResult> ProcessConfirmations()
        {
            var summary = await Placement.ProcessOrderConfirmationsAsync();
            Log.Info("{0}: {1}", CurrentSession.UserName, summary.StatusText);
            return new JsonNetResult(new { status = summary.StatusText, message = summary.MessageText, exceptions = summary.Exceptions });
        }
    }
}
