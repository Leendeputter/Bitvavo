using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Procurement.Suppliers.DigiKey;
using Procurement.Suppliers.DigiKey.Http;

namespace Procurement.Services.Options
{
    public class SupplierSettingsRow
    {
        public int Id { get; set; }
        public string SupplierCode { get; set; }
        public string Name { get; set; }
        public string VendorId { get; set; }
        public bool IsSandbox { get; set; }
        public bool UseMockData { get; set; }
    }

    /// <summary>The ordering account and ship-to contact of one supplier (desktop: ShippingDetailsForm). Nothing here is secret.</summary>
    public class ShippingDetails
    {
        public int SupplierId { get; set; }
        public string SupplierCode { get; set; }
        public string AccountId { get; set; }
        public string ContactName { get; set; }
        public string ContactEmail { get; set; }
        public string ContactTelephone { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string City { get; set; }
        public string Province { get; set; }
        public string PostalCode { get; set; }
        public string CountryCode { get; set; }
    }

    public class SupplierPreferenceRow
    {
        public int Id { get; set; }
        public string SupplierCode { get; set; }
        public int Priority { get; set; }
        public bool Active { get; set; }
        public bool AllowedForAutoOrder { get; set; }
    }

    public class PackagingPolicyRow
    {
        public int Id { get; set; }
        public string ComponentCategory { get; set; }
        public decimal? ExceptionPriceThreshold { get; set; }
        public decimal SurplusPenaltyFactor { get; set; }
        public bool OriginalReelRequired { get; set; }
        public List<string> AllowedPackaging { get; set; } = new List<string>();
        public List<string> ExceptionPackaging { get; set; } = new List<string>();
    }

    public class AttritionRuleRow
    {
        public int Id { get; set; }
        public string AppliesTo { get; set; }
        public decimal? MinUnitPrice { get; set; }
        public decimal? MaxUnitPrice { get; set; }
        public decimal Percentage { get; set; }
        public int MinimumExtra { get; set; }
    }

    public class ApprovalPolicyModel
    {
        public decimal MaxOrderValueForAutoApproval { get; set; }
        public decimal MaxPriceVariancePercentage { get; set; }
        public bool AllowExternalSupplier { get; set; }
        public bool AllowNonOriginalPackaging { get; set; }
        public bool AllowAlternativePart { get; set; }
    }

    public class CapabilityRow
    {
        public string Supplier { get; set; }
        public string Capability { get; set; }
        public string Status { get; set; }
    }

    public class SettingsData
    {
        public IReadOnlyList<SupplierSettingsRow> Suppliers { get; set; }
        public IReadOnlyList<SupplierPreferenceRow> SupplierPreferences { get; set; }
        public IReadOnlyList<PackagingPolicyRow> PackagingPolicies { get; set; }
        public IReadOnlyList<AttritionRuleRow> AttritionRules { get; set; }
        public ApprovalPolicyModel ApprovalPolicy { get; set; }
        public IReadOnlyList<CapabilityRow> Capabilities { get; set; }
        public IReadOnlyList<string> PackagingTypes { get; set; }
    }

    /// <summary>A started DigiKey consent: the page to open, and the state to check the pasted URL against.</summary>
    public class DigiKeyAuthorization
    {
        public string AuthorizeUrl { get; set; }
        public string State { get; set; }
    }

    /// <summary>
    /// Settings (spec §8.6) for the web (19 nov 2026), the same tabs and rules as the desktop's
    /// SettingsForm: suppliers (VendorId, sandbox, mock data, shipping details), supplier
    /// preferences, packaging policies, extra for attrition, the approval policy, the read-only
    /// capability matrix, and DigiKey's one-time "Authorize Ordering". The desktop edits the
    /// tracked entities in place through data binding; the web posts plain rows, which are copied
    /// onto the tracked entities here by Id (Id 0 = a new row).
    /// </summary>
    public class SettingsScreenService
    {
        // Unknown (Any) isn't something to allow or forbid, and EitherReel is just OriginalReel + ReReel (as SettingsForm).
        public static readonly PackagingType[] SelectablePackagingTypes =
        {
            PackagingType.OriginalReel, PackagingType.ReReel, PackagingType.CutTape,
            PackagingType.Tray, PackagingType.Tube, PackagingType.Bulk
        };

        private readonly ProcurementServices _services;

