using System;
using Procurement.Core.Models;
using Xunit;

namespace Procurement.Tests.Models
{
    /// <summary>
    /// QuantityMismatch/PriceMismatch/DeliveryIsLate decide which confirmed lines MainForm shows
    /// under "Orderbevestiging verwerken" versus which get applied silently — the newest, and until
    /// now entirely untested, decision logic in the confirmation-processing feature.
    /// </summary>
    public class OrderConfirmationExceptionTests
    {
        private static OrderConfirmationException Build(
            int orderedQuantity = 100, int? confirmedQuantity = 100,
            decimal orderedUnitPrice = 2.50m, decimal? confirmedUnitPrice = 2.50m,
            DateTime? requiredDate = null, DateTime? confirmedShipDate = null) =>
            new OrderConfirmationException
            {
                OrderedQuantity = orderedQuantity,
                ConfirmedQuantity = confirmedQuantity,
                OrderedUnitPrice = orderedUnitPrice,
                ConfirmedUnitPrice = confirmedUnitPrice,
                RequiredDate = requiredDate,
                ConfirmedShipDate = confirmedShipDate
            };

        [Fact]
        public void QuantityMismatch_EqualQuantities_IsFalse()
        {
            var ex = Build(orderedQuantity: 100, confirmedQuantity: 100);
            Assert.False(ex.QuantityMismatch);
        }

        [Fact]
        public void QuantityMismatch_DifferentQuantities_IsTrue()
        {
            var ex = Build(orderedQuantity: 100, confirmedQuantity: 90);
            Assert.True(ex.QuantityMismatch);
        }

        [Fact]
        public void QuantityMismatch_NullConfirmedQuantity_IsFalse()
        {
            // Null means "no confirmation data for this field" (e.g. adapter didn't report it) —
            // must never be treated as a mismatch, or every partially-populated confirmation would
            // wrongly surface as an exception.
            var ex = Build(orderedQuantity: 100, confirmedQuantity: null);
            Assert.False(ex.QuantityMismatch);
        }

        [Fact]
        public void PriceMismatch_EqualPrices_IsFalse()
        {
            var ex = Build(orderedUnitPrice: 2.50m, confirmedUnitPrice: 2.50m);
            Assert.False(ex.PriceMismatch);
        }

        [Fact]
        public void PriceMismatch_EqualPricesDifferentScale_IsFalse()
        {
            // decimal equality ignores trailing-zero scale (2.50m == 2.5m) — confirms that isn't
            // accidentally broken by a ToString/rounding-based comparison instead of == later.
            var ex = Build(orderedUnitPrice: 2.50m, confirmedUnitPrice: 2.5m);
            Assert.False(ex.PriceMismatch);
        }

        [Fact]
        public void PriceMismatch_DifferentPrices_IsTrue()
        {
            var ex = Build(orderedUnitPrice: 2.50m, confirmedUnitPrice: 2.625m);
            Assert.True(ex.PriceMismatch);
        }

        [Fact]
        public void PriceMismatch_NullConfirmedPrice_IsFalse()
        {
            var ex = Build(orderedUnitPrice: 2.50m, confirmedUnitPrice: null);
            Assert.False(ex.PriceMismatch);
        }

        [Fact]
        public void DeliveryIsLate_ConfirmedAfterRequired_IsTrue()
        {
            var ex = Build(requiredDate: new DateTime(2026, 8, 21), confirmedShipDate: new DateTime(2026, 8, 25));
            Assert.True(ex.DeliveryIsLate);
        }

        [Fact]
        public void DeliveryIsLate_ConfirmedOnSameDate_IsFalse()
        {
            var ex = Build(requiredDate: new DateTime(2026, 8, 21), confirmedShipDate: new DateTime(2026, 8, 21));
            Assert.False(ex.DeliveryIsLate);
        }

        [Fact]
        public void DeliveryIsLate_SameCalendarDayDifferentTimeOfDay_IsFalse()
        {
            // Only the Date component counts — a ship timestamp later in the same day as the
            // required date must not read as "late".
            var ex = Build(requiredDate: new DateTime(2026, 8, 21, 8, 0, 0), confirmedShipDate: new DateTime(2026, 8, 21, 23, 0, 0));
            Assert.False(ex.DeliveryIsLate);
        }

        [Fact]
        public void DeliveryIsLate_ConfirmedBeforeRequired_IsFalse()
        {
            var ex = Build(requiredDate: new DateTime(2026, 8, 21), confirmedShipDate: new DateTime(2026, 8, 18));
            Assert.False(ex.DeliveryIsLate);
        }

        [Fact]
        public void DeliveryIsLate_MissingRequiredDate_IsFalse()
        {
            var ex = Build(requiredDate: null, confirmedShipDate: new DateTime(2026, 8, 25));
            Assert.False(ex.DeliveryIsLate);
        }

        [Fact]
        public void DeliveryIsLate_MissingConfirmedShipDate_IsFalse()
        {
            var ex = Build(requiredDate: new DateTime(2026, 8, 21), confirmedShipDate: null);
            Assert.False(ex.DeliveryIsLate);
        }
    }
}
