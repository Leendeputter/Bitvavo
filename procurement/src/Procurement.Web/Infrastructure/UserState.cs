using System;
using System.Threading.Tasks;
using System.Web;
using System.Web.SessionState;
using Procurement.Core.Session;
using Procurement.Services;

namespace Procurement.Web.Infrastructure
{
    /// <summary>
    /// What one logged-in browser session holds: the chosen company/test mode (as a
    /// <see cref="UserSessionContext"/>) and that user's <see cref="ProcurementServices"/>. One object
    /// graph per user session, the web equivalent of the desktop's one per run, so EF change tracking
    /// and the supplier adapters' token caches work the same way. ASP.NET runs requests of one session
    /// one after another, so the graph is never used by two requests at once.
    /// </summary>
    public static class UserState
    {
        private const string SessionKey = "Procurement.Session";
        private const string ServicesKey = "Procurement.Services";

        public static UserSessionContext GetSession(HttpSessionStateBase session) =>
            session?[SessionKey] as UserSessionContext;

        public static void SignIn(HttpSessionStateBase session, UserSessionContext context)
        {
            DisposeServices(session);
            session[SessionKey] = context ?? throw new ArgumentNullException(nameof(context));
        }

        public static void SignOut(HttpSessionStateBase session)
        {
            DisposeServices(session);
            session.Remove(SessionKey);
        }

        /// <summary>The user's object graph, built on first use after login.</summary>
        public static ProcurementServices GetServices(HttpSessionStateBase session)
        {
            var context = GetSession(session) ?? throw new InvalidOperationException("Niet ingelogd.");
            if (session[ServicesKey] is ProcurementServices existing) return existing;
            // Built on a pool thread: the constructor blocks on an async database read (fine on the
            // desktop), which would deadlock on ASP.NET's request context.
            var services = Task.Run(() => new ProcurementServices(context, WebAppSettings.OrderingEnabled)).GetAwaiter().GetResult();
            session[ServicesKey] = services;
            return services;
        }

        public static void DisposeServices(HttpSessionStateBase session)
        {
            if (session?[ServicesKey] is IDisposable services)
            {
                session.Remove(ServicesKey);
                services.Dispose();
            }
        }

        /// <summary>Overload for Global.asax's Session_End, which gets the non-"Base" session type.</summary>
        public static void DisposeServices(HttpSessionState session) =>
            DisposeServices(session == null ? null : new HttpSessionStateWrapper(session));
    }
}
