using System;
using Procurement.Core.Models;

namespace Procurement.Core.Mocking
{
    /// <summary>
    /// Shared by every mock supplier adapter's GetOrderStatusAsync (DigiKey/Farnell/Mouser/TME/the
    /// generic Other adapter) — without this, a mock order confirmation always echoed back exactly
    /// what was ordered with an empty Lines list, so "Orderbevestiging verwerken" (MainForm) would
    /// silently do nothing observable in mock mode (the only mode available before real supplier
    /// credentials exist): no exceptions ever surface, since nothing ever deviates. Deterministic per
    /// (supplierOrderNumber, part) — same seeding approach as the rest of this mock layer — so a
    /// re-check of the same order keeps returning the same "confirmation".
    /// </summary>
    public static class MockOrderConfirmationBuilder
    {
        public static SupplierOrderStatusLine BuildLine(string supplierOrderNumber, SupplierOrderRequestLine orderedLine)
        {
            var seed = $"{supplierOrderNumber}|{orderedLine.SupplierPartNumber}";

            var quantityDeviates = DeterministicMock.NextInt(seed + "|qtydev", 0, 100) < 15;
            var priceDeviates = DeterministicMock.NextInt(seed + "|pricedev", 0, 100) < 15;
            var isLate = DeterministicMock.NextInt(seed + "|late", 0, 100) < 20;

            var confirmedQuantity = quantityDeviates
                ? Math.Max(1, orderedLine.Quantity - DeterministicMock.NextInt(seed + "|qtydelta", 1, Math.Max(2, orderedLine.Quantity / 10)))
                : orderedLine.Quantity;

            var confirmedUnitPrice = priceDeviates
                ? Math.Round(orderedLine.UnitPrice * 1.05m, 4)
                : orderedLine.UnitPrice;

            var leadDays = isLate
                ? DeterministicMock.NextInt(seed + "|leaddays", 10, 21)
                : DeterministicMock.NextInt(seed + "|leaddays", 2, 7);

            return new SupplierOrderStatusLine
            {
                SupplierPartNumber = orderedLine.SupplierPartNumber,
                ConfirmedQuantity = confirmedQuantity,
                ConfirmedUnitPrice = confirmedUnitPrice,
                EstimatedShipDate = DateTime.UtcNow.AddDays(leadDays)
            };
        }
    }
}
