# Componenteninkoop – Prototype

Windows Forms (.NET Framework 4.8) prototype voor geautomatiseerde componenteninkoop bij DigiKey,
Farnell en een groeiend aantal andere grote elektronicadistributeurs (Mouser, TME, Arrow, Rutronik,
Avnet/Silica, Karl Kruse, RS Components, Distrelec, Conrad — zie "Suppliers" hieronder), volgens
[`docs/functionele-specificatie-procurement.md`](docs/functionele-specificatie-procurement.md).
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
  Procurement.Suppliers.DigiKey   ISupplierAdapter voor DigiKey (echte HTTP-laag voor zoeken/prijzen/voorraad)
  Procurement.Suppliers.Farnell   ISupplierAdapter voor Farnell/element14 (idem)
  Procurement.Suppliers.Mouser    ISupplierAdapter voor Mouser (idem)
  Procurement.Suppliers.TME       ISupplierAdapter voor TME (idem, HMAC-signing — zie "Suppliers")
  Procurement.Suppliers.Other     Eén gedeelde mock-adapter voor Arrow/Rutronik/Avnet-Silica/Karl Kruse/RS Components/Distrelec/Conrad (nog geen bevestigde API)
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
Omdat MAX-orders na een eerste sync altijd lokaal blijven staan (insert-if-not-exists, zie hierboven),
filtert `MaxErpConnector.GetOpenPurchaseRequestsAsync` het teruggegeven lijstje ook nog eens op de
ordernummers uit de *huidige* MAX-query — anders zou een aanscherping van het filter niets zichtbaars
doen omdat alles wat ooit gesynchroniseerd is gewoon in de lokale tabel blijft staan. Handmatig
toegevoegde testaanvragen (herkenbaar aan de `"TEST-"`-prefix die `NewPurchaseRequestForm` aan
`ErpRequestNumber` geeft) vallen hier buiten en blijven altijd zichtbaar, ongeacht het MAX-filter.
Bewust *niet* `PurchaseRequest.MaxOrderStatus == null` als onderscheid gebruikt: dat veld wordt alleen
(opnieuw) gezet voor een rij die in de *huidige* MAX-batch zit, dus een oudere, al gesynchroniseerde
MAX-order die toevallig een tijd lang buiten elk filter viel, zou anders permanent als "handmatig,
altijd tonen" zijn behandeld — en dus nooit meer gefilterd worden (en ook z'n Desc1/Desc2/Type/enz.
nooit meer ververst krijgen, want die worden alleen bijgewerkt zodra de rij wél weer in een
MAX-resultaat voorkomt). Dat was de daadwerkelijke oorzaak van "de filters doen niets".

De statusbalk toont na een Query ook hoeveel orders de MAX-query zelf teruggaf
(`MaxErpConnector.LastMaxOrderCount`), los van hoeveel er na de lokale merge in de grid staan — handig
om te zien of een filter al op MAX/SQL-niveau iets doet.

**Kolommen van het hoofdscherm**: Order, Status, Firm, Type, PartID, Rev, Desc1, Desc2, Quantity,
Cost, Cnv, DueDate, Reference, Manufacturing Part, Customer, StockID — 1-op-1 de velden uit de
Order_Master/Part_Master-query (`FRMPLN_10`, `REVLEV_10`, `COST_10`, `CSTCNV_10`, `STK_10`,
`COMCDE_01`, ...). Deze velden zijn puur informatief en worden meegesynchroniseerd naar
`PurchaseRequestLine` (niet gebruikt door de sourcing/matching-logica). `Status` toont hier het ruwe
`Order_Master.STATUS_10` (`PurchaseRequest.MaxOrderStatus`). Achteraan staat een aparte kolom
**Workflow**, met dit prototype's eigen voortgangsstatus (`PurchaseRequest.Status`: Pending, Sourcing,
WaitingApproval, ReadyToOrder, Ordered, Exception) — dat is een heel ander veld dan de MAX-`Status`-
kolom en de enige plek op het hoofdscherm waar je ziet of een aanvraag al gesourced/besteld is zonder
Order details of Goedkeuringen te openen. Het regels-grid onder het hoofdscherm (regels van de
geselecteerde aanvraag) gebruikt dezelfde kolomnamen/-breedtes als PartID/Manufacturing Part/etc.,
aangevuld met de workflow-specifieke velden Manufacturer/Packaging/ReelRequirement die niet uit de
MAX-query komen (maar niet de Workflow-kolom, want per regel is dat altijd de status van de hele
aanvraag).

