using System.Collections.Generic;
using Newtonsoft.Json;

namespace Procurement.Suppliers.Mouser.Http
{
    // Shapes below follow Mouser's publicly documented Search API v1
    // (api.mouser.com/api/v1/search/...). Not yet verified against a real response — MouserAdapter's
    // real-mode parsing should be re-checked against actual JSON the first time a real API key is
    // available, and these DTOs adjusted if any field name is off.

    internal class MouserSearchResponse
    {
        [JsonProperty("SearchResults")]
        public MouserSearchResultsDto SearchResults { get; set; }

        [JsonProperty("Errors")]
        public List<MouserErrorDto> Errors { get; set; }
    }

    internal class MouserErrorDto
    {
        [JsonProperty("Id")]
        public int Id { get; set; }

        [JsonProperty("Code")]
        public string Code { get; set; }

        [JsonProperty("Message")]
        public string Message { get; set; }
    }

    internal class MouserSearchResultsDto
    {
        [JsonProperty("NumberOfResult")]
        public int NumberOfResult { get; set; }

        [JsonProperty("Parts")]
        public List<MouserPartDto> Parts { get; set; }
    }

    internal class MouserPartDto
    {
        [JsonProperty("MouserPartNumber")]
        public string MouserPartNumber { get; set; }

        [JsonProperty("ManufacturerPartNumber")]
        public string ManufacturerPartNumber { get; set; }

        [JsonProperty("Manufacturer")]
        public string Manufacturer { get; set; }

        [JsonProperty("Description")]
        public string Description { get; set; }

        [JsonProperty("DataSheetUrl")]
        public string DataSheetUrl { get; set; }

        // Documented as a free-text string, e.g. "48000 In Stock" or "On Order" — parsed
        // defensively rather than assumed to always start with a plain integer.
        [JsonProperty("Availability")]
        public string Availability { get; set; }

        // Also free text, e.g. "3 Days" / "Ships Today" — parsed defensively.
        [JsonProperty("LeadTime")]
        public string LeadTime { get; set; }

        [JsonProperty("Min")]
        public string Min { get; set; }

        [JsonProperty("Mult")]
        public string Mult { get; set; }

        [JsonProperty("PriceBreaks")]
        public List<MouserPriceBreakDto> PriceBreaks { get; set; }
    }

    internal class MouserPriceBreakDto
    {
        [JsonProperty("Quantity")]
        public int Quantity { get; set; }

        // Documented as a currency-formatted string (e.g. "$0.11" or "€0,11") rather than a plain
        // decimal — parsed defensively by stripping non-numeric characters except the decimal
        // separator.
        [JsonProperty("Price")]
        public string Price { get; set; }

        [JsonProperty("Currency")]
        public string Currency { get; set; }
    }
}
