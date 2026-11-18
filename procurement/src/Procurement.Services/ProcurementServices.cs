using System;
using System.Collections.Generic;
using System.Linq;
using Procurement.Core.Entities;
using Procurement.Core.Interfaces;
using Procurement.Data;
using Procurement.Data.Repositories;
using Procurement.Data.Secrets;
using Procurement.Engine;
using Procurement.Erp;
using Procurement.Suppliers.Arrow;
using Procurement.Suppliers.Arrow.Http;
using Procurement.Suppliers.DigiKey;
using Procurement.Suppliers.DigiKey.Http;
using Procurement.Suppliers.Farnell;
using Procurement.Suppliers.Farnell.Http;
using Procurement.Suppliers.Mouser;
using Procurement.Suppliers.Mouser.Http;
using Procurement.Suppliers.Other;
using Procurement.Suppliers.Rutronik;
using Procurement.Suppliers.Rutronik.Http;
using Procurement.Suppliers.TME;
using Procurement.Suppliers.TME.Http;

namespace Procurement.Services
{
    /// <summary>
    /// Everything one login needs: repositories, supplier adapters, MAX connector and the engine,
    /// built for a given <see cref="ISessionContext"/>. Moved out of Procurement.UI on 18 nov 2026
    /// (web app plan, phase 1) so the desktop and the web app build exactly the same graph: the
    /// desktop once per run (see Procurement.UI's CompositionRoot), the web app once per request
    /// for the logged-in user. One ProcurementDbContext per instance keeps EF6 change tracking
    /// simple; dispose the instance when done (the web app does so at the end of each request).
    /// </summary>
    public class ProcurementServices : IDisposable
    {
        public ProcurementDbContext DbContext { get; }
        public ISessionContext Session { get; }
        public SecretSettings Secrets { get; }
        public ProcurementEngine Engine { get; }
        public PurchaseRequestRepository PurchaseRequestRepository { get; }
        public SupplierProductMappingRepository SupplierProductMappingRepository { get; }
        public PurchaseOrderRepository PurchaseOrderRepository { get; }
        public PolicyRepository PolicyRepository { get; }
        public SupplierRepository SupplierRepository { get; }
        public ProcurementEventRepository ProcurementEventRepository { get; }
        public IReadOnlyList<ISupplierAdapter> Adapters { get; }
        public MaxErpConnector ErpConnector { get; }
        public MaxOrderRepository MaxOrderRepository { get; }

        /// <summary>
        /// "Ordering.Enabled" from App.config / Web.config (17 nov 2026: order part switched off while
        /// search/select is tested against real data). Read by the host; see <see cref="ReadOrderingEnabled"/>.
        /// </summary>
        public bool OrderingEnabled { get; }

        /// <summary>Missing or unreadable counts as off — safe default.</summary>
        public static bool ReadOrderingEnabled(string configValue) =>
            bool.TryParse(configValue, out var enabled) && enabled;

        /// <summary>
        /// Non-fatal problems found while building the app (so far: a supplier secret this user has
        /// no SecretAccess grant for). The desktop's ShellForm shows them once after startup.
        /// </summary>
        public IReadOnlyList<string> StartupWarnings => _startupWarnings;
        private readonly List<string> _startupWarnings = new List<string>();

        public ProcurementServices(ISessionContext session, bool orderingEnabled)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            OrderingEnabled = orderingEnabled;
            Secrets = new SecretSettings(Session);
            DbContext = new ProcurementDbContext(Session.SharedConnectionString);

            var auditLogger = new DbAuditLogger(DbContext);

            PurchaseRequestRepository = new PurchaseRequestRepository(DbContext, Session);
            SupplierProductMappingRepository = new SupplierProductMappingRepository(DbContext);
            var offerRepository = new SupplierOfferRepository(DbContext);
            var selectionRepository = new SupplierSelectionRepository(DbContext);
            var approvalRepository = new ApprovalRequestRepository(DbContext);
            PurchaseOrderRepository = new PurchaseOrderRepository(DbContext, Session);
            PolicyRepository = new PolicyRepository(DbContext);
            SupplierRepository = new SupplierRepository(DbContext);
            ProcurementEventRepository = new ProcurementEventRepository(DbContext);

            // Blocking on purpose: this runs once per login (desktop) or request (web) and reads a few rows — the same
            // trade-off Program.cs already makes with its own synchronous schema check right before
            // this constructor runs. IsSandbox/UseMockData now come from the
            // Supplier row (editable via Instellingen -> Suppliers) instead of being hardcoded here,
            // so flipping a supplier out of mock mode there actually takes effect on next restart.
            var suppliers = SupplierRepository.GetAllAsync().GetAwaiter().GetResult();
            Supplier FindSupplier(string code) => suppliers.FirstOrDefault(s => s.SupplierCode == code);

