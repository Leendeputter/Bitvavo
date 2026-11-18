using System;
using System.Collections.Generic;
using System.Linq;
using Procurement.Core.Entities;
using Procurement.Erp;
using Procurement.Services.PurchaseRequests;
using Xunit;

namespace Procurement.Tests.Services
{
    /// <summary>The local due date / range filter, moved from MainForm to the shared service (18 nov 2026).</summary>
    public class RequestListFilterTests
    {
        private static PurchaseRequest Request(string number, DateTime? required, string part = "P1", string customer = "C1") => new PurchaseRequest
        {
            ErpRequestNumber = number,
            RequiredDate = required,
            Lines = new List<PurchaseRequestLine> { new PurchaseRequestLine { ErpArticleId = part, Customer = customer } }
        };

        [Fact]
        public void NoFilter_KeepsEverything()
        {
            var requests = new[] { Request("1001", null), Request("1002", new DateTime(2026, 12, 1)) };
            Assert.Equal(2, PurchaseRequestScreenService.ApplyRangeAndDueDateFilters(requests, new RequestListFilter()).Count);
        }

        [Fact]
        public void DueDate_FiltersButKeepsTestRequests()
        {
            var requests = new[]
            {
                Request("1001", new DateTime(2026, 11, 20)),
                Request("1002", new DateTime(2027, 1, 5)),
                Request("TEST-1", new DateTime(2027, 1, 5))
            };
            var filter = new RequestListFilter { DueDateStart = new DateTime(2026, 11, 1), DueDateEnd = new DateTime(2026, 11, 30) };

            var result = PurchaseRequestScreenService.ApplyRangeAndDueDateFilters(requests, filter);

            Assert.Equal(new[] { "1001", "TEST-1" }, result.Select(r => r.ErpRequestNumber));
        }

        [Fact]
        public void Range_OnPart_IsTrimmedAndOrdinal()
        {
            var requests = new[] { Request("1", null, part: "A100"), Request("2", null, part: "B200"), Request("3", null, part: "C300") };
            var filter = new RequestListFilter { RangeField = MaxOrderRangeField.Part, RangeStart = " B ", RangeEnd = "C" };

            var result = PurchaseRequestScreenService.ApplyRangeAndDueDateFilters(requests, filter);

            Assert.Equal(new[] { "2" }, result.Select(r => r.ErpRequestNumber));
        }

        [Theory]
        [InlineData(10, 2.5, 1, 25.0)]
        [InlineData(10, 2.5, 0, null)]
        public void ExtCost(int quantity, double cost, double conv, double? expected)
        {
            var result = PurchaseRequestScreenService.ComputeExtCost(quantity, (decimal)cost, (decimal)conv);
            Assert.Equal(expected.HasValue ? (decimal?)expected.Value : null, result);
        }
    }
}
