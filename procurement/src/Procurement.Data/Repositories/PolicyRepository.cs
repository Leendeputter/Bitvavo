using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;

namespace Procurement.Data.Repositories
{
    /// <summary>CRUD over the three business-rule tables from spec §3.5, managed via de Instellingen-scherm (§8.6).</summary>
    public class PolicyRepository
    {
        private readonly ProcurementDbContext _context;

        public PolicyRepository(ProcurementDbContext context)
        {
            _context = context;
        }

        public Task<ApprovalPolicy> GetApprovalPolicyAsync() => _context.RunGuardedAsync(() =>
            _context.ApprovalPolicies.FirstOrDefaultAsync());

        public Task SaveApprovalPolicyAsync(ApprovalPolicy policy) => _context.RunGuardedAsync(async () =>
        {
            if (policy.Id == 0)
                _context.ApprovalPolicies.Add(policy);
            await _context.SaveChangesAsync();
        });

        public Task<IReadOnlyList<PackagingPolicy>> GetPackagingPoliciesAsync() => _context.RunGuardedAsync(async () =>
        {
            IReadOnlyList<PackagingPolicy> result = await _context.PackagingPolicies.OrderBy(p => p.ComponentCategory).ToListAsync();
            return result;
        });

        public Task<PackagingPolicy> GetPackagingPolicyAsync(string componentCategory) => _context.RunGuardedAsync(async () =>
            await _context.PackagingPolicies.FirstOrDefaultAsync(p => p.ComponentCategory == componentCategory)
            ?? await _context.PackagingPolicies.FirstOrDefaultAsync(p => p.ComponentCategory == "Default"));

        public Task SavePackagingPolicyAsync(PackagingPolicy policy) => _context.RunGuardedAsync(async () =>
        {
            if (policy.Id == 0)
                _context.PackagingPolicies.Add(policy);
            await _context.SaveChangesAsync();
        });

        public Task<IReadOnlyList<SupplierPreference>> GetSupplierPreferencesAsync() => _context.RunGuardedAsync(async () =>
        {
            IReadOnlyList<SupplierPreference> result = await _context.SupplierPreferences.OrderBy(p => p.Priority).ToListAsync();
            return result;
        });

        public Task<SupplierPreference> GetSupplierPreferenceAsync(string supplierCode) => _context.RunGuardedAsync(() =>
            _context.SupplierPreferences.FirstOrDefaultAsync(p => p.SupplierCode == supplierCode));

        public Task SaveSupplierPreferenceAsync(SupplierPreference preference) => _context.RunGuardedAsync(async () =>
        {
            if (preference.Id == 0)
                _context.SupplierPreferences.Add(preference);
            await _context.SaveChangesAsync();
        });
    }
}
