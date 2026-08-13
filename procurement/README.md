# Componenteninkoop – Prototype

Windows Forms (.NET Framework 4.8) prototype voor geautomatiseerde componenteninkoop bij DigiKey
en Farnell, volgens [`docs/functionele-specificatie-procurement.md`](docs/functionele-specificatie-procurement.md).
Dit is een op zichzelf staande solution, los van de Bitvavo trading bot elders in deze repository.

Er is nog geen live koppeling met UniPro en geen echte DigiKey/Farnell API-credentials — de
ERP-kant en de supplier-kant draaien beide in mock-modus, zodat de volledige workflow (Purchase
Request → Sourcing → Selectie → ERP PO → Supplier Order → Confirmation) end-to-end te doorlopen is
met testdata.

## Solution-structuur

```
Procurement.sln
src/
  Procurement.Core                POCO's, interfaces, business rules — geen UI/DB-afhankelijkheid
  Procurement.Data                EF6 Code First DbContext, mappings, migrations, repositories
  Procurement.Suppliers.DigiKey   ISupplierAdapter-implementatie voor DigiKey (mock-modus)
  Procurement.Suppliers.Farnell   ISupplierAdapter-implementatie voor Farnell (mock-modus)
  Procurement.Erp                 IErpConnector + MockErpConnector
  Procurement.Engine              ProcurementEngine: orkestreert sourcing/selectie/order
  Procurement.UI                  WinForms-app (startproject)
tests/
  Procurement.Tests               xUnit-tests, inclusief de rekenvoorbeeld-casus uit spec §7
```

`Procurement.Core`, `Procurement.Data` en `Procurement.Engine` zijn bewust vrij van UI/ERP-specifieke
code, zodat ze later grotendeels 1-op-1 als UniPro-module kunnen worden hergebruikt — alleen
`Procurement.UI` en `Procurement.Erp` zouden dan wezenlijk veranderen.

## Bouwen en draaien

Dit prototype target `net48` (.NET Framework 4.8) met WinForms en EF6, en **draait alleen op
Windows** — vereist Visual Studio 2019/2022 met de ".NET desktop development"-workload, of de
.NET Framework 4.8 Developer Pack + MSBuild.

```powershell
# Vanuit de procurement/-map
nuget restore Procurement.sln    # of: msbuild -t:restore Procurement.sln
msbuild Procurement.sln
```

Of open `Procurement.sln` in Visual Studio en bouw vanuit daar.

### Database

`Procurement.UI` gebruikt EF6 **automatic migrations** (`src/Procurement.Data/Migrations/Configuration.cs`)
— er is geen losse `Add-Migration`-stap nodig. Bij de eerste start:

1. Wordt de database aangemaakt op basis van de connection string `ProcurementDbContext` in
   `src/Procurement.UI/App.config` (standaard: LocalDB, `(localdb)\MSSQLLocalDB`).
2. Worden de policy-tabellen geseed: `Supplier`/`SupplierCapability` (DigiKey + Farnell, conform
   spec §5), een standaard `SupplierPreference` per leverancier, een default `PackagingPolicy` en
   een default `ApprovalPolicy`.

Pas de connection string aan in `App.config` om een andere (LocalDB of volwaardige) SQL
Server-instantie te gebruiken.

### Starten

Zet `Procurement.UI` als startproject en start (F5). Het hoofdscherm (`MainForm`,
Purchase Requests-overzicht) verschijnt; via "Nieuwe testaanvraag toevoegen" kan een testmatige
aanvraag worden ingevoerd, waarna "Sourcing starten" de volledige workflow doorloopt.

### Tests

```powershell
dotnet test tests\Procurement.Tests\Procurement.Tests.csproj
```

`Procurement.Tests` bevat o.a. `SupplierSelectionEngineTests`, die de rekenvoorbeeld-casus uit
spec §7 (12.000 stuks, DigiKey reel 5.000 @ €0,11 / Farnell reel 2.500 @ €0,115 / hypothetische
Supplier C re-reel 4.000 @ €0,105) letterlijk reproduceert, inclusief de twee varianten
(`ORIGINAL_REEL_REQUIRED` vs. `EITHER_REEL`).

## Belangrijkste ontwerpkeuzes

- **`ISupplierAdapter`** (`Procurement.Core.Interfaces`) is de enige plek waar leverancierspecifieke
  logica hoort. `ProcurementEngine` kent alleen deze interface — een nieuwe mock-adapter toevoegen
  vereist geen wijziging aan de engine (spec §14, acceptatiecriterium 10).
- **Mock-modus**: `DigiKeyAdapter`/`FarnellAdapter` draaien met `UseMockData = true` en retourneren
  deterministische testdata (`DigiKeyMockDataProvider`/`FarnellMockDataProvider`) — één "bekend"
  testonderdeel reproduceert de spec §7-casus exact, elk ander ingevoerd onderdeel krijgt
  deterministische (dus reproduceerbare) pseudo-realistische offers. De echte HTTP-laag
  (`Http/*HttpClientWrapper.cs`) is als stub aanwezig met een `NotImplementedException` en een
  `TODO`, zodat alleen die klasse hoeft te worden ingevuld zodra er echte credentials zijn.
- **Business-regels als data**: `SupplierPreference`, `PackagingPolicy` en `ApprovalPolicy` zijn
  SQL Server-tabellen, beheerd via het Instellingen-scherm (§8.6) — niet hard-coded.
- **Idempotency** (spec §10): elke supplier-order krijgt een `PROC-{jaar}-{volgnummer}-{SUPPLIERCODE}`
  sleutel; `ProcurementEngine` checkt eerst of er al een `SupplierOrder` bestaat voor de combinatie
  ErpPoNumber+SupplierCode voordat een nieuwe wordt geplaatst.
- **Audit log**: elke stap van de workflow schrijft een `ProcurementEvent`; `DbAuditLogger` maskeert
  bekende secret-sleutelnamen (`apiKey`, `secret`, `password`, `token`, ...) voordat een payload
  wordt weggeschreven.

## Bekende beperkingen van dit prototype

- Er is **geen Windows/.NET Framework-toolchain beschikbaar in de omgeving waarin dit is
  gegenereerd** (Linux-container zonder `dotnet`/MSBuild/Mono) — de code is met zorg geschreven
  tegen de EF6/WinForms/.NET Framework 4.8 API's, maar is **niet gecompileerd of getest** in deze
  sessie. Doe een eerste `msbuild`/`dotnet test`-run op een Windows-machine met Visual Studio voor
  je verder bouwt, en verwacht een enkele kleine build-fix (bv. een ontbrekende `using`).
- Eén gebruiker, geen rollen-/rechtenmodel (spec §15-aanname).
- Valuta: EUR als standaard; het `Currency`-veld is aanwezig maar meerdere valuta's zijn niet
  volledig uitgewerkt (spec §15-aanname).
- Shipment tracking, invoice-verwerking en de echte UniPro-koppeling zijn expliciet fase 2 /
  buiten scope (spec §13).