**Statusfilter ("Weergeven")**: een aparte groep checkboxen (los van de MAX-Query-filters ernaast),
één per `PurchaseRequest.Status`-waarde, bepaalt welke Workflow-statussen in het grid getoond worden.
Standaard alles behalve Ordered aangevinkt — een eenmaal daadwerkelijk bestelde aanvraag blijft zo uit
de normale werk-weergave, maar is alsnog op te vragen door het vakje aan te zetten (bv. om via "Order
details" terug te kijken). Dit raakt nooit MAX aan: aan- of uitvinken filtert enkel lokaal al
gesynchroniseerde aanvragen opnieuw (`PurchaseRequestRepository.GetAllAsync()`, client-side gefilterd),
ook na een "Query"-klik. Sourcing starten/Alles sourcen slaat Ordered-aanvragen expliciet over (zou
anders een al geplaatste order stilletjes terugzetten naar ReadyToOrder — elke regel heeft al een
SupplierSelection zonder openstaande exception/goedkeuring, dus de sourcing-uitkomst zou opnieuw
ReadyToOrder worden); Order plaatsen was dat al veilig via de bestaande `HasOrderForLineAsync`-check in
`ProcurementEngine.PlaceOrdersAsync`.

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

Alle eigen tabellen van deze app staan in de **Unitron**-database, op dezelfde SQL Server als
MAX/UniPro (zelfde server/auth, andere catalog — `Unitron` of `Unitron_test` in testmodus, opgebouwd
door `ProcurementConnectionStringHelper` uit de MAX-adminconnectie na het inloggen; er staat geen
statische connection string meer in `App.config`). Elke tabel heeft het prefix **`Procurement_`**
(bv. `Procurement_PurchaseRequest`, `Procurement_Supplier`) zodat ze duidelijk herkenbaar zijn tussen
UniPro's eigen tabellen in diezelfde database en er geen naamsbotsing kan ontstaan.

**Database-schema — expliciet, niet automatisch.** Eerder gebruikte dit project EF6 *automatic*
migrations: het schema werd stilzwijgend aangepast bij elke opstart, zonder dat daar een zichtbare
stap voor nodig was. Dat is bewust uitgezet omdat schema-wijzigingen op een gedeelde database zoals
Unitron eerst beoordeeld moeten kunnen worden voordat ze worden toegepast.

**Belangrijke correctie op een eerdere aanname in deze README**: het aanvankelijke plan was EF6's
*code-based* migrations (`Add-Migration`/`Update-Database` in Visual Studio's Package Manager
Console) — dat bleek niet te werken. Die cmdlets worden geregistreerd via NuGet's klassieke
`install.ps1`/`init.ps1`-scriptmechanisme, en dat mechanisme draait NuGet nooit voor
`PackageReference`-stijl projecten (elk project in deze solution, inclusief `Procurement.Data`) —
vandaar `Add-Migration : The term 'Add-Migration' is not recognized...`. Dit is geen ontbrekend
Visual Studio-onderdeel en geen op te lossen instelling; het is een structurele beperking van EF6's
tooling in combinatie met moderne SDK-stijl `.csproj`-bestanden. Er is dus geen migratiegeschiedenis
of scaffolding meer — schema's worden nu als volgt beheerd:

1. Bij het opstarten controleert `Program.cs` of de kolom `Procurement_PurchaseOrder.SupplierCode`
   al bestaat — niet alleen of de tabel bestaat, want een tabel kan ook bestaan in een *oudere* vorm
   die niet meer bij het huidige model past (zie de aug-2026-wijziging hieronder). Bestaat die kolom
   niet, dan wordt er **niets** automatisch aangepast — in plaats daarvan genereert de app zelf een
   compleet SQL-script (via EF6's eigen `ObjectContext.CreateDatabaseScript()`, dus gegarandeerd
   overeenkomend met het huidige model, zonder dat daar migratie-tooling voor nodig is) en zet dat op
   je bureaublad (`procurement-schema-<tijdstip>.sql`), met een duidelijke melding. Bekijk dat script,
   voer het uit in **SQL Server Management Studio**, en start de app daarna opnieuw.
2. Voor **toekomstige** modelwijzigingen geldt hetzelfde principe: `Program.cs`'s check wordt
   uitgebreid met een kenmerk van de nieuwe vorm (een kolom/tabel die pas in het nieuwe model bestaat),
   zodat een verouderd schema hetzelfde script-en-SSMS-pad doorloopt als een compleet lege database.
   Dit vervangt de eerder voorgestelde `Add-Migration`-stap volledig.
3. Is de check positief, dan slaat de app de scriptgeneratie over en gaat direct verder met het
   seeden van de policy-tabellen (zie hieronder) en opstarten.

**Schone lei voor zowel `Unitron` als `Unitron_test`** (in plaats van de oude data behouden): het
gegenereerde script begint met het opruimen van **alle** `Procurement_`-tabellen — ook de tabellen
waarvan de vorm niet gewijzigd is, want `CreateDatabaseScript()` genereert altijd een volledig
`CREATE TABLE` per tabel in het model (het is geen ALTER/diff-tool), dus elke tabel moet eerst weg om
zonder "already exists"-fouten opnieuw aangemaakt te kunnen worden — inclusief het eerst verwijderen
van alle foreign keys die daarnaar verwijzen, dynamisch opgezocht via `sys.foreign_keys` in plaats van
een handmatig uitgezochte volgorde. Elke stap is voorzien van een `IF OBJECT_ID(...) IS NOT NULL`-
guard, dus hetzelfde script is veilig te draaien tegen `Unitron_test` ook als die database de tabellen
niet (allemaal) heeft. Dit "schone lei"-principe (afgesproken bij de eerdere `Procurement_`-prefix-
rename) wordt nu hergebruikt voor latere schemawijzigingen zolang dit nog een vroege prototype-fase is
zonder productiedata van waarde — elke keer dat het opnieuw gebeurt, staat dat expliciet in de
opstartmelding, nooit stilzwijgend.

Bij een eerste start op een lege database worden na het aanmaken van het schema ook de
policy-tabellen geseed: `Procurement_Supplier`/`Procurement_SupplierCapability` (alle 11 suppliers
hieronder, conform spec §5), een `SupplierPreference` per leverancier, een default `PackagingPolicy`
en een default `ApprovalPolicy` (`Procurement.Data/SeedData.cs`, aangeroepen vanuit `Program.cs` —
dit was voorheen EF6 migrations' `Seed()`-callback).

### Suppliers

Elf distributeurs zijn geseed in `Procurement_Supplier` (`Procurement.Data/SeedData.cs`), elk met een
eigen `SupplierPreference` (spec §5) — hoeveel van elk daadwerkelijk *werkt* verschilt sterk, zie de
drie groepen hieronder. Alleen DigiKey/Farnell staan bij het seeden op `Active=true` in hun
`SupplierPreference`; de overige negen staan bewust op `Active=false` (nooit automatisch meegenomen
in sourcing) totdat iemand ze bewust aanzet via **Instellingen → Supplier preferences** — de meeste
hebben nog geen `VendorId` (MAX `Part_Vendor.VENID_07`) ingevuld, en negen van de elf hebben nog geen
werkende echte API.

**1. Publiek gedocumenteerde, zelfbedienings-API — echte HTTP-laag aanwezig** (`UseMockData=false`
zet de leesacties echt aan zodra credentials bekend zijn):
- **DigiKey** (`Procurement.Suppliers.DigiKey`) — OAuth2 client-credentials.
- **Farnell/element14** (`Procurement.Suppliers.Farnell`) — ApiKey via query-string.
- **Mouser** (`Procurement.Suppliers.Mouser`) — ApiKey via query-string, POST-only.
- **TME** (`Procurement.Suppliers.TME`) — Token + HMAC-SHA1-signing (zie hieronder, minst zeker van
  deze vier).

**2. Grote distributeurs zonder bevestigde publieke API** (`Procurement.Suppliers.Other`, één
gedeelde `GenericMockSupplierAdapter`, altijd mock — zie hieronder): **Arrow, Rutronik, Avnet/Silica,
Karl Kruse, RS Components, Distrelec/Elfa, Conrad Business Supplies**. Deze werken doorgaans alleen
via een aparte account-/EDI-overeenkomst met de distributeur (in plaats van een self-service
API-sleutel zoals bij groep 1) — dat contract/die exacte API-vorm is niet bevestigd, dus is er
bewust geen giswerk gedaan tegen een live account. Credential-velden (ClientId/ClientSecret/ApiKey)
staan al wél klaar in `Procurement_Supplier` zodat ze alvast ingevuld kunnen worden zodra bekend, en
zetten "meedoen in mock-modus" mogelijk voor demo's/sourcing-vergelijkingen over alle distributeurs
heen — maar `UseMockData=false` zetten voor een van deze zeven gooit een expliciete
`SupplierException` ("nog geen bevestigde API/EDI-integratie") in plaats van een halve implementatie
te laten draaien.

**3. Nog te bepalen**: andere grote NL-actieve distributeurs die niet in bovenstaande lijst zitten
kunnen op dezelfde manier toegevoegd worden — ofwel als groep-1-adapter (eigen project +
`Http/*Wrapper.cs`, zie `DigiKeyAdapter` als sjabloon) zodra een publiek gedocumenteerde API bevestigd
is, ofwel als extra `GenericMockSupplierAdapter`-entry in `CompositionRoot.cs` (twee regels: een
`SeedSupplier`-call en een entry in `genericSupplierDefinitions`) als voorbereiding zonder API.

DigiKey/Mouser (ApiKey/ClientId+ClientSecret), Farnell (ApiKey), en TME (Token in `ClientId`,
HMAC-secret in `ClientSecret` — hergebruikt dezelfde twee kolommen in plaats van er nog twee bij te
maken, zie `TmeOptions`) staan versleuteld in `Procurement_Supplier`
(`ClientIdEncrypted`/`ClientSecretEncrypted`/`ApiKeyEncrypted`) in plaats van in `App.config` of code
— zo kan een rotatie/wijziging van een van deze sleutels zonder rebuild of redeploy via
**Instellingen → Suppliers → "Credentials bewerken..."**, en staat er nergens een sleutel in leesbare
vorm in de database of in source control.

**Hoe dat versleutelen werkt** (`Procurement.Core.Security.SecretProtector`): AES-256, met de
sleutel gelezen uit de omgevingsvariabele **`PROCUREMENT_SECRET_KEY`** — die staat dus zelf nergens
in de database of in `App.config`. Zet 'm (dezelfde waarde) op elke machine die deze app draait, bv.
als System Environment Variable of via GPO. Dit is bewust geen volwaardige secrets-manager (geen
rotatie-tooling, geen audit van wie de sleutel gebruikt heeft, zoals Azure Key Vault wél zou geven)
— het lost het concrete probleem op (geen credentials in platte tekst in de database/source control)
tegen vrijwel geen extra bouwkosten. Mocht dit ooit naar een echte secrets-manager moeten, is dat een
kleine, geïsoleerde wijziging (enkel `SecretProtector.GetKey()`), niets in de rest van de app hoeft
dan te veranderen.

Twee dingen die *niet* hetzelfde zijn, ondanks dat ze allebei "de sleutel wijzigen" heten:
- **De credentials zelf wijzigen** (bv. na een DigiKey/Farnell-sleutelrotatie) — dat hoort vaak voor
  te komen, en is precies waarom dit uit `App.config` gehaald is: een paar klikken in Instellingen,
  geen rebuild.
- **`PROCUREMENT_SECRET_KEY` zelf wijzigen** — dat is geen routinehandeling: alle al opgeslagen
  credentials worden daarmee onleesbaar (er is geen sleutel-versionering), dus dat betekent ze
  allemaal opnieuw invoeren via Instellingen. Behandel dat als een incident-response-scenario, niet
  als iets om regelmatig te doen.

**HTTP-laag (leesacties, groep 1)**: `Http/*HttpClientWrapper.cs` in elk van de vier projecten doet
echte HTTP-aanroepen tegen de publiek gedocumenteerde Product Information API van die supplier zodra
`UseMockData=false` staat:
- **DigiKey** (V4 Product Information API): OAuth2 client-credentials token-aanvraag/caching tegen
  `/v1/oauth2/token` (sandbox of productie, afhankelijk van `IsSandbox`), en de vereiste
  `X-DIGIKEY-Client-Id`/`X-DIGIKEY-Locale-*`-headers op elke aanroep. Zoeken via
  `POST /products/v4/search/keyword`, productdetails/prijzen/voorraad/verpakking via
  `GET /products/v4/search/{deelnummer}/productdetails` (DigiKey's V4 API heeft geen aparte
  endpoints per soort gegeven — één aanroep levert alles).
- **Farnell/element14**: geen OAuth, de ApiKey gaat als query-string-parameter mee
  (`callInfo.apiKey`/`callInfo.responseDataFormat`/`storeInfo.id`) op elke aanroep naar
  `GET catalog/products`.
- **Mouser**: ook geen OAuth, ApiKey als query-string-parameter, maar alle acties (ook zoeken) zijn
  POST met een JSON-body naar `POST /api/v1/search/keyword` (op MPN) of
  `POST /api/v1/search/partnumber` (op Mouser's eigen partnummer) — velden als `Availability`/
  `LeadTime`/`Price` komen als vrije tekst terug (bv. `"48000 In Stock"`, `"$0,11"`) en worden
  defensief geparsed (`MouserAdapter.ParseLeadingInt`/`ParseCurrency`) in plaats van blind
  `int.Parse`/`decimal.Parse` te doen.
- **TME**: geen bearer-token of simpele API-key-in-query, maar een OAuth1-achtig
  HMAC-SHA1-signing-schema (`Token` + privé `ApiKey` als HMAC-secret, canonieke string
  `"POST&{url}&{gesorteerde params}"`, base64-signature als extra `ApiSignature`-formveld) — dit is
  het minst zekere onderdeel van groep 1: de signing-aanpak komt uit herinnering van TME's
  documentatie, niet uit een geteste aanroep. Zoeken/productdetails/prijzen zijn bovendien drie
  aparte acties (`Products/Search.json`/`Products/GetProducts.json`/`Products/GetPrices.json`) in
  plaats van één gecombineerde aanroep zoals bij DigiKey/Mouser.
- Elke groep-1-adapter heeft een echte tak (naast de mock-tak) voor
  `SearchProductsAsync`/`GetProductAsync`/`GetAvailabilityAsync`/`GetPricingAsync`/
  `GetPackagingOptionsAsync`, die de JSON-respons parst (`Http/*Dtos.cs`, `Newtonsoft.Json`) naar de
  bestaande `SupplierProduct`/`SupplierPricing`/`SupplierAvailability`/`SupplierPackagingOption`-
  modellen.

**Belangrijk voorbehoud (groep 1)**: dit is geschreven op basis van elke supplier's publiek
gedocumenteerde API-vorm, **niet** getest tegen een echte (sandbox-)aanroep — er zijn nog geen
credentials beschikbaar voor geen van de vier. Veldnamen in `Http/*Dtos.cs`, de response-wrapper-naam
bij Farnell (`premierFarnellPartNumberReturn` vs. varianten — de code probeert alle drie bekende
namen), TME's volledige signing-schema, en de `LocaleSite`/`LocaleLanguage`/`LocaleCurrency`/
`StoreId`/`Country`/`Language`/`Currency`-defaults in de vier Options-klassen moeten allemaal
geverifieerd worden zodra er echt tegen een sandbox getest kan worden — zet dan een supplier op
`UseMockData=false` en test één-voor-één (Zoeken → Product → Prijzen → Voorraad → Verpakking) voordat
er op vertrouwd wordt. TME verdient daarbij extra aandacht: een signature-fout geeft waarschijnlijk
gewoon een nette 401/403 (`SupplierErrorCode.AuthenticationError`), maar het is de enige van de vier
waar de hele auth-aanpak (niet alleen veldnamen) nog moet blijken te kloppen.

**Groep 2 (Arrow/Rutronik/Avnet-Silica/Karl Kruse/RS Components/Distrelec/Conrad)**: geen HTTP-laag
aanwezig, met opzet — zie "Suppliers" hierboven voor de reden. `GenericMockSupplierAdapter` in
`Procurement.Suppliers.Other` retourneert voor al deze zeven altijd deterministische mockdata
(`GenericMockDataProvider`, hetzelfde patroon als `DigiKeyMockDataProvider` maar generiek
geparametriseerd op supplier-code i.p.v. zeven keer bijna-identieke code). Wil je een van deze zeven
echt aansluiten: bouw een eigen project + `Http/*Wrapper.cs` naar het voorbeeld van
`Procurement.Suppliers.DigiKey`, en vervang de betreffende entry in
`CompositionRoot.genericSupplierDefinitions` door een aparte adapter-constructie zoals bij DigiKey.

**Nog niet gebouwd — bestellen (alle elf suppliers)**: `CreateOrderAsync`/`GetOrderStatusAsync`/
`CancelOrderAsync` blijven bij groep 1 in real-mode (`UseMockData=false`) een expliciete
`SupplierException` gooien in plaats van een gok te wagen, en bij groep 2 hoe dan ook (die hebben
sowieso geen echte modus). Elke supplier's Ordering-API is aanzienlijk minder goed gedocumenteerd dan
zijn Product-Information-API (voor zover die al bestaat) en vergt vermoedelijk een aparte
account-goedkeuring — een verkeerde aanname daar plaatst in het ergste geval een echte, verkeerde
bestelling bij een leverancier, wat een heel ander risiconiveau is dan een mislukte leesaanroep.
Bestellen blijft dus mock-only totdat het Ordering-contract van een supplier bevestigd is; zet een
groep-1-supplier dan ook alleen op `UseMockData=false` als je alleen de zoek/prijs/voorraad-kant wilt
testen.

### Starten

Zet `Procurement.UI` als startproject en start (F5). Eerst verschijnt het inlogscherm
(Windows-gebruiker, administratie-keuze, testmodus — zie
[Inloggen en company-selectie](#inloggen-en-company-selectie)); na een geslaagde login verschijnt
het hoofdscherm (`MainForm`, Purchase Requests-overzicht) met de gekozen administratie in de
titelbalk. Via "Nieuwe testaanvraag toevoegen" kan een testmatige aanvraag worden ingevoerd,
waarna "Sourcing starten" een aanvraag zo ver mogelijk brengt — maar bestelt hem nog niet, zie
hieronder.

**Sourcing en order plaatsen zijn losgekoppeld** (spec-correctie, aug 2026): sourcing brengt een
aanvraag maximaal tot `ReadyToOrder` (elke regel heeft een `SupplierSelection`, automatisch bij een
goede match of na handmatige goedkeuring) — er wordt daarbij nog **niets** besteld. Pas een aparte,
expliciete stap, **"Order plaatsen (selectie)"**, plaatst de daadwerkelijke order. Reden: automatisch
meteen bestellen bij elke goede match liet geen ruimte om eerst een reeks aanvragen te verzamelen en
gestructureerd per project/klant te ontvangen. Regels worden daarbij **per leverancier** gebundeld tot
één PO (nooit per project/klant — zo koopt dit bedrijf niet in): één `PurchaseOrder` heeft dus altijd
precies één leverancier, maar kan lijnen bevatten van meerdere verschillende aanvragen als die
dezelfde leverancier delen (`ProcurementEngine.PlaceOrdersAsync`).

**Sourcing/order plaatsen van meerdere aanvragen tegelijk**: elke rij in het grid komt van precies 1
MAX-order (dus altijd 1 regel) — één voor één verwerken is bij veel open orders onwerkbaar. Het
requests-grid is daarom multi-select (Ctrl/Shift-klik); **"Sourcing starten (selectie)"** verwerkt alle
geselecteerde aanvragen na elkaar (nooit parallel, want de app deelt toch al één `ProcurementDbContext`
die alles serialiseert), **"Alles sourcen"** verwerkt de hele grid zonder eerst te hoeven selecteren,
en **"Order plaatsen (selectie)"** plaatst orders voor de geselecteerde aanvragen (regels zonder
`SupplierSelection`, of die al op een PO staan, worden overgeslagen — zie hierboven). Sourcing-acties
tonen na afloop een samenvatting (aantal klaar om te bestellen/wacht op goedkeuring/fout) i.p.v. een
los berichtvenster per aanvraag, en slaan aanvragen over die al `WaitingApproval` zijn — opnieuw
sourcen zou anders een tweede, dubbele `ApprovalRequest` aanmaken voor regels die nog niet besloten
zijn.

### Tests

```powershell
dotnet test tests\Procurement.Tests\Procurement.Tests.csproj
```

`Procurement.Tests` bevat o.a. `SupplierSelectionEngineTests`, die de rekenvoorbeeld-casus uit
spec §7 (12.000 stuks, DigiKey reel 5.000 @ €0,11 / Farnell reel 2.500 @ €0,115 / hypothetische
Supplier C re-reel 4.000 @ €0,105) letterlijk reproduceert, inclusief de twee varianten
(`ORIGINAL_REEL_REQUIRED` vs. `EITHER_REEL`).

**Suppliers** (`tests/Procurement.Tests/Suppliers/`): elke adapter heeft mock-modus-tests naar het
patroon van `DigiKeyAdapterTests`/`FarnellAdapterTests` (zoeken/prijzen/idempotente order-aanmaak).
Twee dingen die verder gaan dan alleen mock-modus, omdat ze precies het minst-geverifieerde gedrag
raken dat mock-modus zelf nooit aanraakt:
- `MouserAdapterTests` test `ParseLeadingInt`/`ParseCurrency` (nu `internal` +
  `InternalsVisibleTo("Procurement.Tests")` in `Procurement.Suppliers.Mouser`) direct tegen
  realistische Mouser-responsformaten (`"48000 In Stock"`, `"$1,234.56"`).
- `TmeSignatureTests` verifieert `TmeHttpClientWrapper.ComputeSignature` (idem `internal` +
  `InternalsVisibleTo`) tegen een onafhankelijk in Python berekende referentie-HMAC-SHA1-waarde —
  bewijst niet dat TME's server de aanroep accepteert, maar wel dat het encode/sorteer/HMAC/base64-
  algoritme zelf klopt en dat een toekomstige regressie hier hard faalt in plaats van stilletjes een
  andere-maar-ook-base64-vormige signature te produceren.
- `MaxPurchaseOrderRepositoryTests` (`tests/Procurement.Tests/Erp/`) test `BuildPurchaseOrderLine`
  (nu `internal static`, `userName` als parameter i.p.v. via `_session` — puur om dit testbaar te
  maken zonder een echte SqlConnection/DbContext) en `BuildConfirmedReference` — precies de twee
  plekken waar dit project al twee keer een verkeerde-maar-plausibele aanname pas bij een echte
  rebuild ontdekte (`FORCUR_10`'s cast, `ORDER_10` leeg op niet-eerste regels). Dit testproject
  verwijst nu ook rechtstreeks naar `MAXCore.dll`/`MaxOrderNET.dll` (zelfde vaste pad als
  `Procurement.Erp.csproj`) — `dotnet test` werkt dus alleen op een machine met de MAX50 SDK
  geïnstalleerd, wat op elke machine waar deze app draait sowieso het geval is.

## Belangrijkste ontwerpkeuzes

- **`ISupplierAdapter`** (`Procurement.Core.Interfaces`) is de enige plek waar leverancierspecifieke
  logica hoort. `ProcurementEngine` kent alleen deze interface — een nieuwe mock-adapter toevoegen
  vereist geen wijziging aan de engine (spec §14, acceptatiecriterium 10).
- **Mock-modus**: alle elf adapters draaien standaard met `UseMockData = true` en retourneren
  deterministische testdata — DigiKey/Farnell/Mouser/TME hebben elk een "bekend" testonderdeel dat de
  spec §7-casus (nagenoeg) reproduceert, elk ander ingevoerd onderdeel (en alle zeven suppliers in
  `Procurement.Suppliers.Other`) krijgt deterministische (dus reproduceerbare) pseudo-realistische
  offers. `IsSandbox`/`UseMockData` en de (versleutelde) credentials komen uit de `Supplier`-tabel
  i.p.v. hardcoded in `CompositionRoot` — zie "Suppliers" hierboven. Voor DigiKey/Farnell/Mouser/TME
  is de echte HTTP-laag (`Http/*HttpClientWrapper.cs`) geïmplementeerd voor de leesacties
  (zoeken/prijzen/voorraad/verpakking), ongetest tegen een echte sandbox — zie "Suppliers" hierboven
  voor het voorbehoud. De overige zeven suppliers hebben geen HTTP-laag (nog geen bevestigde API).
  Bestellen blijft voor alle elf altijd mock-only (zie daar).
- **Business-regels als data**: `SupplierPreference`, `PackagingPolicy` en `ApprovalPolicy` zijn
  SQL Server-tabellen, beheerd via het Instellingen-scherm (§8.6) — niet hard-coded.
- **`PurchaseOrder` = `SupplierOrder`** (spec-correctie, aug 2026): wat eerst twee entiteiten waren
  (een ERP-PO en een los, aan die PO gekoppeld supplier-order) bleek in de praktijk hetzelfde ding —
  in MAX heet dat allebei gewoon "PurchaseOrder", en dit bedrijf koopt altijd per leverancier in, nooit
  per project/klant. Eén PO heeft daarom precies één leverancier, met meerdere `PurchaseOrderLine`s die
  op hun beurt meerdere `PurchaseOrderDelivery`'s (deelleveringen) kunnen hebben.
- **Idempotency** (spec §10): elke PO krijgt bij aanmaak meteen een
  `PROC-{jaar}-{volgnummer}-{SUPPLIERCODE}`-sleutel, ook als de leverancier-adapter geen Ordering
  ondersteunt (dan blijft de PO een handmatig proces, maar heeft 'm alsnog).
- **Audit log**: elke stap van de workflow schrijft een `ProcurementEvent`; `DbAuditLogger` maskeert
  bekende secret-sleutelnamen (`apiKey`, `secret`, `password`, `token`, ...) voordat een payload
  wordt weggeschreven.
- **MAX PO-aanmaak: aanpak nu gegrond in MaxOrderModule's eigen (gedecompileerde) broncode, nog niet
  live getest** — `MaxErpConnector.UseMockPurchaseOrders` (default `true`) bepaalt of "Order
  plaatsen" naar dit prototype's eigen `Procurement_PurchaseOrder` blijft schrijven, of naar een
  echte MAX-PO via `MaxPurchaseOrderRepository`. Wat wij in het requests-grid zien is in MAX een
  Purchase Requisition (PR) — een `Order_Master`-rij — die omgezet moet worden naar een PO-regel en
  daarna verwijderd, anders blijft 'm als open aanvraag in de query staan. Dat gebeurt nu als twee
  stappen per regel, met bevestigde `MaxOrderModule`-methodes (geen tabellen rechtstreeks
  benaderen, dus MAX's eigen validaties/business rules blijven intact):
  1. `AddPODetail(Order_Master, IncOrdRev, CreateHeader, FixVar, RoundType)` maakt de PO-regel aan.
     Voor de eerste regel van een nieuwe PO met `CreateHeader:true` — dat laat `AddPODetail` zelf de
     `Purchase_Order_Code`-header opbouwen uit `Vendor_Master` (Terms/ShipVia/GShip/GTerm/valuta/
     Fobpt komen dus automatisch per leverancier mee, niets hardcoded meer) en het nieuwe
     PO-nummer op die regel zetten; volgende regels van dezelfde PO krijgen `CreateHeader:false` met
     dat PO-nummer en een oplopend regelnummer.
  2. `DeletePurchaseRequisitionLineItem(ordnum, linnum, delnum, out errMsg)` verwijdert de
     oorspronkelijke PR-rij (vandaar de nieuwe velden `PurchaseRequestLine.MaxLineNumber`/
     `MaxDeliveryNumber`, gesynct door `MaxOrderRepository` om deze samenstelde sleutel te bewaren).
     Best-effort: een fout hier wordt gelogd, niet gegooid — de PO-regel uit stap 1 staat er dan al
     correct, dus de hele batch afbreken om één PR-rij zou een slechtere uitkomst zijn dan die ene
     regel voor handmatige opruiming te laten staan.

  **Zet `UseMockPurchaseOrders` nog niet op `false`** — niet omdat de aanpak nog giswerk is (die is
  nu gebaseerd op `MaxOrderModule`'s volledige, aangeleverde broncode, niet meer op één geïsoleerd
  voorbeeld), maar omdat nog niets hiervan tegen een echte MAX-administratie getest is. Bevestigd via
  de volledige module-code: `GetErrors()` is publiek (gebruikt voor foutmeldingen na een mislukte
  `AddPODetail`), en een PR-rij gebruikt `"00"` voor zowel `LINNUM_10` als `DELNUM_10` (niet `"01"`
  zoals eerst aangenomen). Zie de class-comment van `MaxPurchaseOrderRepository` voor de resterende
  openstaande punten: `AssignPRsToPO` (en de verwante bulkmethode `AssignPONumber`) lijken mogelijk
  een correctere, atomaire aanpak (PR direct ombouwen naar PO-regel i.p.v. nieuw aanmaken + apart
  verwijderen), maar `OrderAssign`'s volledige veldenoverzicht, `TargetOrder`'s exacte gedrag, en of
  een eigen hoeveelheid meegegeven kan worden (nodig na order-multiples/MOQ-afronding) zijn niet
  bevestigd; de betekenis van `AddPODetail`'s `FixVar`/`RoundType`-parameters staat nog niet vast.
  Test dit bij voorkeur eerst één keer tegen een MAX test-/sandbox-administratie voordat het tegen
  een live administratie draait.

- **Orderbevestiging verwerken** (MainForm-knop, `ProcurementEngine.ProcessOrderConfirmationsAsync`):
  haalt voor elke PO die nog op een leveranciersbevestiging wacht (`PurchaseOrderRepository.
  GetAwaitingConfirmationAsync`, alleen suppliers met `OrderStatus`-capability en een
  `SupplierOrderNumber`) de status op via `ISupplierAdapter.GetOrderStatusAsync`, werkt lokaal altijd
  de `PurchaseOrderLine`-bevestigingsvelden bij (`ConfirmedQuantity`/`ConfirmedUnitPrice`/een
  `PurchaseOrderDelivery`), en toont enkel de regels die afwijken (aantal, prijs, of een bevestigde
  leverdatum later dan gevraagd) in een apart venster — alles wat zonder afwijking binnenkomt wordt
  stilzwijgend verwerkt, geen los bevestigingsvenster per order.

  In real-mode (`UseMockPurchaseOrders=false`) schrijft dit ook drie specifieke MAX-velden — door de
  gebruiker zelf opgegeven, niet afgeleid uit gedecompileerde bron zoals de rest van deze sectie:
  - `Purchase_Order_Code.CONFRM_16` (char(15)): het SalesOrder-/confirmation-nummer van de
    leverancier.
  - `Order_Master.ORDREF_10` (char(25)) per regel: krijgt een `"O "`-prefix (bevestigd zoals besteld)
    of `"OP "`-prefix (bevestigde leverdatum wijkt af) vóór de bestaande tekst, afgekapt op 25 tekens
    — idempotent, een latere herverwerking vervangt een eerdere `O`/`OP`-prefix in plaats van er nóg
    een voor te plakken (`MaxPurchaseOrderRepository.BuildConfirmedReference`, met unit tests).
  - `Order_Master.CURDUE_10`: bijgewerkt naar de bevestigde leverdatum, alleen bij afwijking.

  Dit is de eerste plek in dit project die een **bestaande** MAX-rij wijzigt (`ChangePOHeading`/
  `ChangePODetail`) in plaats van alleen nieuwe rijen toe te voegen (`AddPODetail`) — dus een verkeerd
  veld raakt hier in potentie een al geplaatste order, in plaats van alleen een nieuw aangemaakte rij.
  Bewust smal gehouden: alleen de drie velden hierboven worden ooit aangepast, de rest van de
  MAX-rij wordt ongewijzigd teruggeschreven. `ChangePODetail`'s eigen interne herberekeningslogica bij
  een `CURDUE_10`-wijziging (bv. rond `DUEQTY_10`/`Requirement_Detail`) is niet losgetraceerd uit de
  sterk geobfusceerde gedecompileerde bron — de gebruiker heeft bevestigd dat dat voor dit gebruik
  acceptabel is, o.a. omdat de dagelijkse MRP-run `Requirement_Detail` sowieso weer bijwerkt.

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
