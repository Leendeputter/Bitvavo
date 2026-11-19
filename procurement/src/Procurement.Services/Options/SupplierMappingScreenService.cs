using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Enums;

namespace Procurement.Services.Options
{
    /// <summary>One mapping row, the columns of the desktop's SupplierMappingForm.</summary>
    public class SupplierMappingRow
    {
        public int Id { get; set; }
        public string ErpArticleId { get; set; }
        public string SupplierCode { get; set; }
        public string SupplierPartNumber { get; set; }
        public string Manufacturer { get; set; }
        public string ManufacturerPartNumber { get; set; }
        public string MatchConfidence { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Supplier Mapping (spec §8.5): list the mappings, filter on match confidence, and mark one as
    /// Verified. Used by the web (19 nov 2026); same behaviour as the desktop's SupplierMappingForm.
    /// </summary>
    public class SupplierMappingScreenService
    {
        private readonly ProcurementServices _services;

        public SupplierMappingScreenService(ProcurementServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>The filter choices: "(all)" followed by every MatchConfidence.</summary>
        public static IReadOnlyList<string> FilterChoices =>
            new[] { "(all)" }.Concat(Enum.GetNames(typeof(MatchConfidence))).ToList();

        /// <summary><paramref name="confidence"/> null, empty or "(all)" shows every mapping.</summary>
        public async Task<IReadOnlyList<SupplierMappingRow>> GetMappingsAsync(string confidence)
        {
            MatchConfidence? filter = null;
            if (!string.IsNullOrEmpty(confidence) && Enum.TryParse(confidence, out MatchConfidence parsed))
                filter = parsed;

            var mappings = await _services.SupplierProductMappingRepository.GetAllAsync(filter);
            return mappings.Select(m => new SupplierMappingRow
            {
                Id = m.Id,
                ErpArticleId = m.ErpArticleId,
                SupplierCode = m.SupplierCode,
                SupplierPartNumber = m.SupplierPartNumber,
                Manufacturer = m.Manufacturer,
                ManufacturerPartNumber = m.ManufacturerPartNumber,
                MatchConfidence = m.MatchConfidence.ToString(),
                CreatedAt = m.CreatedAt
            }).ToList();
        }

        public Task MarkVerifiedAsync(int mappingId) =>
            _services.SupplierProductMappingRepository.UpdateConfidenceAsync(mappingId, MatchConfidence.Verified);
    }
}
