using System.Collections.Generic;
using System.Threading.Tasks;
using Procurement.Core.Models;

namespace Procurement.Core.Interfaces
{
    /// <summary>
    /// All supplier-specific logic lives behind this interface — ProcurementEngine never depends
    /// on a concrete supplier. A capability an adapter does not support is reported via
    /// <see cref="Capabilities"/>, never by throwing, so the engine can skip that step cleanly.
    /// </summary>
    public interface ISupplierAdapter
    {
        string SupplierCode { get; }
        SupplierCapabilities Capabilities { get; }

        Task<IReadOnlyList<SupplierProduct>> SearchProductsAsync(ProductSearchRequest request);
        Task<SupplierProduct> GetProductAsync(string supplierPartNumber);
        Task<SupplierAvailability> GetAvailabilityAsync(string supplierPartNumber);
        Task<SupplierPricing> GetPricingAsync(string supplierPartNumber, int quantity);
        Task<IReadOnlyList<SupplierPackagingOption>> GetPackagingOptionsAsync(string supplierPartNumber, int quantity);

        Task<SupplierOrderResult> CreateOrderAsync(SupplierOrderRequest request, string idempotencyKey);
        Task<SupplierOrderStatus> GetOrderStatusAsync(string supplierOrderNumber);
        Task<bool> CancelOrderAsync(string supplierOrderNumber);
    }
}
