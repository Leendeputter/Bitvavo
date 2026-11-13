using System;

namespace Procurement.Core.Models
{
    /// <summary>
    /// One row in SharedSecrets.dbo.SecretAccess — who may let this app decrypt/use which secret.
    /// Same shape as UniPro2026's Application/Models/SecretAccessEntry.cs (deliberately: once
    /// Procurement is merged into UniPro, this class can simply be dropped in favor of UniPro's).
    /// </summary>
    public class SecretAccessEntry
    {
        public string SecretName { get; set; }
        public string UserName { get; set; }
        public bool CanRead { get; set; }
        public string GrantedBy { get; set; }
        public DateTime? GrantedDate { get; set; }
    }
}
