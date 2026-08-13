namespace Procurement.Core.BusinessLogic
{
    /// <summary>Spec §6 step 6: landed_cost = product_cost + reeling_cost + shipping_cost + fees.</summary>
    public static class LandedCostCalculator
    {
        public static decimal Calculate(int orderedQuantity, decimal unitPrice, decimal reelingFee, decimal shippingCost, decimal tax)
        {
            var productCost = orderedQuantity * unitPrice;
            return productCost + reelingFee + shippingCost + tax;
        }
    }
}
