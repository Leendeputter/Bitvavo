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

        private const string AppTargetName = "procurementWebFile";
        private static NLog.Targets.FileTarget _appTarget;
        private static bool _restoring;

        /// <summary>
        /// The web app's own log file, in the same folder as MAX's log (Logging.Path in Web.config).
        /// MAX's library replaces NLog's global configuration with its own when it (re)creates its log
        /// manager (19 nov 2026: after the MGR rights check was added, the web app's lines stopped
        /// appearing). So our file target is added to whatever configuration is active, now and every
        /// time it is swapped, instead of being set once and silently lost.
        /// </summary>
        private static void ConfigureLogging()
        {
            _appTarget = new NLog.Targets.FileTarget(AppTargetName)
            {
                FileName = System.IO.Path.Combine(WebAppSettings.LogPath, "Procurement.Web.${shortdate}.log"),
                Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:|${exception:format=tostring}}",
                MaxArchiveFiles = 30
            };

            var config = LogManager.Configuration ?? new NLog.Config.LoggingConfiguration();
            AddAppTarget(config);
            LogManager.Configuration = config;
            LogManager.ConfigurationChanged += (sender, e) =>
            {
                if (_restoring || e.ActivatedConfiguration == null) return;
                if (e.ActivatedConfiguration.FindTargetByName(AppTargetName) != null) return;
                _restoring = true;
                try
                {
                    AddAppTarget(e.ActivatedConfiguration);
                    LogManager.ReconfigExistingLoggers();
                    Log.Info("Logconfiguratie was vervangen (door MAX); Procurement-log opnieuw gekoppeld.");
                }
                finally
                {
                    _restoring = false;
                }
            };
        }

        private static void AddAppTarget(NLog.Config.LoggingConfiguration config)
        {
            config.AddTarget(_appTarget);
            // Only our own loggers (Procurement.*), so MAX's own lines stay in MAX's log.
            config.AddRule(WebAppSettings.DebugLogging ? LogLevel.Debug : LogLevel.Info, LogLevel.Fatal, _appTarget, "Procurement.*");
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
