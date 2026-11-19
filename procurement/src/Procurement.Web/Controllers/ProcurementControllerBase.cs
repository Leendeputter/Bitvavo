using System;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Web.Mvc;
using NLog;
using Procurement.Core.Session;
using Procurement.Services;
using Procurement.Web.Infrastructure;

namespace Procurement.Web.Controllers
{
    /// <summary>Base for every screen after login: the user's session and object graph, and errors as JSON for the grids.</summary>
    [RequireCompany]
    [ValidateAjaxAntiForgery]
    public abstract class ProcurementControllerBase : Controller
    {
        protected static readonly Logger Log = LogManager.GetCurrentClassLogger();

        protected UserSessionContext CurrentSession => UserState.GetSession(Session);
        protected ProcurementServices Services => UserState.GetServices(Session);

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            var session = CurrentSession;
            if (session == null) return;
            ViewBag.UserName = session.UserName;
            ViewBag.CompanyName = session.CompanyName;
            ViewBag.TestMode = session.TestMode;
            ViewBag.OrderingEnabled = WebAppSettings.OrderingEnabled;
            ViewBag.HasBeheerAccess = HasBeheerAccess;
        }

        /// <summary>
        /// MGR User Security, which the Beheer menu needs (as on the desktop). Looked up once per
        /// login and kept in the session; it only hides the menu, App Secrets checks again itself.
        /// </summary>
        protected bool HasBeheerAccess
        {
            get
            {
                if (Session["HasBeheerAccess"] is bool cached) return cached;
                var services = Services;
                var hasAccess = services != null && Procurement.Services.Options.AppSecretsScreenService.HasAccess(services);
                Session["HasBeheerAccess"] = hasAccess;
                return hasAccess;
            }
        }

        /// <summary>
        /// One property of the posted JSON body, read with Json.NET. Used for the Settings rows:
        /// MVC's own JSON binding can't put a whole number into a decimal property and would read
        /// decimals with the server's culture, so "0.5" could become 5.
        /// </summary>
        protected T FromBody<T>(string property)
        {
            var stream = Request.InputStream;
            stream.Position = 0;
            using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 4096, leaveOpen: true))
            {
                var body = JObject.Parse(reader.ReadToEnd());
                var token = body[property];
                return token == null || token.Type == JTokenType.Null ? default(T) : token.ToObject<T>();
            }
        }

        /// <summary>A failed grid call shows its message in the status line instead of a broken page, like the desktop's message box.</summary>
        protected override void OnException(ExceptionContext filterContext)
        {
            Log.Error(filterContext.Exception, "Fout bij {0} voor {1}", filterContext.HttpContext.Request.RawUrl, CurrentSession?.UserName);
            if (!filterContext.HttpContext.Request.IsAjaxRequest()) return;

            filterContext.Result = new JsonNetResult(new { error = MessageOf(filterContext.Exception) }, 500);
            filterContext.ExceptionHandled = true;
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
        }

        private static string MessageOf(Exception exception)
        {
            while (exception is AggregateException && exception.InnerException != null) exception = exception.InnerException;
            return exception.Message;
        }
    }
}
