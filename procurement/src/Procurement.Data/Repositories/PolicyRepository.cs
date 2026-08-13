using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;

namespace Procurement.Data.Repositories
{
    /// <summary>CRUD over the three business-rule tables from spec §3.5, managed via the Instellingen-scherm (§8.6).</summary>
    public class PolicyRepository
    {
        private readonly ProcurementDbContext _context;

        public PolicyRepository(ProcurementDbContext context)
        {
            _context = context;
        }

        public async Task<ApprovalPolicy> GetApprovalPolicyAsync()
        {
            return await _context.ApprovalPolicies.FirstOrDefaultAsync();
        }

        public async Task SaveApprovalPolicyAsync(ApprovalPolicy policy)
        {
            if (policy.Id == 0)
                _context.ApprovalPolicies.Add(policy);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<PackagingPolicy>> GetPackagingPoliciesAsync()
        {
            return await _context.PackagingPolicies.OrderBy(p => p.ComponentCategory).ToListAsync();
        }

        public async Task<PackagingPolicy> GetPackagingPolicyAsync(string componentCategory)
        {
            return await _context.PackagingPolicies.FirstOrDefaultAsync(p => p.ComponentCategory == componentCategory)
                   ?? await _context.PackagingPolicies.FirstOrDefaultAsync(p => p.ComponentCategory == "Default");
        }

        public async Task SavePackagingPolicyAsync(PackagingPolicy policy)
        {
            if (policy.Id == 0)
                _context.PackagingPolicies.Add(policy);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<SupplierPreference>> GetSupplierPreferencesAsync()
        {
            return await _context.SupplierPreferences.OrderBy(p => p.Priority).ToListAsync();
        }

        public async Task<SupplierPreference> GetSupplierPreferenceAsync(string supplierCode)
        {
            return await _context.SupplierPreferences.FirstOrDefaultAsync(p => p.SupplierCode == supplierCode);
        }

        public async Task SaveSupplierPreferenceAsync(SupplierPreference preference)
        {
            if (preference.Id == 0)
                _context.SupplierPreferences.Add(preference);
            await _context.SaveChangesAsync();
        }
    }
}
