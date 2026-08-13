# Componenteninkoop – Prototype

Windows Forms (.NET Framework 4.8) prototype voor geautomatiseerde componenteninkoop bij DigiKey
en Farnell, volgens [`docs/functionele-specificatie-procurement.md`](docs/functionele-specificatie-procurement.md).
Dit is een **zelfstandig programma** (eigen .exe, geen UniPro-module) dat zijn eigen tabellen in de
gedeelde `Unitron`-database heeft, los van de Bitvavo trading bot elders in deze repository.

Er zijn nog geen echte DigiKey/Farnell API-credentials — de supplier-kant draait in mock-modus,
zodat de rest van de workflow (Sourcing → Selectie → ERP PO → Supplier Order → Confirmation)
end-to-end te doorlopen is. **Inloggen, company-selectie én de purchase requests zelf zijn wél
echt**: login gebruikt dezelfde MAX-infrastructuur (`MaxSQL`, `ExactRMCompanies`) als UniPro, en
open purchase requests komen standaard uit MAX's eigen `Order_Master`/`Part_Master` — zie
[Inloggen en company-selectie](#inloggen-en-company-selectie) en
[Purchase requests uit MAX](#purchase-requests-uit-max) hieronder. Handmatig een testaanvraag
toevoegen (§8.1) blijft ook werken, als aanvulling op de echte MAX-orders.

## Solution-structuur

```
Procurement.sln
src/
  Procurement.Core                POCO's, interfaces, business rules, sessie-abstractie — geen UI/DB-afhankelijkheid
  Procurement.Data                EF6 Code First DbContext, mappings, migrations, repositories
  Procurement.Suppliers.DigiKey   ISupplierAdapter-implementatie voor DigiKey (mock-modus)
  Procurement.Suppliers.Farnell   ISupplierAdapter-implementatie voor Farnell (mock-modus)
  Procurement.Erp                 IErpConnector + MockErpConnector
  Procurement.Engine              ProcurementEngine: orkestreert sourcing/selectie/order
  Procurement.UI                  WinForms-app (startproject) — login, MAX-koppeling, security-stub
tests/
  Procurement.Tests               xUnit-tests, inclusief de rekenvoorbeeld-casus uit spec §7
```

`Procurement.Core`, `Procurement.Data` en `Procurement.Engine` blijven vrij van UI/MAX-specifieke
code (ze kennen alleen `ISessionContext`, niet MAX zelf) — alle MAX50-afhankelijkheid
(`LoginForm`, `SecurityHelper`) zit uitsluitend in `Procurement.UI`.

## Inloggen en company-selectie

`Procurement.UI/Forms/LoginForm.cs` is bewust zo dicht mogelijk tegen UniPro2026's eigen
`UI/Forms/Login.cs` aan gebouwd (zelfde gedrag, niet alleen zelfde look):

1. **Geen wachtwoord** — de gebruikersnaam komt uit de Windows-login (`WindowsIdentity.GetCurrent()`).
2. **Administratie-dropdown**, gevuld vanuit `ExactRMCompanies` via `MaxSQL.GetPrimaryConnection`/
   `GetConnection` (MAX50) — exact dezelfde tabel en bibliotheek als UniPro, maar zonder de
   `WHERE CompanyID IN (...)`-filter die UniPro's eigen `CompanyRepository` gebruikt (die filtert op
   twee specifieke UniPro-companies; hier staan standaard **alle** administraties in de lijst — pas
   `LoginForm.LoadCompanies` aan als dat niet gewenst is).
3. **Testmodus-checkbox**: zet, net als UniPro (`ConnectionStringHelper.WithTestSuffix`), de
   catalog-naam van dit programma's eigen database om naar `Unitron_test` in plaats van `Unitron`.
   Verder verandert er niets.
4. Na een geslaagde login staat alles in `Procurement.Core.Session.ProcurementSession`
   (mirrors UniPro's `AppSession`) en het bijbehorende read-only `ISessionContext`
   (mirrors UniPro's `ISessionContext`/`AppSessionContext`) — inclusief de `AdminConnectionString`
   (per-company MAX-administratie), gereserveerd voor toekomstige `MaxSecurity`-rechtencontroles.

**Company-scoping**: elke `PurchaseRequest` krijgt bij het aanmaken de `CompanyId` van de ingelogde
sessie; het overzichtsscherm en alle lookups filteren daarop (`PurchaseRequestRepository`). Regels,
offers, PO's en supplier-orders zelf hebben géén eigen `CompanyId` — die worden altijd via hun
`PurchaseRequest`/`PurchaseRequestLineId` bereikt, dus scoping loopt daar automatisch mee.
`SupplierPreference`/`PackagingPolicy`/`ApprovalPolicy` blijven **niet** per company gescoped
(gedeelde configuratie) — zeg het als dat wel zou moeten.

**Rechten (nog niet actief)**: `Procurement.UI/Security/SecurityHelper.cs` mirrort UniPro's
`Security/SecurityHelper.cs` (`HasAccess`/`DemandAccess` tegen `MaxSecurity.GetAccess`) zodat een
echte per-scherm/knop rechtencontrole later een kleine wijziging is in plaats van een verbouwing —
maar wordt momenteel nergens aangeroepen; elk scherm is nu nog open voor iedereen die inlogt.

### Niet-geverifieerde aannames — controleer dit voor je build/start

Dit is gebouwd op basis van UniPro2026's brongoede (die is meegeleverd), maar zonder toegang tot de
MAX50-bibliotheek zelf of de echte database, dus het volgende is **aangenomen, niet geverifieerd**:

- **`LicPath`** in `LoginForm.cs` (`\\192.168.0.12\Exact Max\RMServer\LIC`) — 1-op-1 overgenomen
  van UniPro2026's `Login.licPath`. Klopt dit nog?
- **MAXCore/MaxOrderNET/MaxTransNET HintPaths** in `Procurement.UI.csproj` — 1-op-1 overgenomen van
  UniPro2026.csproj (`F:\Software\MAX\MAX Update\...\bin\*.dll`). Moeten op de buildmachine bestaan.
- **Catalog-naam `"Unitron"`** (`ProcurementConnectionStringHelper.SharedCatalogName`) — UniPro's
  eigen equivalent-constante was al geredigeerd/verwijderd in de aangeleverde broncode, dus dit is
  een aanname op basis van hoe je zelf naar de database verwijst, niet uit UniPro's code gehaald.
- **`ExactRMCompanies`-filter**: geen filter (zie boven) — UniPro's eigen `'1001','1012'`-filter is
  bewust *niet* overgenomen.

## Purchase requests uit MAX

`MaxErpConnector` (`Procurement.Erp`) is de standaard `IErpConnector` en haalt open purchase
requests op uit MAX's eigen `Order_Master`/`Part_Master` (via `AdminConnectionString`, de
per-company MAX-administratie die bij het inloggen is opgehaald) — de door de gebruiker aangeleverde
query, met `Part_Master.TYPE_01 IN ('B','D','Y')` vast en `Order_Master.STATUS_10` filterbaar via
de checkboxes "1 - Planned" / "2 - Approved" op het hoofdscherm (standaard: alleen Approved).

**Handmatig opvragen, niet automatisch**: het hoofdscherm laadt bij het openen helemaal niets — pas
een klik op de knop **"Query"** (in het groepsvak "Orders ophalen uit MAX", samen met de status-
checkboxes) start de MAX-query, met een busy-cursor tijdens het laden. Na lokale acties (nieuwe
testaanvraag, sourcing starten, een goedkeuring) wordt het scherm wel ververst, maar enkel met de al
lokaal gesynchroniseerde aanvragen (`PurchaseRequestRepository.GetOpenAsync`) — zonder MAX te raken.

**Extra filters**: naast de status-checkboxes staan twee losse groepsvakken, apart van de overige
actieknoppen. "Due Date Range" filtert op `Order_Master.CURDUE_10` (enkel actief als "Enable" is
aangevinkt). "Filter" is een generieke "Select By"-range (Order Number/Customer/Part) die een
`BETWEEN`-achtige `>=`/`<=` toevoegt op `ORDNUM_10`, `Part_Master.COMCDE_01` resp. `PRTNUM_10` —
Start en/of End mogen leeg blijven voor een open-einde range. Beide filters worden pas toegepast bij
de volgende klik op "Query" (`MaxOrderQueryFilter`, opgebouwd in `MainForm.ApplyFiltersToErpConnector`).

**Kolommen van het hoofdscherm**: Order, Status, Firm, Type, PartID, Rev, Desc1, Desc2, Quantity,
Cost, Cnv, DueDate, Reference, Manufacturing Part, Customer, StockID — 1-op-1 de velden uit de
Order_Master/Part_Master-query (`FRMPLN_10`, `REVLEV_10`, `COST_10`, `CSTCNV_10`, `STK_10`,
`COMCDE_01`, ...). Deze velden zijn puur informatief en worden meegesynchroniseerd naar
`PurchaseRequestLine` (niet gebruikt door de sourcing/matching-logica). `Status` toont hier het ruwe
`Order_Master.STATUS_10` (`PurchaseRequest.MaxOrderStatus`), niet dit prototype's eigen workflow-
status (`PurchaseRequest.Status`, zichtbaar via Order details/Goedkeuringen).

**Hoe dit samenwerkt met de rest van de engine**: `ProcurementEngine`, de approval-flow en het
plaatsen van orders werken volledig in termen van dit prototype's eigen `PurchaseRequest`-tabel
(`CompanyId`-gescheiden, zie boven). In plaats van MAX-orders los daarvan te verwerken, worden ze
bij elke `GetOpenPurchaseRequestsAsync()`-aanroep (dus bij elke klik op "Query") **gesynchroniseerd**:
elke MAX-order zonder bestaande `PurchaseRequest` (gededupliceerd op `ErpRequestNumber` =
`Order_Master.ORDNUM_10`) wordt één keer aangemaakt; bestaat 'm al, dan gebeurt er niets (geen update
van hoeveelheid/status bij wijzigingen in MAX — buiten scope voor dit prototype). Handmatig
toegevoegde testaanvragen (§8.1) staan gewoon naast de gesynchroniseerde MAX-orders in dezelfde
tabel/lijst.

**Bekende datamapping-aanname, graag controleren**: de aangeleverde query heeft geen apart
fabrikant-veld, alleen `Part_Master.VIEWER_01 AS ManufacturingPart` — die wordt gebruikt als
`ManufacturerPartNumber`, met `Manufacturer` leeg. Zonder fabrikantnaam matchen de DigiKey/Farnell
mock-adapters op `Medium` in plaats van `Exact` confidence, wat betekent dat **elke** MAX-order via
het goedkeuringsscherm moet in plaats van automatisch te worden besteld (spec §6 stap 7 filtert op
Exact/Verified). Staat er ergens in `Part_Master` een echt fabrikantveld, laat het weten dan voeg ik
dat toe aan de query/mapping (`MaxOrderRepository`/`MaxErpConnector`).

**Gedeeltelijke ondervanging via `Part_Vendor`**: MAX houdt zelf al bekende koppelingen bij tussen
interne artikelnummers en leverancierscodes, in `dbo.Part_Vendor` (`PRTNUM_07`/`VENID_07`/
`VENPRT_07`). `MaxErpConnector.SyncVendorPartMappingsAsync` leest deze tabel uit (via
`MaxVendorPartRepository`, geschaald tot de artikelen uit de huidige batch open orders) en zet elke
rij met een herkenbare `VENID_07` om naar een `SupplierProductMapping` met `MatchConfidence.Verified`
— precies de plek waar `ProcurementEngine.ResolveOneMappingAsync` al naar kijkt *voordat* de
DigiKey/Farnell-adapters worden aangeroepen. Voor artikelen met een bekende `Part_Vendor`-koppeling
wordt de fuzzy-matching dus overgeslagen en kan de order alsnog automatisch verwerkt worden (spec §6
stap 7), ook zonder een fabrikant-veld.

`VENID_07` is MAX-omgeving-specifiek en staat daarom niet hardcoded in de code, maar op
`Supplier.VendorId` — bewerkbaar via het Instellingen-scherm, tabblad "Suppliers" (§8.6). Bij eerste
opstart wordt dit veld geseed met de waarden die de gebruiker heeft opgegeven: Farnell = `0349`,
DigiKey = `10194`; een handmatige wijziging via Instellingen wordt bij een volgende sync nooit
overschreven. Zoals bij de purchase-request sync geldt ook hier: een bestaande
`SupplierProductMapping` (bv. handmatig gecorrigeerd via het mapping-scherm, §8.5) wordt nooit
overschreven door een latere sync.

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
— er is geen losse `Add-Migration`-stap nodig. Er staat geen statische connection string meer in
`App.config`: die wordt na het inloggen opgebouwd door `ProcurementConnectionStringHelper` uit de
MAX-adminconnectie (zelfde server/auth, andere catalog: `Unitron` of `Unitron_test`). Bij de eerste
start op een lege `Unitron`-database:

1. Wordt het schema aangemaakt op basis van de na het inloggen opgebouwde connection string.
2. Worden de policy-tabellen geseed: `Supplier`/`SupplierCapability` (DigiKey + Farnell, conform
   spec §5), een standaard `SupplierPreference` per leverancier, een default `PackagingPolicy` en
   een default `ApprovalPolicy`.

### Starten

Zet `Procurement.UI` als startproject en start (F5). Eerst verschijnt het inlogscherm
(Windows-gebruiker, administratie-keuze, testmodus — zie
[Inloggen en company-selectie](#inloggen-en-company-selectie)); na een geslaagde login verschijnt
het hoofdscherm (`MainForm`, Purchase Requests-overzicht) met de gekozen administratie in de
titelbalk. Via "Nieuwe testaanvraag toevoegen" kan een testmatige aanvraag worden ingevoerd,
waarna "Sourcing starten" de volledige workflow doorloopt.

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
  je verder bouwt.
- **`LoginForm`/`SecurityHelper` roepen `MaxSQL`/`MaxSecurity` (MAX50) aan met signaturen die zijn
  afgeleid uit hoe UniPro2026 ze gebruikt** (zelfde aanroepen, zelfde argumenten) — niet
  gecontroleerd tegen de daadwerkelijke MAX50-bibliotheek, die hier niet beschikbaar is. Zie
  [Niet-geverifieerde aannames](#niet-geverifieerde-aannames--controleer-dit-voor-je-buildstart)
  hierboven.
- Rechten (`SecurityHelper.HasAccess`/`DemandAccess`) zijn gebouwd maar nog nergens aangeroepen —
  elk scherm is nu nog open voor iedereen die inlogt.
- Valuta: EUR als standaard; het `Currency`-veld is aanwezig maar meerdere valuta's zijn niet
  volledig uitgewerkt (spec §15-aanname).
- Shipment tracking, invoice-verwerking en de echte UniPro-koppeling voor purchase-request-data
  zijn expliciet fase 2 / buiten scope (spec §13) — dat blijft mock, alleen login/company-selectie
  is nu echt.