        public SettingsScreenService(ProcurementServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public async Task<SettingsData> GetAsync()
        {
            var suppliers = await _services.SupplierRepository.GetAllAsync();
            var preferences = await _services.PolicyRepository.GetSupplierPreferencesAsync();
            var packaging = await _services.PolicyRepository.GetPackagingPoliciesAsync();
            var attrition = await _services.PolicyRepository.GetAttritionRulesAsync();
            var approval = await _services.PolicyRepository.GetApprovalPolicyAsync() ?? new ApprovalPolicy();

            return new SettingsData
            {
                Suppliers = suppliers.Select(s => new SupplierSettingsRow
                {
                    Id = s.Id,
                    SupplierCode = s.SupplierCode,
                    Name = s.Name,
                    VendorId = s.VendorId,
                    IsSandbox = s.IsSandbox,
                    UseMockData = s.UseMockData
                }).ToList(),
                SupplierPreferences = preferences.Select(p => new SupplierPreferenceRow
                {
                    Id = p.Id,
                    SupplierCode = p.SupplierCode,
                    Priority = p.Priority,
                    Active = p.Active,
                    AllowedForAutoOrder = p.AllowedForAutoOrder
                }).ToList(),
                PackagingPolicies = packaging.Select(p => new PackagingPolicyRow
                {
                    Id = p.Id,
                    ComponentCategory = p.ComponentCategory,
                    ExceptionPriceThreshold = p.ExceptionPriceThreshold,
                    SurplusPenaltyFactor = p.SurplusPenaltyFactor,
                    OriginalReelRequired = p.OriginalReelRequired,
                    AllowedPackaging = Expand(p.AllowedPackaging).Select(t => t.ToString()).ToList(),
                    ExceptionPackaging = Expand(p.ExceptionPackaging).Select(t => t.ToString()).ToList()
                }).ToList(),
                AttritionRules = attrition.Select(r => new AttritionRuleRow
                {
                    Id = r.Id,
                    AppliesTo = r.AppliesTo,
                    MinUnitPrice = r.MinUnitPrice,
                    MaxUnitPrice = r.MaxUnitPrice,
                    Percentage = r.Percentage,
                    MinimumExtra = r.MinimumExtra
                }).ToList(),
                ApprovalPolicy = new ApprovalPolicyModel
                {
                    MaxOrderValueForAutoApproval = approval.MaxOrderValueForAutoApproval,
                    MaxPriceVariancePercentage = approval.MaxPriceVariancePercentage,
                    AllowExternalSupplier = approval.AllowExternalSupplier,
                    AllowNonOriginalPackaging = approval.AllowNonOriginalPackaging,
                    AllowAlternativePart = approval.AllowAlternativePart
                },
                Capabilities = suppliers
                    .SelectMany(s => s.Capabilities.Select(c => new CapabilityRow { Supplier = s.SupplierCode, Capability = c.Capability, Status = c.Status.ToString() }))
                    .OrderBy(x => x.Supplier).ThenBy(x => x.Capability)
                    .ToList(),
                PackagingTypes = SelectablePackagingTypes.Select(t => t.ToString()).ToList()
            };
        }

        public async Task SaveSuppliersAsync(IEnumerable<SupplierSettingsRow> rows)
        {
            foreach (var row in rows ?? Enumerable.Empty<SupplierSettingsRow>())
                await _services.SupplierRepository.UpdateSettingsAsync(row.Id, Clean(row.VendorId), row.IsSandbox, row.UseMockData);
        }

        public async Task<ShippingDetails> GetShippingDetailsAsync(string supplierCode)
        {
            var s = await _services.SupplierRepository.GetByCodeAsync(supplierCode);
            if (s == null) return null;
            return new ShippingDetails
            {
                SupplierId = s.Id,
                SupplierCode = s.SupplierCode,
                AccountId = s.AccountId,
                ContactName = s.ContactName,
                ContactEmail = s.ContactEmail,
                ContactTelephone = s.ContactTelephone,
                AddressLine1 = s.AddressLine1,
                AddressLine2 = s.AddressLine2,
                City = s.City,
                Province = s.Province,
                PostalCode = s.PostalCode,
                CountryCode = s.CountryCode
            };
        }

        /// <summary>Always overwrites, as the desktop dialog: a cleared field is saved as "".</summary>
        public Task SaveShippingDetailsAsync(ShippingDetails d) =>
            _services.SupplierRepository.UpdateOrderingContactAsync(
                d.SupplierId, Trim(d.AccountId), Trim(d.ContactName), Trim(d.ContactEmail), Trim(d.ContactTelephone),
                Trim(d.AddressLine1), Trim(d.AddressLine2), Trim(d.City), Trim(d.Province), Trim(d.PostalCode), Trim(d.CountryCode));

        /// <summary>Rows without a supplier code are skipped, as on the desktop (the empty "new row").</summary>
        public async Task SaveSupplierPreferencesAsync(IEnumerable<SupplierPreferenceRow> rows)
        {
            var stored = (await _services.PolicyRepository.GetSupplierPreferencesAsync()).ToDictionary(p => p.Id);
            foreach (var row in (rows ?? Enumerable.Empty<SupplierPreferenceRow>()).Where(r => !string.IsNullOrWhiteSpace(r.SupplierCode)))
            {
                SupplierPreference entity;
                if (row.Id == 0 || !stored.TryGetValue(row.Id, out entity)) entity = new SupplierPreference();
                entity.SupplierCode = row.SupplierCode.Trim();
                entity.Priority = row.Priority;
                entity.Active = row.Active;
                entity.AllowedForAutoOrder = row.AllowedForAutoOrder;
                await _services.PolicyRepository.SaveSupplierPreferenceAsync(entity);
            }
        }

        public async Task SavePackagingPoliciesAsync(IEnumerable<PackagingPolicyRow> rows)
        {
            var stored = (await _services.PolicyRepository.GetPackagingPoliciesAsync()).ToDictionary(p => p.Id);
            foreach (var row in (rows ?? Enumerable.Empty<PackagingPolicyRow>()).Where(r => !string.IsNullOrWhiteSpace(r.ComponentCategory)))
            {
                PackagingPolicy entity;
                if (row.Id == 0 || !stored.TryGetValue(row.Id, out entity)) entity = new PackagingPolicy();
                entity.ComponentCategory = row.ComponentCategory.Trim();
                entity.ExceptionPriceThreshold = row.ExceptionPriceThreshold;
                entity.SurplusPenaltyFactor = row.SurplusPenaltyFactor;
                entity.OriginalReelRequired = row.OriginalReelRequired;
                entity.AllowedPackaging = ParseTypes(row.AllowedPackaging);
                entity.ExceptionPackaging = ParseTypes(row.ExceptionPackaging);
                await _services.PolicyRepository.SavePackagingPolicyAsync(entity);
            }
        }

        /// <summary>The whole table, as on the desktop: rows left out are deleted.</summary>
        public Task SaveAttritionRulesAsync(IEnumerable<AttritionRuleRow> rows)
        {
            var rules = (rows ?? Enumerable.Empty<AttritionRuleRow>())
                .Where(r => !string.IsNullOrWhiteSpace(r.AppliesTo))
                .Select(r => new AttritionRule
                {
                    Id = r.Id,
                    AppliesTo = r.AppliesTo.Trim(),
                    MinUnitPrice = r.MinUnitPrice,
                    MaxUnitPrice = r.MaxUnitPrice,
                    Percentage = r.Percentage,
                    MinimumExtra = r.MinimumExtra
                }).ToList();
            return _services.PolicyRepository.SaveAttritionRulesAsync(rules);
        }

        public async Task SaveApprovalPolicyAsync(ApprovalPolicyModel model)
        {
            var policy = await _services.PolicyRepository.GetApprovalPolicyAsync() ?? new ApprovalPolicy();
            policy.MaxOrderValueForAutoApproval = model.MaxOrderValueForAutoApproval;
            policy.MaxPriceVariancePercentage = model.MaxPriceVariancePercentage;
            policy.AllowExternalSupplier = model.AllowExternalSupplier;
            policy.AllowNonOriginalPackaging = model.AllowNonOriginalPackaging;
            policy.AllowAlternativePart = model.AllowAlternativePart;
            await _services.PolicyRepository.SaveApprovalPolicyAsync(policy);
        }

        /// <summary>
        /// Step 1 of DigiKey's one-time Ordering consent (desktop: SettingsForm.AuthorizeDigiKeyOrderingAsync).
        /// Reads the client id/secret under the logged-in user's SecretAccess rights.
        /// </summary>
        public async Task<DigiKeyAuthorization> StartDigiKeyAuthorizationAsync()
        {
            var options = await DigiKeyOptionsAsync();
            var state = Guid.NewGuid().ToString("N");
            return new DigiKeyAuthorization { AuthorizeUrl = new DigiKeyOrderingAuthorizer().BuildAuthorizeUrl(options, state), State = state };
        }

        /// <summary>Step 2: the pasted "https://localhost/?code=..." address in, refresh token saved in SharedSecrets.</summary>
        public async Task FinishDigiKeyAuthorizationAsync(string pastedUrl, string expectedState)
        {
            var options = await DigiKeyOptionsAsync();
            var code = DigiKeyOrderingAuthorizer.ExtractAuthorizationCode(pastedUrl, expectedState);
            var refreshToken = await new DigiKeyOrderingAuthorizer().ExchangeAuthorizationCodeAsync(options, code);
            _services.Secrets.SaveDigiKeyRefreshToken(refreshToken);
        }

        private async Task<DigiKeyOptions> DigiKeyOptionsAsync()
        {
            var supplier = await _services.SupplierRepository.GetByCodeAsync(DigiKeyAdapter.Code);
            return new DigiKeyOptions
            {
                ClientId = _services.Secrets.DigiKeyClientId,
                ClientSecret = _services.Secrets.DigiKeyClientSecret,
                IsSandbox = supplier?.IsSandbox ?? false
            };
        }

        private static HashSet<PackagingType> Expand(IEnumerable<PackagingType> types)
        {
            var set = new HashSet<PackagingType>(types ?? Enumerable.Empty<PackagingType>());
            if (set.Remove(PackagingType.EitherReel))
            {
                set.Add(PackagingType.OriginalReel);
                set.Add(PackagingType.ReReel);
            }
            return set;
        }

        /// <summary>Only the selectable types, in the order of the list; anything else posted is ignored.</summary>
        private static List<PackagingType> ParseTypes(IEnumerable<string> names)
        {
            var chosen = new HashSet<string>(names ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return SelectablePackagingTypes.Where(t => chosen.Contains(t.ToString())).ToList();
        }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static string Trim(string value) => (value ?? string.Empty).Trim();
    }
}
