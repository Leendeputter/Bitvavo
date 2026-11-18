using System;
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
