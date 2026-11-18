using System.Configuration;
using Procurement.Services;
using Procurement.Core.Session;

namespace Procurement.UI.Composition
{
    /// <summary>
    /// The desktop's object graph: <see cref="ProcurementServices"/> (shared with the web app since
    /// 18 nov 2026) for the process-wide login that LoginForm filled in, with "Ordering.Enabled"
    /// from App.config. Built once per run, after login (see Program.cs).
    /// </summary>
    public class CompositionRoot : ProcurementServices
    {
        public CompositionRoot()
            : base(new ProcurementSessionContext(),
                   ReadOrderingEnabled(ConfigurationManager.AppSettings["Ordering.Enabled"]))
        {
        }
    }
}