            // Supplier credentials come from the SharedSecrets database (SecretSettings, same as
            // UniPro — user request 13 nov 2026), and only for suppliers that actually run in real
            // mode: a mock-mode supplier never uses its credentials, so there's no reason to make
            // every user hold a SecretAccess grant for it. Each read also enforces SecretAccess;
            // a missing grant becomes a startup warning (that supplier then fails with a clear
            // "credentials missing" on first real use) instead of stopping the whole app.
            bool IsReal(Supplier supplier) => supplier != null && !supplier.UseMockData;
            string Secret(Supplier supplier, System.Func<string> read)
            {
                if (!IsReal(supplier)) return null;
                try
                {
                    return read();
                }
                catch (System.UnauthorizedAccessException ex)
                {
                    _startupWarnings.Add(ex.Message);
                    return null;
                }
            }

            var digiKeySupplier = FindSupplier("DIGIKEY");
            var farnellSupplier = FindSupplier("FARNELL");

            var digiKeyOptions = new DigiKeyOptions
            {
                ClientId = Secret(digiKeySupplier, () => Secrets.DigiKeyClientId),
                ClientSecret = Secret(digiKeySupplier, () => Secrets.DigiKeyClientSecret),
                IsSandbox = digiKeySupplier?.IsSandbox ?? true,
                // The 3-legged route for Product Information only works around DigiKey's Sandbox
                // 2-legged bug. In production search/pricing use the normal 2-legged client-credentials
                // token (18 nov 2026), so price lookups don't depend on the Ordering authorization.
                UseThreeLeggedForProductInformation = digiKeySupplier?.IsSandbox ?? true,
                UseMockData = digiKeySupplier?.UseMockData ?? true,
                RefreshToken = Secret(digiKeySupplier, () => Secrets.DigiKeyRefreshToken),
                AccountId = digiKeySupplier?.AccountId,
                ContactName = digiKeySupplier?.ContactName,
                ContactEmail = digiKeySupplier?.ContactEmail,
                ContactTelephone = digiKeySupplier?.ContactTelephone,
                AddressLine1 = digiKeySupplier?.AddressLine1,
                AddressLine2 = digiKeySupplier?.AddressLine2,
                City = digiKeySupplier?.City,
                Province = digiKeySupplier?.Province,
                PostalCode = digiKeySupplier?.PostalCode,
                CountryCode = digiKeySupplier?.CountryCode
            };
            var farnellOptions = new FarnellOptions
            {
                ApiKey = Secret(farnellSupplier, () => Secrets.FarnellApiKey),
                IsSandbox = farnellSupplier?.IsSandbox ?? true,
                UseMockData = farnellSupplier?.UseMockData ?? true,
                // Farnell's Order API "tradeAccount" — confirmed by the user (21 okt 2026) to be
                // their Farnell customer number, reusing the same generic AccountId column DigiKey
                // already populates rather than adding a new one.
                AccountId = farnellSupplier?.AccountId
            };

            // Mouser: ApiKey only (self-service Search API key, no OAuth). TME: Token + HMAC
            // signing secret, now two separately named secrets (TmeToken/TmeApiKey) instead of
            // the old reuse of the ClientId/ClientSecret columns.
            var mouserSupplier = FindSupplier("MOUSER");
            var mouserOptions = new MouserOptions
            {
                ApiKey = Secret(mouserSupplier, () => Secrets.MouserApiKey),
                IsSandbox = mouserSupplier?.IsSandbox ?? true,
                UseMockData = mouserSupplier?.UseMockData ?? true
            };
            var tmeSupplier = FindSupplier("TME");
            var tmeOptions = new TmeOptions
            {
                Token = Secret(tmeSupplier, () => Secrets.TmeToken),
                ApiKey = Secret(tmeSupplier, () => Secrets.TmeApiKey),
                IsSandbox = tmeSupplier?.IsSandbox ?? true,
                UseMockData = tmeSupplier?.UseMockData ?? true
            };

