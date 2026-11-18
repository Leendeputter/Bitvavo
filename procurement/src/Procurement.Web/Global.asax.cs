using System;
using System.Data.Entity;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using NLog;
using Procurement.Data;
using Procurement.Services.Login;
using Procurement.Web.Infrastructure;

namespace Procurement.Web
{
    public class MvcApplication : HttpApplication
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        protected void Application_Start()
        {
            // Same as the desktop: the schema is managed by the upgrade script, EF never creates or
            // migrates anything. Without this EF6 would try its default initializer on first use.
            Database.SetInitializer<ProcurementDbContext>(null);

            // MaxSQL needs MAX's own log manager before the first call (see MaxLogin).
            MaxLogin.EnsureMaxLogging(WebAppSettings.LogFile, WebAppSettings.LogPath, WebAppSettings.DebugLogging);

            ConfigureLogging();

            GlobalFilters.Filters.Add(new HandleErrorAttribute());
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }

        /// <summary>The web app's own log file, in the same folder as MAX's log (Logging.Path in Web.config).</summary>
        private static void ConfigureLogging()
        {
            var config = new NLog.Config.LoggingConfiguration();
            var file = new NLog.Targets.FileTarget("file")
            {
                FileName = System.IO.Path.Combine(WebAppSettings.LogPath, "Procurement.Web.${shortdate}.log"),
                Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:|${exception:format=tostring}}",
                MaxArchiveFiles = 30
            };
            config.AddRule(WebAppSettings.DebugLogging ? LogLevel.Debug : LogLevel.Info, LogLevel.Fatal, file);
            LogManager.Configuration = config;
        }

        protected void Application_Error()
        {
            var exception = Server.GetLastError();
            if (exception != null) Log.Error(exception, "Onverwachte fout in de web-app");
        }

        protected void Session_End()
        {
            // The per-user object graph holds an EF context; release it when the session expires.
            UserState.DisposeServices(Session);
        }
    }
}
