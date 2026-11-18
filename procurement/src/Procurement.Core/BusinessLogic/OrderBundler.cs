using System;
using System.Collections.Generic;
using System.Linq;
using Procurement.Core.Entities;
using Procurement.Core.Models;

namespace Procurement.Core.BusinessLogic
{
    /// <summary>
    /// Bundling of several request lines for the same article into one supplier purchase (user
    /// request, 16 nov 2026: "10 regels voor hetzelfde artikel ... samenvoegen tot 1 inkoop van een
    /// volle reel van 5000 stuks, maar voorkomen dat er 10x een reel van 5000 stuks besteld wordt").
    /// Sourcing still rounds each line up on its own (an offer per line); at Place Order the lines
    /// with the same supplier part number are summed and rounded up to MOQ/order multiple ONCE.
    /// MAX and the supplier both get ONE line with the total (user, 16 nov 2026: "Wat we bestellen
    /// bij de leverancier moet hetzelfde zijn als wat in MAX terecht komt"), on the earliest required
    /// date; all bundled PRs are deleted in MAX. Locally each request line keeps its own tracking
    /// PurchaseOrderLine (all pointing at that one MAX line), with the rounding surplus on the
    /// earliest one, so "is this request line ordered" keeps working per line.
    /// </summary>
    public static class OrderBundler
    {
        /// <summary>
        /// Per-line PO quantities for one bundle, in the same order as <paramref name="requestedQuantities"/>.
        /// Their sum is the bundle's ordered quantity: sum of requested, rounded up once to MOQ/multiple,
        /// with the surplus added to the line at <paramref name="earliestIndex"/>.
        /// </summary>
        public static int[] AllocateQuantities(IReadOnlyList<int> requestedQuantities, int earliestIndex, int orderMultiple, int minimumOrderQuantity)
        {
            if (requestedQuantities == null || requestedQuantities.Count == 0)
                throw new ArgumentException("At least one requested quantity is needed.", nameof(requestedQuantities));
            if (earliestIndex < 0 || earliestIndex >= requestedQuantities.Count)
                throw new ArgumentOutOfRangeException(nameof(earliestIndex));

            var totalRequested = requestedQuantities.Sum();
            var totalOrdered = QuantityCalculator.CalculateOrderedQuantity(totalRequested, orderMultiple, minimumOrderQuantity);

            var result = requestedQuantities.ToArray();
            result[earliestIndex] += totalOrdered - totalRequested;
            return result;
        }

        /// <summary>
        /// Spreads fixed pools (e.g. full reels, then the rest on a re-reel) over lines in the given
        /// order: each line takes from the first pool, then the next. Whatever the pools hold beyond
        /// the total need (surplus) goes to the first line. Result[line][pool].
        /// A line can end up with quantities from two pools (two PO lines for one request line).
        /// </summary>
        public static int[][] AllocateAcrossPools(IReadOnlyList<int> neededQuantities, IReadOnlyList<int> poolQuantities)
        {
            if (neededQuantities == null || neededQuantities.Count == 0)
                throw new ArgumentException("At least one needed quantity is required.", nameof(neededQuantities));
            if (poolQuantities == null || poolQuantities.Count == 0)
                throw new ArgumentException("At least one pool is required.", nameof(poolQuantities));

            var result = neededQuantities.Select(_ => new int[poolQuantities.Count]).ToArray();
            var remainingInPool = poolQuantities.ToArray();
            var needs = neededQuantities.ToArray();

            // Surplus first on the earliest line, so later lines still get exactly what they need.
            var surplus = Math.Max(poolQuantities.Sum() - needs.Sum(), 0);
            needs[0] += surplus;

            var pool = 0;
            for (var line = 0; line < needs.Length; line++)
            {
                var need = needs[line];
                while (need > 0 && pool < remainingInPool.Length)
                {
                    var take = Math.Min(need, remainingInPool[pool]);
                    result[line][pool] += take;
                    remainingInPool[pool] -= take;
                    need -= take;
                    if (remainingInPool[pool] == 0) pool++;
                }
            }

            return result;
        }

        /// <summary>
        /// The supplier's confirmation for one PO line. A supplier confirms per part number, so when
        /// several PO lines share one supplier part number (a bundle), the confirmed quantity is
        /// split over them in line order: each gets up to its own ordered quantity, the last one gets
        /// whatever is left (so a short or over-delivery shows up on the last line only).
        /// </summary>
        public static SupplierOrderStatusLine ConfirmationForLine(SupplierOrderStatus status, IEnumerable<PurchaseOrderLine> orderLines, PurchaseOrderLine line)
        {
            var statusLine = status?.Lines?.FirstOrDefault(l => l.SupplierPartNumber == line.SupplierPartNumber);
            if (statusLine == null) return null;

            var siblings = (orderLines ?? Enumerable.Empty<PurchaseOrderLine>())
                .Where(l => l.SupplierPartNumber == line.SupplierPartNumber)
                .OrderBy(l => l.Id)
                .ToList();
            if (siblings.Count <= 1) return statusLine;

            var remaining = statusLine.ConfirmedQuantity;
            var share = 0;
            for (var i = 0; i < siblings.Count; i++)
            {
                share = i == siblings.Count - 1 ? remaining : Math.Min(siblings[i].Quantity, Math.Max(remaining, 0));
                remaining -= share;
                if (siblings[i].Id == line.Id) break;
            }

            return new SupplierOrderStatusLine
            {
                SupplierPartNumber = statusLine.SupplierPartNumber,
                ConfirmedQuantity = share,
                EstimatedShipDate = statusLine.EstimatedShipDate,
                ConfirmedUnitPrice = statusLine.ConfirmedUnitPrice
            };
        }
    }
}
