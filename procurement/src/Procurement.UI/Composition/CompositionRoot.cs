using System.Collections.Generic;
using System.Linq;
using Procurement.Core.Entities;
using Procurement.Core.Interfaces;
using Procurement.Core.Session;
using Procurement.Data;
using Procurement.Data.Repositories;
using Procurement.Engine;
using Procurement.Erp;
using Procurement.Suppliers.DigiKey;
using Procurement.Suppliers.DigiKey.Http;
using Procurement.Suppliers.Farnell;
using Procurement.Suppliers.Farnell.Http;
using Procurement.Suppliers.Mouser;
using Procurement.Suppliers.Mouser.Http;
using Procurement.Suppliers.Other;
using Procurement.Suppliers.TME;
using Procurement.Suppliers.TME.Http;

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
            Supplier FindSupplier(string code) => suppliers.FirstOrDefault(s => s.SupplierCode == code);

            var digiKeySupplier = FindSupplier("DIGIKEY");
            var farnellSupplier = FindSupplier("FARNELL");

            var digiKeyOptions = new DigiKeyOptions
            {
                ClientId = digiKeySupplier?.ClientId,
                ClientSecret = digiKeySupplier?.ClientSecret,
                IsSandbox = digiKeySupplier?.IsSandbox ?? true,
                UseMockData = digiKeySupplier?.UseMockData ?? true
            };
            var farnellOptions = new FarnellOptions
            {
                ApiKey = farnellSupplier?.ApiKey,
                IsSandbox = farnellSupplier?.IsSandbox ?? true,
                UseMockData = farnellSupplier?.UseMockData ?? true
            };

            // Mouser: ApiKey only (self-service Search API key, no OAuth). TME: reuses the
            // ClientId/ClientSecret columns for its Token/HMAC-secret pair (Token = ClientId,
            // ApiKey secret = ClientSecret) rather than adding yet more Supplier columns — see
            // TmeOptions's doc comment.
            var mouserSupplier = FindSupplier("MOUSER");
            var mouserOptions = new MouserOptions
            {
                ApiKey = mouserSupplier?.ApiKey,
                IsSandbox = mouserSupplier?.IsSandbox ?? true,
                UseMockData = mouserSupplier?.UseMockData ?? true
            };
            var tmeSupplier = FindSupplier("TME");
            var tmeOptions = new TmeOptions
            {
                Token = tmeSupplier?.ClientId,
                ApiKey = tmeSupplier?.ClientSecret,
                IsSandbox = tmeSupplier?.IsSandbox ?? true,
                UseMockData = tmeSupplier?.UseMockData ?? true
            };

            // The HTTP wrappers are cheap to construct (just an HttpClient + options) and are only
            // ever called when UseMockData is false, so they're always built rather than
            // conditionally wired — one less branch to get wrong here.
            var digiKeyAdapter = new DigiKeyAdapter(digiKeyOptions, new DigiKeyHttpClientWrapper(digiKeyOptions));
            var farnellAdapter = new FarnellAdapter(farnellOptions, new FarnellHttpClientWrapper(farnellOptions));
            var mouserAdapter = new MouserAdapter(mouserOptions, new MouserHttpClientWrapper(mouserOptions));
            var tmeAdapter = new TmeAdapter(tmeOptions, new TmeHttpClientWrapper(tmeOptions));

            // Distributors with no confirmed public API yet (likely account-/EDI-gated) all share
            // one generic mock adapter (Procurement.Suppliers.Other) instead of seven near-copies
            // of DigiKeyAdapter — see GenericMockSupplierAdapter's doc comment. Swap an entry out
            // for a dedicated adapter (mirroring DigiKeyAdapter/MouserAdapter) once that
            // distributor's real API/EDI contract is confirmed.
            var genericSupplierDefinitions = new[]
            {
                (Code: "ARROW", DatasheetHost: "www.arrow.com"),
                (Code: "RUTRONIK", DatasheetHost: "www.rutronik.com"),
                (Code: "AVNET_SILICA", DatasheetHost: "www.avnet.com"),
                (Code: "KARL_KRUSE", DatasheetHost: "www.karlkruse.de"),
                (Code: "RS_COMPONENTS", DatasheetHost: "www.rs-online.com"),
                (Code: "DISTRELEC", DatasheetHost: "www.distrelec.nl"),
                (Code: "CONRAD", DatasheetHost: "www.conrad.nl"),
            };
            var genericAdapters = genericSupplierDefinitions.Select(def =>
            {
                var supplier = FindSupplier(def.Code);
                var options = new GenericMockSupplierOptions
                {
                    ClientId = supplier?.ClientId,
                    ClientSecret = supplier?.ClientSecret,
                    ApiKey = supplier?.ApiKey,
                    IsSandbox = supplier?.IsSandbox ?? true,
                    UseMockData = supplier?.UseMockData ?? true
                };
                return (ISupplierAdapter)new GenericMockSupplierAdapter(def.Code, def.DatasheetHost, options);
            });

            Adapters = new List<ISupplierAdapter> { digiKeyAdapter, farnellAdapter, mouserAdapter, tmeAdapter }
                .Concat(genericAdapters)
                .ToList();

            var maxOrderRepository = new MaxOrderRepository(Session);
            var maxVendorPartRepository = new MaxVendorPartRepository(Session);
            var maxPurchaseOrderRepository = new MaxPurchaseOrderRepository(Session, SupplierRepository, PurchaseRequestRepository);
            ErpConnector = new MaxErpConnector(
                PurchaseRequestRepository,
                PurchaseOrderRepository,
                maxOrderRepository,
                maxVendorPartRepository,
                SupplierRepository,
                SupplierProductMappingRepository,
                maxPurchaseOrderRepository);

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
