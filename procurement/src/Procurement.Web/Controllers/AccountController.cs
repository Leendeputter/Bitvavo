using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NLog;
using Procurement.Services.Login;
using Procurement.Web.Infrastructure;
using Procurement.Web.Models;

namespace Procurement.Web.Controllers
{
    /// <summary>
    /// The web version of the desktop's login screen: who you are comes from Windows (IIS Windows
    /// authentication, the same account the desktop uses), and here you pick the MAX company and
    /// test mode. The last choice is remembered in a cookie.
    /// </summary>
    [ValidateAjaxAntiForgery]
    public class AccountController : Controller
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private const string LastChoiceCookie = "Procurement.LastCompany";

        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            var model = new LoginViewModel { UserName = WindowsUserName(), ReturnUrl = returnUrl };
            if (!FillCompanies(model)) return View(model);

            var last = Request.Cookies[LastChoiceCookie];
            if (last != null && long.TryParse(last["company"], out var lastCompany)) model.CompanyId = lastCompany;
            model.TestMode = last != null && last["test"] == "1";
            return View(model);
        }

        [HttpPost]
        public ActionResult Login(LoginViewModel posted)
        {
            var model = new LoginViewModel
            {
                UserName = WindowsUserName(),
                CompanyId = posted.CompanyId,
                TestMode = posted.TestMode,
                ReturnUrl = posted.ReturnUrl
            };
            if (!FillCompanies(model)) return View(model);

            var company = model.Companies.FirstOrDefault(c => c.CompanyID == posted.CompanyId);
            if (company == null)
            {
                model.Error = "Selecteer eerst een administratie.";
                return View(model);
            }
            if (string.IsNullOrEmpty(model.UserName))
            {
                model.Error = "Je Windows-account is niet bekend. Windows Authentication moet aan staan in IIS (en anoniem uit).";
                return View(model);
            }

            try
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                Log.Info("Inloggen: {0} bij {1}{2}...", model.UserName, company.CompanyName, model.TestMode ? " (testmodus)" : string.Empty);
                var primary = PrimaryConnectionString();
                var session = WithTimeout("verbinding met de administratie (MAX-licentie)", () => MaxLogin.CreateSession(
                    WebAppSettings.LicensePath, primary, model.UserName, company, model.TestMode,
                    WebAppSettings.LogPath, WebAppSettings.LogFile));
                Log.Info("Inloggen: verbinding met {0} ({1} ms)", company.CompanyName, clock.ElapsedMilliseconds);
                UserState.SignIn(Session, session);
                Log.Info("{0} ingelogd op {1}{2} ({3} ms)", model.UserName, company.CompanyName, model.TestMode ? " (testmodus)" : string.Empty, clock.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Inloggen van {0} bij {1} mislukt", model.UserName, company.CompanyName);
                model.Error = $"Inloggen bij '{company.CompanyName}' is mislukt: {ex.Message}";
                return View(model);
            }

            var cookie = new HttpCookie(LastChoiceCookie) { Expires = DateTime.Now.AddYears(1), HttpOnly = true };
            cookie["company"] = company.CompanyID.ToString();
            cookie["test"] = model.TestMode ? "1" : "0";
            Response.Cookies.Add(cookie);

            return Url.IsLocalUrl(model.ReturnUrl) ? (ActionResult)Redirect(model.ReturnUrl) : RedirectToAction("Index", "PurchaseRequests");
        }

        [HttpPost]
        public ActionResult Logout()
        {
            UserState.SignOut(Session);
            return RedirectToAction("Login");
        }

        private bool FillCompanies(LoginViewModel model)
        {
            try
            {
                var primary = PrimaryConnectionString();
                model.Companies = WithTimeout("lijst met administraties", () => MaxLogin.GetCompanies(primary));
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Geen verbinding met MAX");
                model.Error = $"Kan geen verbinding maken met MAX: {ex.Message}";
                return false;
            }
        }

        // The primary connection only depends on the licence, so it's looked up once per app start.
        // Lazy instead of a lock around the call (24 nov 2026, Leen: login regularly kept spinning):
        // a lookup that hangs no longer blocks every later login behind the lock; a failed one is
        // tried again next time.
        private static Lazy<string> _primaryConnectionString = NewPrimaryLookup();

        private static Lazy<string> NewPrimaryLookup() => new Lazy<string>(() =>
            WithTimeout("MAX-licentie lezen", () => MaxLogin.GetPrimaryConnectionString(WebAppSettings.LicensePath)));

        private static string PrimaryConnectionString()
        {
            var lookup = _primaryConnectionString;
            try
            {
                return lookup.Value;
            }
            catch
            {
                System.Threading.Interlocked.CompareExchange(ref _primaryConnectionString, NewPrimaryLookup(), lookup);
                throw;
            }
        }

        private static readonly TimeSpan MaxCallTimeout = TimeSpan.FromSeconds(45);

        /// <summary>
        /// A MAX call that doesn't answer within 45 s gives a message instead of a page that keeps
        /// loading (24 nov 2026). The call itself can't be stopped and finishes in the background.
        /// </summary>
        private static T WithTimeout<T>(string step, Func<T> call)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var task = System.Threading.Tasks.Task.Run(call);
            try
            {
                if (!task.Wait(MaxCallTimeout))
                {
                    Log.Error("Inloggen: {0} gaf na {1} s geen antwoord", step, MaxCallTimeout.TotalSeconds);
                    throw new TimeoutException($"MAX gaf geen antwoord bij {step} (na {MaxCallTimeout.TotalSeconds:0} s). Probeer het opnieuw; blijft het hangen, herstart dan de web-app.");
                }
            }
            catch (AggregateException ex) when (ex.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
            Log.Info("Inloggen: {0} ({1} ms)", step, clock.ElapsedMilliseconds);
            return task.Result;
        }

        private string WindowsUserName() => MaxLogin.ShortUserName(User?.Identity?.Name);
    }
}
