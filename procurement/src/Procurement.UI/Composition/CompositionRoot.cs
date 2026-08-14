using System.Collections.Generic;
using System.Linq;
using Procurement.Core.Interfaces;
using Procurement.Core.Session;
using Procurement.Data;
using Procurement.Data.Repositories;
using Procurement.Engine;
using Procurement.Erp;
using Procurement.Suppliers.DigiKey;
using Procurement.Suppliers.Farnell;

namespace Procurement.UI.Composition
{
    /// <summary>
    /// Manual composition root — this prototype is small enough that a DI container would only
    /// add ceremony. One ProcurementDbContext per application run keeps EF6 change tracking
    /// simple for a single-user WinForms prototype (spec §15: single user, no roles model).
    /// Built only after LoginForm has populated ProcurementSession (see Program.cs) — the
    /// database connection string itself depends on the company chosen at login.
    /// </summary>
    public class CompositionRoot
    {
        public ProcurementDbContext DbContext { get; }
        public ISessionContext Session { get; }
        public ProcurementEngine Engine { get; }
        public PurchaseRequestRepository PurchaseRequestRepository { get; }
        public SupplierProductMappingRepository SupplierProductMappingRepository { get; }
        public PurchaseOrderRepository PurchaseOrderRepository { get; }
        public PolicyRepository PolicyRepository { get; }
        public SupplierRepository SupplierRepository { get; }
        public ProcurementEventRepository ProcurementEventRepository { get; }
        public IReadOnlyList<ISupplierAdapter> Adapters { get; }
        public MaxErpConnector ErpConnector { get; }

        public CompositionRoot()
        {
            Session = new ProcurementSessionContext();
            DbContext = new ProcurementDbContext(ProcurementSession.SharedConnectionString);

            var auditLogger = new DbAuditLogger(DbContext);

            PurchaseRequestRepository = new PurchaseRequestRepository(DbContext, Session);
            SupplierProductMappingRepository = new SupplierProductMappingRepository(DbContext);
            var offerRepository = new SupplierOfferRepository(DbContext);
            var selectionRepository = new SupplierSelectionRepository(DbContext);
            var approvalRepository = new ApprovalRequestRepository(DbContext);
            PurchaseOrderRepository = new PurchaseOrderRepository(DbContext);
            PolicyRepository = new PolicyRepository(DbContext);
            SupplierRepository = new SupplierRepository(DbContext);
            ProcurementEventRepository = new ProcurementEventRepository(DbContext);

            // Blocking on purpose: this is startup-only, single-user, and reads 2 rows — the same
            // trade-off Program.cs already makes with its own synchronous schema check right before
            // this constructor runs. IsSandbox/UseMockData/credentials all now come from the
            // Supplier row (editable via Instellingen -> Suppliers) instead of being hardcoded here,
            // so flipping a supplier out of mock mode there actually takes effect on next restart.
            var suppliers = SupplierRepository.GetAllAsync().GetAwaiter().GetResult();
            var digiKeySupplier = suppliers.FirstOrDefault(s => s.SupplierCode == "DIGIKEY");
            var farnellSupplier = suppliers.FirstOrDefault(s => s.SupplierCode == "FARNELL");

            var digiKeyAdapter = new DigiKeyAdapter(new DigiKeyOptions
            {
                ClientId = digiKeySupplier?.ClientId,
                ClientSecret = digiKeySupplier?.ClientSecret,
                IsSandbox = digiKeySupplier?.IsSandbox ?? true,
                UseMockData = digiKeySupplier?.UseMockData ?? true
            });
            var farnellAdapter = new FarnellAdapter(new FarnellOptions
            {
                ApiKey = farnellSupplier?.ApiKey,
                IsSandbox = farnellSupplier?.IsSandbox ?? true,
                UseMockData = farnellSupplier?.UseMockData ?? true
            });
            Adapters = new List<ISupplierAdapter> { digiKeyAdapter, farnellAdapter };

            var maxOrderRepository = new MaxOrderRepository(Session);
            var maxVendorPartRepository = new MaxVendorPartRepository(Session);
            ErpConnector = new MaxErpConnector(
                PurchaseRequestRepository,
                PurchaseOrderRepository,
                maxOrderRepository,
                maxVendorPartRepository,
                SupplierRepository,
                SupplierProductMappingRepository);

            Engine = new ProcurementEngine(
                ErpConnector,
                Adapters,
                auditLogger,
                PurchaseRequestRepository,
                SupplierProductMappingRepository,
                offerRepository,
                selectionRepository,
                approvalRepository,
                PurchaseOrderRepository,
                PolicyRepository);
        }
    }
}
