using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Mvc;
using Procurement.Services.Options;
using Procurement.Web.Infrastructure;

namespace Procurement.Web.Controllers
{
    /// <summary>Settings (Options menu), as the desktop's SettingsForm; logic in <see cref="SettingsScreenService"/>.</summary>
    public class SettingsController : ProcurementControllerBase
    {
        private const string DigiKeyStateKey = "DigiKeyAuthorizeState";

        private SettingsScreenService Screen => new SettingsScreenService(Services);

        [HttpGet]
        public ActionResult Index() => View();

        [HttpGet]
        public async Task<ActionResult> Data() => new JsonNetResult(await Screen.GetAsync());

        [HttpPost]
        public async Task<ActionResult> SaveSuppliers()
        {
            var rows = FromBody<List<SupplierSettingsRow>>("rows");
            await Screen.SaveSuppliersAsync(rows);
            Log.Info("{0} sloeg de supplier-instellingen op", CurrentSession.UserName);
            return new JsonNetResult(new { status = "Suppliers opgeslagen." });
        }

        [HttpGet]
        public async Task<ActionResult> Shipping(string supplierCode) =>
            new JsonNetResult(await Screen.GetShippingDetailsAsync(supplierCode));

        [HttpPost]
        public async Task<ActionResult> SaveShipping()
        {
            var details = FromBody<ShippingDetails>("details");
            await Screen.SaveShippingDetailsAsync(details);
            Log.Info("{0} sloeg de verzendgegevens van supplier {1} op", CurrentSession.UserName, details.SupplierCode);
            return new JsonNetResult(new { status = "Verzendgegevens opgeslagen." });
        }

        [HttpPost]
        public async Task<ActionResult> SavePreferences()
        {
            var rows = FromBody<List<SupplierPreferenceRow>>("rows");
            await Screen.SaveSupplierPreferencesAsync(rows);
            Log.Info("{0} sloeg de supplier preferences op", CurrentSession.UserName);
            return new JsonNetResult(new { status = "Supplier preferences opgeslagen." });
        }

        [HttpPost]
        public async Task<ActionResult> SavePackaging()
        {
            var rows = FromBody<List<PackagingPolicyRow>>("rows");
            await Screen.SavePackagingPoliciesAsync(rows);
            Log.Info("{0} sloeg de packaging policies op", CurrentSession.UserName);
            return new JsonNetResult(new { status = "Packaging policies opgeslagen." });
        }

        [HttpPost]
        public async Task<ActionResult> SaveAttrition()
        {
            var rows = FromBody<List<AttritionRuleRow>>("rows");
            await Screen.SaveAttritionRulesAsync(rows);
            Log.Info("{0} sloeg de uitvalregels op", CurrentSession.UserName);
            return new JsonNetResult(new { status = "Extra voor uitval opgeslagen." });
        }

        [HttpPost]
        public async Task<ActionResult> SaveApproval()
        {
            var policy = FromBody<ApprovalPolicyModel>("policy");
            await Screen.SaveApprovalPolicyAsync(policy);
            Log.Info("{0} sloeg de approval policy op", CurrentSession.UserName);
            return new JsonNetResult(new { status = "Approval policy opgeslagen." });
        }

        /// <summary>DigiKey "Authorize Ordering", step 1: the consent page to open. The state stays in the session.</summary>
        [HttpPost]
        public async Task<ActionResult> DigiKeyStart()
        {
            var authorization = await Screen.StartDigiKeyAuthorizationAsync();
            Session[DigiKeyStateKey] = authorization.State;
            return new JsonNetResult(new { url = authorization.AuthorizeUrl });
        }

        /// <summary>Step 2: the pasted redirect address; the refresh token goes straight to SharedSecrets, never to the page.</summary>
        [HttpPost]
        public async Task<ActionResult> DigiKeyFinish(string pastedUrl)
        {
            var state = Session[DigiKeyStateKey] as string;
            if (string.IsNullOrEmpty(state))
                return new JsonNetResult(new { error = "Start de autorisatie eerst opnieuw met \"Authorize Ordering...\"." }, 400);
            Session.Remove(DigiKeyStateKey);
            await Screen.FinishDigiKeyAuthorizationAsync(pastedUrl, state);
            Log.Info("{0} autoriseerde DigiKey Ordering", CurrentSession.UserName);
            // A new object graph on the next request, so this user's DigiKey client starts from the new token.
            UserState.DisposeServices(Session);
            return new JsonNetResult(new { status = "DigiKey Ordering is geautoriseerd. Andere ingelogde gebruikers gebruiken de nieuwe autorisatie na opnieuw inloggen." });
        }
    }
}
