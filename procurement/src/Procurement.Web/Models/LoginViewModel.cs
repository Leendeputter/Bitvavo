using System.Collections.Generic;
using Procurement.Core.Models;

namespace Procurement.Web.Models
{
    public class LoginViewModel
    {
        public string UserName { get; set; }
        public IReadOnlyList<CompanyInfo> Companies { get; set; } = new List<CompanyInfo>();
        public long? CompanyId { get; set; }
        public bool TestMode { get; set; }
        public string Error { get; set; }
        public string ReturnUrl { get; set; }
    }
}
