using System.Configuration;
using Procurement.Services;

namespace Procurement.Web.Infrastructure
{
    /// <summary>appSettings from Web.config. Same keys as the desktop's App.config where they overlap.</summary>
    public static class WebAppSettings
    {
        /// <summary>MAX licence folder (ExactRM.lic). The AppPool account needs read access here, like MAX Anywhere's.</summary>
        public static string LicensePath =>
            ConfigurationManager.AppSettings["Max.LicensePath"] ?? @"\\192.168.0.12\Exact Max\RMServer\LIC";

        public static string LogPath => ConfigurationManager.AppSettings["Logging.Path"] ?? @"C:\Unitron\Procurement\Log";
        public static string LogFile => ConfigurationManager.AppSettings["Logging.FileName"] ?? "Procurement.Web.Max.log";
        public static bool DebugLogging => bool.TryParse(ConfigurationManager.AppSettings["Logging.Debug"], out var debug) && debug;

        /// <summary>Off unless explicitly true, like the desktop.</summary>
        public static bool OrderingEnabled => ProcurementServices.ReadOrderingEnabled(ConfigurationManager.AppSettings["Ordering.Enabled"]);
    }
}
