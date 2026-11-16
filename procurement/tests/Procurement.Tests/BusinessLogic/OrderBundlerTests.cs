using System.Collections.Generic;
using System.Linq;
using Procurement.Core.BusinessLogic;
using Procurement.Core.Entities;
using Procurement.Core.Models;
using Xunit;

namespace Procurement.Tests.BusinessLogic
{
    public class OrderBundlerTests
    {
        [Fact]
        public void TenSmallLines_WithMoq5000_BecomeOneReel_SurplusOnEarliest()
        {
            var requested = Enumerable.Repeat(100, 10).ToList();

            var quantities = OrderBundler.AllocateQuantities(requested, earliestIndex: 0, orderMultiple: 5000, minimumOrderQuantity: 5000);

            Assert.Equal(5000, quantities.Sum());
            Assert.Equal(4100, quantities[0]);
            Assert.All(quantities.Skip(1), q => Assert.Equal(100, q));
        }

        [Fact]
        public void TotalAboveOneReel_RoundsUpToNextMultipleOnce()
        {
            var quantities = OrderBundler.AllocateQuantities(new[] { 3000, 2500 }, earliestIndex: 1, orderMultiple: 5000, minimumOrderQuantity: 5000);

            Assert.Equal(10000, quantities.Sum());
            Assert.Equal(3000, quantities[0]);
            Assert.Equal(7000, quantities[1]);
        }

        [Fact]
        public void SingleLine_MatchesQuantityCalculator()
        {
            var quantities = OrderBundler.AllocateQuantities(new[] { 120 }, 0, orderMultiple: 50, minimumOrderQuantity: 100);

            Assert.Equal(QuantityCalculator.CalculateOrderedQuantity(120, 50, 100), quantities[0]);
        }

        [Fact]
        public void Confirmation_IsSplitOverBundledLines_RestOnLast()
        {
            var lines = new List<PurchaseOrderLine>
            {
                new PurchaseOrderLine { Id = 1, SupplierPartNumber = "SPN", Quantity = 4100 },
                new PurchaseOrderLine { Id = 2, SupplierPartNumber = "SPN", Quantity = 100 },
                new PurchaseOrderLine { Id = 3, SupplierPartNumber = "SPN", Quantity = 800 }
            };
            var status = new SupplierOrderStatus
            {
                Lines = { new SupplierOrderStatusLine { SupplierPartNumber = "SPN", ConfirmedQuantity = 5000 } }
            };

            Assert.Equal(4100, OrderBundler.ConfirmationForLine(status, lines, lines[0]).ConfirmedQuantity);
            Assert.Equal(100, OrderBundler.ConfirmationForLine(status, lines, lines[1]).ConfirmedQuantity);
            Assert.Equal(800, OrderBundler.ConfirmationForLine(status, lines, lines[2]).ConfirmedQuantity);
        }

        [Fact]
        public void Confirmation_ShortDelivery_ShowsOnLastLine()
        {
            var lines = new List<PurchaseOrderLine>
            {
                new PurchaseOrderLine { Id = 1, SupplierPartNumber = "SPN", Quantity = 4100 },
                new PurchaseOrderLine { Id = 2, SupplierPartNumber = "SPN", Quantity = 900 }
            };
            var status = new SupplierOrderStatus
            {
                Lines = { new SupplierOrderStatusLine { SupplierPartNumber = "SPN", ConfirmedQuantity = 4500 } }
            };

            Assert.Equal(4100, OrderBundler.ConfirmationForLine(status, lines, lines[0]).ConfirmedQuantity);
            Assert.Equal(400, OrderBundler.ConfirmationForLine(status, lines, lines[1]).ConfirmedQuantity);
        }

        [Fact]
        public void Confirmation_SingleLine_IsUnchanged()
        {
            var line = new PurchaseOrderLine { Id = 1, SupplierPartNumber = "SPN", Quantity = 5000 };
            var statusLine = new SupplierOrderStatusLine { SupplierPartNumber = "SPN", ConfirmedQuantity = 5000 };
            var status = new SupplierOrderStatus { Lines = { statusLine } };

            Assert.Same(statusLine, OrderBundler.ConfirmationForLine(status, new[] { line }, line));
        }
    }
}
