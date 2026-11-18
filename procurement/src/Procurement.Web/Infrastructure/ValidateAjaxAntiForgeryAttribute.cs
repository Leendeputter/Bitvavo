using System;
using System.Web.Helpers;
using System.Web.Mvc;

namespace Procurement.Web.Infrastructure
{
    /// <summary>
    /// Anti-forgery check for the grid's fetch() calls. Important with Windows authentication: the
    /// browser logs in automatically, so without this another web page could make your browser
    /// press "Source" here. The page puts a token in a meta tag (see _Layout), scripts send it as the
    /// "RequestVerificationToken" header, and this checks it against the anti-forgery cookie.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ValidateAjaxAntiForgeryAttribute : FilterAttribute, IAuthorizationFilter
    {
        public const string HeaderName = "RequestVerificationToken";

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            var request = filterContext.HttpContext.Request;
            if (string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)) return;

            var cookie = request.Cookies[AntiForgeryConfig.CookieName];
            var header = request.Headers[HeaderName] ?? request.Form["__RequestVerificationToken"];
            AntiForgery.Validate(cookie?.Value, header);
        }

        /// <summary>
        /// The form token for this page (meta tag and hidden form fields), generated once per request
        /// so every token on the page matches the one anti-forgery cookie. Use this instead of
        /// Html.AntiForgeryToken(), which would generate a second, different cookie.
        /// </summary>
        public static string TokenHeaderValue(System.Web.HttpContextBase context)
        {
            const string itemKey = "Procurement.AntiForgeryFormToken";
            if (context.Items[itemKey] is string cached) return cached;

            AntiForgery.GetTokens(context.Request.Cookies[AntiForgeryConfig.CookieName]?.Value, out var newCookieToken, out var formToken);
            if (newCookieToken != null)
                context.Response.Cookies.Add(new System.Web.HttpCookie(AntiForgeryConfig.CookieName, newCookieToken) { HttpOnly = true, Secure = context.Request.IsSecureConnection });
            context.Items[itemKey] = formToken;
            return formToken;
        }

        /// <summary>Hidden field for a normal (non-script) form post.</summary>
        public static System.Web.IHtmlString HiddenField(System.Web.HttpContextBase context) =>
            new System.Web.HtmlString("<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"" +
                System.Web.HttpUtility.HtmlAttributeEncode(TokenHeaderValue(context)) + "\" />");
    }
}
