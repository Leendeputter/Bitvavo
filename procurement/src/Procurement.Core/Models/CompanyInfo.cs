namespace Procurement.Core.Models
{
    /// <summary>Mirrors UniPro2026.Application.Models.CompanyInfo — one row from MAX's ExactRMCompanies table, shown in the login screen's company dropdown.</summary>
    public class CompanyInfo
    {
        public int CompanyID { get; set; }
        public string CompanyName { get; set; }
    }
}