            // Arrow: OAuth2 ClientId/ClientSecret (confirmed, 22 okt 2026) — "voorbereid" per the
            // user's own framing, not yet trusted for real use (see ArrowAdapter's doc comment):
            // auth and order-status are real, but search/pricing/order-placement still refuse to
            // guess an unconfirmed response/request shape even with UseMockData=false. Rutronik:
            // ApiKey only (plain query-string key, no OAuth) — confirmed, fully read-only.
            var arrowSupplier = FindSupplier("ARROW");
            var arrowOptions = new ArrowOptions
            {
                ClientId = Secret(arrowSupplier, () => Secrets.ArrowClientId),
                ClientSecret = Secret(arrowSupplier, () => Secrets.ArrowClientSecret),
                BillToId = arrowSupplier?.AccountId,
                IsSandbox = arrowSupplier?.IsSandbox ?? true,
                UseMockData = arrowSupplier?.UseMockData ?? true
            };
            var rutronikSupplier = FindSupplier("RUTRONIK");
            var rutronikOptions = new RutronikOptions
            {
                ApiKey = Secret(rutronikSupplier, () => Secrets.RutronikApiKey),
                IsSandbox = rutronikSupplier?.IsSandbox ?? true,
                UseMockData = rutronikSupplier?.UseMockData ?? true
            };

            // The HTTP wrappers are cheap to construct (just an HttpClient + options) and are only
            // ever called when UseMockData is false, so they're always built rather than
            // conditionally wired — one less branch to get wrong here.
            var digiKeyHttpClientWrapper = new DigiKeyHttpClientWrapper(digiKeyOptions);
            // Some OAuth providers issue a new refresh token every time the old one is used to get a
            // fresh access token — without persisting that, the rotated value would only live for
            // this process's lifetime, and the next run would fail to refresh with the (by then
            // revoked) token still in the database. Now written to SharedSecrets (whose permission
            // script grants the app's SQL login db_datawriter — the old Procurement_Supplier write
            // sometimes lacked UPDATE rights). Swallowed on failure: that just means "Authorize
            // Ordering..." needs to be run again next time, not a crash worth blocking on.
            digiKeyHttpClientWrapper.OnRefreshTokenRotated = newRefreshToken =>
            {
                try
                {
                    Secrets.SaveDigiKeyRefreshToken(newRefreshToken);
                }
                catch
                {
                    // See comment above.
                }
            };
            var digiKeyAdapter = new DigiKeyAdapter(digiKeyOptions, digiKeyHttpClientWrapper);
            var farnellAdapter = new FarnellAdapter(farnellOptions, new FarnellHttpClientWrapper(farnellOptions));
            var mouserAdapter = new MouserAdapter(mouserOptions, new MouserHttpClientWrapper(mouserOptions));
            var tmeAdapter = new TmeAdapter(tmeOptions, new TmeHttpClientWrapper(tmeOptions));
            var arrowAdapter = new ArrowAdapter(arrowOptions, new ArrowHttpClientWrapper(arrowOptions));
            var rutronikAdapter = new RutronikAdapter(rutronikOptions, new RutronikHttpClientWrapper(rutronikOptions));

            // Distributors with no confirmed public API yet (likely account-/EDI-gated) all share
            // one generic mock adapter (Procurement.Suppliers.Other) instead of near-copies of
            // DigiKeyAdapter — see GenericMockSupplierAdapter's doc comment. Swap an entry out for
            // a dedicated adapter (mirroring DigiKeyAdapter/MouserAdapter) once that distributor's
            // real API/EDI contract is confirmed — as just happened for Arrow and Rutronik, both
            // now built above instead of listed here.
            var genericSupplierDefinitions = new[]
            {
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
                    // No secrets defined for these yet (no confirmed API, mock only) — add them to
                    // SecretSettings.Definitions once one gets a real adapter.
                    IsSandbox = supplier?.IsSandbox ?? true,
                    UseMockData = supplier?.UseMockData ?? true
                };
                return (ISupplierAdapter)new GenericMockSupplierAdapter(def.Code, def.DatasheetHost, options);
            });

            Adapters = new List<ISupplierAdapter> { digiKeyAdapter, farnellAdapter, mouserAdapter, tmeAdapter, arrowAdapter, rutronikAdapter }
                .Concat(genericAdapters)
                .ToList();

            MaxOrderRepository = new MaxOrderRepository(Session);
            var maxVendorPartRepository = new MaxVendorPartRepository(Session);
            var maxPurchaseOrderRepository = new MaxPurchaseOrderRepository(Session, SupplierRepository, PurchaseRequestRepository, MaxOrderRepository);
            ErpConnector = new MaxErpConnector(
                PurchaseRequestRepository,
                PurchaseOrderRepository,
                MaxOrderRepository,
                maxVendorPartRepository,
                SupplierRepository,
                SupplierProductMappingRepository,
                maxPurchaseOrderRepository,
                auditLogger);

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
                PolicyRepository)
            {
                OrderingEnabled = OrderingEnabled
            };
        }

        public void Dispose() => DbContext?.Dispose();
    }
}
