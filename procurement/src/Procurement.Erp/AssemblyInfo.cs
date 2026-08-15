using System.Runtime.CompilerServices;

// Lets Procurement.Tests call MaxPurchaseOrderRepository.BuildPurchaseOrderLine/BuildConfirmedReference directly.
[assembly: InternalsVisibleTo("Procurement.Tests")]
