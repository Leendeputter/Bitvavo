using System.Collections.Generic;
using System.Web.Mvc;
using Procurement.Core.Models;
using Procurement.Services.Options;
using Procurement.Web.Infrastructure;

namespace Procurement.Web.Controllers
{
    /// <summary>
    /// Beheer > App Secrets, as the desktop's AppSecretsForm; logic and the MGR User Security check
    /// in <see cref="AppSecretsScreenService"/> (checked on every request). Secret values only travel
    /// from the page to the server, never back. Off the local machine they are only accepted over
    /// HTTPS, so a value never crosses the network in plain text.
    /// </summary>
    public class AppSecretsController : ProcurementControllerBase
    {
        private AppSecretsScreenService Screen => new AppSecretsScreenService(Services);

        [HttpGet]
        public ActionResult Index()
        {
            if (!HasBeheerAccess) return new HttpStatusCodeResult(403, "Geen rechten voor App Secrets (MGR User Security).");
            ViewBag.SecretNames = Screen.SecretNames;
            ViewBag.InsecureConnection = !IsSafeConnection;
            return View();
        }

        [HttpGet]
        public ActionResult Data() => new JsonNetResult(new { secrets = Screen.GetStatuses(), access = Screen.GetAccess() });

        [HttpPost]
        public ActionResult SaveSecrets(List<SecretValue> values)
        {
            if (!IsSafeConnection)
                return new JsonNetResult(new { error = "Secrets worden alleen via HTTPS opgeslagen. Er is niets opgeslagen." }, 400);
            var count = Screen.SaveSecrets(values);
            Log.Info("{0} sloeg {1} secret(s) op", CurrentSession.UserName, count); // names and values are never logged
            // A new object graph on the next request, so this user's supplier clients use the new values.
            UserState.DisposeServices(Session);
            return new JsonNetResult(new
            {
                status = count + " secret(s) opgeslagen; lege velden zijn ongewijzigd gelaten. Andere ingelogde gebruikers gebruiken nieuwe waarden na opnieuw inloggen."
            });
        }

        [HttpPost]
        public ActionResult SaveAccess(List<SecretAccessEntry> entries)
        {
            Screen.SaveAccess(entries);
            Log.Info("{0} sloeg de rechten op secrets op", CurrentSession.UserName);
            return new JsonNetResult(new { status = "Rechten opgeslagen." });
        }

        /// <summary>Revokes one or more grants at once (Ctrl/Shift-select on the page).</summary>
        [HttpPost]
        public ActionResult DeleteAccess(List<SecretAccessEntry> entries)
        {
            var count = 0;
            foreach (var entry in entries ?? new List<SecretAccessEntry>())
            {
                if (string.IsNullOrWhiteSpace(entry?.SecretName) || string.IsNullOrWhiteSpace(entry.UserName)) continue;
                Screen.DeleteAccess(entry.SecretName, entry.UserName);
                Log.Info("{0} trok het recht op {1} in voor {2}", CurrentSession.UserName, entry.SecretName, entry.UserName);
                count++;
            }
            return new JsonNetResult(new { status = count == 1 ? "Recht ingetrokken." : count + " rechten ingetrokken." });
        }

        [HttpPost]
        public ActionResult CopyAccess(string fromUser, string toUsers)
        {
            var count = Screen.CopyAccess(fromUser, toUsers);
            Log.Info("{0} kopieerde de rechten van {1} naar {2}", CurrentSession.UserName, fromUser, toUsers);
            return new JsonNetResult(new
            {
                status = count > 0 ? $"Rechten van '{fromUser}' gekopieerd naar {count} gebruiker(s)." : $"Geen rechten gevonden voor '{fromUser}' om te kopiëren."
            });
        }

        private bool IsSafeConnection => Request.IsSecureConnection || Request.IsLocal;
    }
}
