using System;

namespace Procurement.Core.BusinessLogic
{
    /// <summary>
    /// Rounds the requested quantity up to a valid order quantity, respecting MOQ and the order
    /// multiple (which, for reel-packaged parts, is the reel/packaging quantity). Spec §6 step 5:
    /// ordered_quantity = ceil(requested_quantity / order_multiple) * order_multiple.
    /// </summary>
    public static class QuantityCalculator
    {
        public static int CalculateOrderedQuantity(int requestedQuantity, int orderMultiple, int minimumOrderQuantity)
        {
            if (requestedQuantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(requestedQuantity), "Requested quantity must be positive.");

            var multiple = orderMultiple <= 0 ? 1 : orderMultiple;
            var baseQuantity = Math.Max(requestedQuantity, minimumOrderQuantity);
            var multiples = (int)Math.Ceiling(baseQuantity / (double)multiple);
            return multiples * multiple;
        }
    }
}
