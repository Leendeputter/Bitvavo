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
                var session = MaxLogin.CreateSession(
                    WebAppSettings.LicensePath, PrimaryConnectionString(), model.UserName, company, model.TestMode,
                    WebAppSettings.LogPath, WebAppSettings.LogFile);
                UserState.SignIn(Session, session);
                Log.Info("{0} ingelogd op {1}{2}", model.UserName, company.CompanyName, model.TestMode ? " (testmodus)" : string.Empty);
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
                model.Companies = MaxLogin.GetCompanies(PrimaryConnectionString());
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
        private static readonly object PrimaryLock = new object();
        private static string _primaryConnectionString;

        private static string PrimaryConnectionString()
        {
            lock (PrimaryLock)
            {
                return _primaryConnectionString ??
                       (_primaryConnectionString = MaxLogin.GetPrimaryConnectionString(WebAppSettings.LicensePath));
            }
        }

        private string WindowsUserName() => MaxLogin.ShortUserName(User?.Identity?.Name);
    }
}
