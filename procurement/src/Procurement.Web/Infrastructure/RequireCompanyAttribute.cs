using System.Web.Mvc;
using System.Web.Routing;

namespace Procurement.Web.Infrastructure
{
    /// <summary>
    /// Windows authentication (IIS) already says who you are; this sends you to the company / test
    /// mode choice when you haven't made it yet in this browser session, like the desktop's login screen.
    /// </summary>
    public class RequireCompanyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (UserState.GetSession(filterContext.HttpContext.Session) != null) return;

            if (filterContext.HttpContext.Request.IsAjaxRequest())
            {
                filterContext.Result = new HttpStatusCodeResult(401, "Sessie verlopen, log opnieuw in.");
                return;
            }

            filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary
            {
                { "controller", "Account" },
                { "action", "Login" },
                { "returnUrl", filterContext.HttpContext.Request.RawUrl }
            });
        }
    }
}
