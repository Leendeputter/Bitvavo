# Componenteninkoop – Prototype

Windows Forms (.NET Framework 4.8) prototype voor geautomatiseerde componenteninkoop bij DigiKey,
Farnell en een groeiend aantal andere grote elektronicadistributeurs (Mouser, TME, Arrow, Rutronik,
Avnet/Silica, Karl Kruse, RS Components, Distrelec, Conrad — zie "Suppliers" hieronder), volgens
[`docs/functionele-specificatie-procurement.md`](docs/functionele-specificatie-procurement.md).
Dit is een **zelfstandig programma** (eigen .exe, geen UniPro-module) dat zijn eigen tabellen in de
gedeelde `Unitron`-database heeft, los van de Bitvavo trading bot elders in deze repository.

Farnell/Mouser/TME en de zeven groep-2-distributeurs hebben nog geen echte API-credentials — die
supplierkant draait in mock-modus. **DigiKey heeft wél echte sandbox-credentials** (zie "Status"
hieronder voor hoe ver dat staat). **Inloggen, company-selectie én de purchase requests zelf zijn
wél echt**: login gebruikt dezelfde MAX-infrastructuur (`MaxSQL`, `ExactRMCompanies`) als UniPro, en
open purchase requests komen standaard uit MAX's eigen `Order_Master`/`Part_Master` — zie
[Inloggen en company-selectie](#inloggen-en-company-selectie) en
[Purchase requests uit MAX](#purchase-requests-uit-max) hieronder. Handmatig een testaanvraag
toevoegen (§8.1) blijft ook werken, als aanvulling op de echte MAX-orders.

## Status (laatst bijgewerkt: 19 okt 2026)

**Werkt en is bevestigd tegen de echte DigiKey-sandbox:**
- Product Information V4 (zoeken/prijzen/voorraad/verpakking) — `UseMockData=false` getest, inclusief
  het eerdere 403-probleem (DigiKey-portal-desync, door DigiKey zelf opgelost na contact).
- **Ordering v3 is nu end-to-end getest en werkend tegen de echte sandbox** (19 okt 2026): via
  `Procurement.Tools.DigiKeyOrderTester` (buiten MAX om, zie hieronder) succesvol een testorder
  geplaatst (`DK-019085`, 1x, €3,00) — DigiKey gaf `SalesOrderId = 12345` terug (een normaal,
  positief ordernummer, dus niet de `0`/`-1`-specialgevallen). De 3-legged OAuth-flow
  (`DigiKeyOrderingAuthorizer`, Callback URL `https://localhost`) werkt; DigiKey roteert het
  refresh-token bij (vrijwel) elke token-ververing.
- **Nog niet getest via de normale UI**: alleen het losse tooltje is geprobeerd, bewust, omdat er geen
  MAX-testomgeving is en de normale "Order plaatsen"-knop altijd ook een echte PO naar MAX schrijft.

**Bekende frictie om bij een volgende poging rekening mee te houden:**
- De SQL-login achter `PROCUREMENT_TEST_CONNECTION_STRING` heeft **geen UPDATE-rechten** op
  `Procurement_Supplier` — een refresh-token-rotatie kan daardoor niet automatisch worden opgeslagen.
  Het tooltje print in dat geval een kant-en-klaar, al-versleuteld `UPDATE`-statement dat je zelf in
  SSMS tegen de `Unitron`-database moet plakken; sla je dat over, dan faalt de **volgende** run met
  DigiKey's 401 "Invalid RefreshToken" (het dan verouderde token staat nog in de database) en moet
  "Ordering autoriseren..." opnieuw. Permanente oplossing: UPDATE-rechten op die ene tabel regelen
  voor die login, of consequent na elke run het geprinte statement draaien.
- **`GetOrderStatusAsync` is nu ook end-to-end getest en werkend tegen de echte sandbox** (19 okt
  2026): geïmplementeerd tegen **twee** confirmed specs (OrderStatus v4 en Order Management v1).
  OrderStatus v4 gaf een 403 ("not authorized") tegen de sandbox ondanks Enabled in het portal —
  vermoedelijk hetzelfde portal/backend-desyncprobleem als destijds bij Product Information V4.
  **Order Management v1 wordt daarom actief gebruikt** (3-legged, zelfde token als Ordering) en gaf via
  `Procurement.Tools.DigiKeyOrderTester status 12345` succesvol `Status=Confirmed` terug, met als
  bevestigde trade-off `ConfirmedAt`/`ConfirmedUnitPrice` altijd leeg (die velden bestaan niet in
  Order Management's respons). De teruggekomen regel (`296-1005-5-ND`, 1000 stuks) kwam niet overeen
  met de zelf geplaatste order (`DK-019085`, 1x) — vermoedelijk vaste sandboxtestdata, net als bij
  Product Information, geen fout. `CancelOrderAsync` blijft mock-only (nog geen contract aangeleverd).
- De normale "Order plaatsen"-knop (MainForm) zelf is dus nog niet in real-mode beproefd — alleen de
  onderliggende `DigiKeyAdapter.CreateOrderAsync` via het losse tooltje.

**MAX PO-aanmaak: nu actief in eerste echte test** (19 okt 2026): `MaxErpConnector.
UseMockPurchaseOrders` staat voortaan standaard op `false` — "Order plaatsen" schrijft dus een echte
PO via `MaxPurchaseOrderRepository.AddPODetail` in plaats van naar dit prototype's eigen
`Procurement_PurchaseOrder`. Er is geen MAX-testomgeving, dus bewust eerst alleen uitgeprobeerd met
een handmatige testaanvraag (§8.1) — die krijgt nooit een `MaxLineNumber`, waardoor zowel de
PR-existence-check als de PR-verwijdering vanzelf no-oppen (zie "Nog niet gebouwd/MAX PO-aanmaak"
hieronder voor de precieze reden). Nog niet daadwerkelijk via de UI uitgevoerd/geverifieerd in MAX —
dat is de eerstvolgende concrete stap. Let op: dezelfde vlag zet ook "Orderbevestiging verwerken"'s
echte MAX-schrijfkant aan.

**OrderStatus v4 opnieuw geprobeerd en weer teruggedraaid, zelfde dag (20 okt 2026)**: na DigiKey's
bevestiging dat de eerdere 403 hun bekende, no-ETA Sandbox 2-legged-bug was (zie verderop), leek het
voor de hand liggend dat 3-legged (via `DigiKeyOptions.UseThreeLeggedForProductInformation`, die
`ISupplierHttpClient.GetAsync` deelt met Product Information) die 403 ook voor OrderStatus v4 zou
oplossen. `GetOrderStatusAsync`'s aanroep is daarom tijdelijk omgezet naar
`GetOrderStatusViaOrderStatusApiAsync` en getest met `Procurement.Tools.DigiKeyOrderTester status 12345`
(dezelfde SalesOrderId die Order Management v1 al succesvol teruggaf). Resultaat: de 403 was inderdaad
weg (3-legged werkt, DigiKey's uitleg klopt) — maar het endpoint gaf in plaats daarvan een gewone
**404 "resource not found"** voor die echte, bestaande order. Dat is dus geen auth-probleem meer, maar
een kennelijk permanent verschil tussen DigiKey's twee sandbox-implementaties: Order Management v1's
sandbox geeft blijkbaar altijd wél iets terug (canned testdata, los van de werkelijke order), terwijl
OrderStatus v4's sandbox zelf geplaatste testorders kennelijk helemaal niet kent. Geen codefout, geen
oplosbaar auth-probleem — `GetOrderStatusAsync` is dezelfde dag teruggezet naar
`GetOrderStatusViaOrderManagementApiAsync`. `GetOrderStatusViaOrderStatusApiAsync` blijft staan (niet
verwijderd) mocht DigiKey's sandbox dit ooit anders doen.

Dit lost het eerder gemelde "ConfirmedQuantity niet gevuld"-probleem dus niet op — dat blijft een
apart, nog openstaand punt: Order Management v1's sandbox geeft vaste/canned testdata terug
(`296-1005-5-ND`, 1000 stuks, ongeacht welke order je opvraagt) die niet overeenkomt met een echt
geplaatste order, waardoor de exacte `SupplierPartNumber`-match in
`PurchaseOrderRepository.ApplyOrderConfirmationAsync` niets vindt en `ConfirmedQuantity` stilletjes
leeg blijft. Dit is een DigiKey-sandbox-testdata-beperking, geen bug in deze app — pas opnieuw te
beoordelen zodra er tegen een echte (niet-sandbox) omgeving getest kan worden.

**DigiKey support bevestigt de oorzaak van de 403's (20 okt 2026)**: een bekende, no-ETA bug in hun
Sandbox-omgeving specifiek voor de **2-legged** OAuth2-flow ("client credentials not authorized").
3-legged werkt in Sandbox wel gewoon. DigiKey's eigen advies: voor Product Information V4 (de API die
dit feitelijk raakt — search/pricing) naar Production overstappen als 2-legged de voorkeur heeft, of
anders 3-legged gebruiken. Voor Ordering was 3-legged al verplicht en dus al in gebruik; dit verklaart
ook meteen de eerdere OrderStatus v4 403 (die loopt óók via 2-legged client credentials) — geen apart
mysterie, dezelfde bug.

**Experiment: `DigiKeyOptions.UseThreeLeggedForProductInformation`** (true by default, okt 2026) —
in plaats van naar Production over te stappen (nieuwe DigiKey-registratie, apart ClientId/Secret,
zorgvuldig gescheiden houden van de sandbox-bestelflow) eerst geprobeerd of de **bestaande** Ordering-
RefreshToken ook Product Information V4 autoriseert. `DigiKeyHttpClientWrapper.AttachSearchHeadersAsync`
kiest op basis van deze vlag tussen het normale 2-legged client-credentials-token en het 3-legged
Ordering-token — verder geen enkele aanroep in `DigiKeyAdapter` hoeft te wijzigen, dezelfde
`GetAsync`/`PostAsync` als altijd. Nog niet apart getest voor Product Information zelf (wel al indirect
bevestigd bruikbaar: de 403 verdween voor OrderStatus v4 toen die hierdoor via 3-legged ging liep, zie
hierboven — dus de bestaande RefreshToken autoriseert in elk geval iets buiten Ordering zelf om). Als
dit voor Product Information ook werkt: geen Production-app nodig. Als DigiKey's token-endpoint Product
Information-scopes niet onder de Ordering-consent toestaat: vlag terugzetten op `false` (geen andere
codewijziging) en alsnog de Production-route uitwerken.

Eén trade-off om in gedachten te houden zolang deze vlag aan staat: Product Information's
beschikbaarheid hangt dan af van dezelfde RefreshToken/consent als Ordering — nu zijn dat twee
onafhankelijke faalpaden, hierna één. Als "Ordering autoriseren..." nog nooit gedraaid is
(RefreshToken leeg), faalt voortaan ook een gewone product search, niet alleen het plaatsen van een
order.

**Les uit deze sessie, voor toekomstige schema-wijzigingen**: elke nieuwe kolom heeft in
`Program.cs`'s schema-existence-check een **eigen** `sys.columns`-probe nodig, ook als een andere
kolom op dezelfde tabel al gecontroleerd wordt — een kolom die in een latere, aparte commit is
toegevoegd (`RefreshTokenEncrypted` na `AccountId`) werd anders niet gedetecteerd op een database die
de eerdere kolom al wél had, met een "Invalid column name"-crash bij het seeden tot gevolg (inmiddels
gefixt).

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
een klik op de knop **"Query"** (in het groepsvak "Fetch Orders from MAX", samen met de status-
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

**Due Date Range/Select By gingen verloren bij het omzetten van een "Display"-checkbox** (gebruiker,
okt 2026): die checkboxen roepen `LoadLocalRequestsAsync` aan, dat — anders dan "Query" — nooit MAX
raakt en dus ook nooit `GetOpenPurchaseRequestsAsync`'s eigen filtering doorloopt; het liet simpelweg
alle lokaal gesynchroniseerde aanvragen weer zien die bij de huidige statuscheckboxen pasten, ongeacht
een actief Due Date-/Select By-filter — vandaar dat "bijna alles" terugkwam zodra je een vinkje
omzette. `MainForm.ApplyRangeAndDueDateFilters` bouwt dat filter nu ook lokaal na (zelfde
velden/vergelijking als de MAX-query, client-side toegepast op de al-geladen lijst) en wordt nu ook
door `LoadLocalRequestsAsync` aangeroepen — met dezelfde uitzondering voor handmatige testaanvragen
als hierboven.

De statusbalk toont na een Query ook hoeveel orders de MAX-query zelf teruggaf
(`MaxErpConnector.LastMaxOrderCount`), los van hoeveel er na de lokale merge in de grid staan — handig
om te zien of een filter al op MAX/SQL-niveau iets doet.

**Kolommen van het hoofdscherm**: Order, Workflow, Status, Firm, Type, PartID, Rev, Desc1, Desc2,
Quantity, Cost, Cnv, DueDate, Reference, Manufacturing Part, Customer, StockID zijn 1-op-1 de velden
uit de Order_Master/Part_Master-query (`FRMPLN_10`, `REVLEV_10`, `COST_10`, `CSTCNV_10`, `STK_10`,
`COMCDE_01`, ...), op **Workflow** na. Deze velden zijn puur informatief en worden meegesynchroniseerd
naar `PurchaseRequestLine` (niet gebruikt door de sourcing/matching-logica). Direct na StockID staat
**Rec. Vendor** (`RecVendor` in de code, hernoemd vanuit `VendorID` op user request, 20 okt 2026) —
`Part_Master.RECVEN_01`, de voorkeursleverancier van het artikel, bevestigd door de
gebruiker (okt 2026). Anders dan de velden ervoor is dit géén nieuwe kolom op `PurchaseRequestLine`:
een nieuwe EF-kolom triggert de schema-regen die ook Supplier-credentials wist (zie "Les uit deze
sessie" verderop), dus net als bij PO DueDate hierboven wordt dit live en gebatcht opgehaald
(`MaxOrderRepository.GetPreferredVendorIdsByPartIdsAsync`, per PartID) in plaats van lokaal
opgeslagen. `Status` toont hier het
ruwe `Order_Master.STATUS_10` (`PurchaseRequest.MaxOrderStatus`). **Workflow**, met dit prototype's
eigen voortgangsstatus (`PurchaseRequest.Status`: Pending, Sourcing, WaitingApproval, ReadyToOrder,
Ordered, Exception), staat bewust vooraan naast Order — een heel ander veld dan de MAX-`Status`-kolom
en de plek op het hoofdscherm waar je in één oogopslag ziet of een aanvraag al gesourced/besteld is
zonder Order details of Goedkeuringen te openen. Achteraan staan vier kolommen die *niet* uit de
oorspronkelijke MAX-aanvraag komen maar uit de gekozen `SupplierOffer` (via de actieve
`SupplierSelection` voor die regel, spec correctie sep 2026): **Selected Supplier/Price/Qty/DueDate**
— leeg zolang er nog geen selectie is, en bewust apart van (niet in plaats van) de oorspronkelijke
Quantity/Cost/DueDate hierboven, want dat blijft tonen wat er in MAX oorspronkelijk aangevraagd is.
**Onderste grid vervangen door offers, niet langer "regels"** (user feedback, 20 okt 2026 — "de regel
die in het bovenste grid wordt geselecteerd wordt hetzelfde weergegeven in het onderste grid [...] ik
zou het logischer vinden als onderaan dan direct de offers worden weergegeven [...] met de optie om
daar het gewenste offer te kiezen"): het oude onderste grid toonde de regel(s) van de geselecteerde
aanvraag — maar elke MAX-PR/PL heeft er altijd precies één, dus dat was vrijwel een letterlijke
herhaling van de bovenste rij. `MainForm.LoadOffersForSelectedRequestAsync` toont daar nu in plaats
daarvan rechtstreeks de offers (`ProcurementEngine.GetOffersForLineAsync`) van die ene regel — dezelfde
kolommen als het oude "Offers bekijken"-scherm (Supplier, Supplier Part Number, Match, Quantity, Unit
Price, Currency, Packaging, Reel, Landed Cost, Lead Time (Working Days), Available Qty, Due Date — de
laatste twee kolomnamen op verzoek aangepast, zie verderop), plus een **"Select"**-checkboxkolom
vooraan (hernoemd vanuit "Kiezen", smal gehouden i.p.v. Fill-breed net als Currency — user request, 20
okt 2026): de huidig geselecteerde offerte staat aangevinkt, een ander vakje aanvinken kiest meteen die
offerte (`ProcurementEngine.SelectOfferManuallyAsync`) en ververst beide grids. Het losse "Offers
bekijken"-scherm (`OfferComparisonForm`, met zijn verplichte reden-tekstveld-vóór-kiezen-flow) is
hiermee overbodig geworden en verwijderd — zie "Verplichte reden bij handmatige keuze vervallen"
hieronder voor waarom die eis zelf ook is losgelaten, niet alleen het scherm errond.

**Deselecteren kan ook** (zelfde user feedback, 20 okt 2026): aanvinken zet de workflowstatus naar
ReadyToOrder zodra de regel een selectie heeft (bevestigd gedrag, ongewijzigd) — maar het vakje van de
al-gekozen offerte uitvinken deed voorheen zelf niets (enkel een refresh die het vakje terugzette naar
aangevinkt). `ProcurementEngine.DeselectOfferAsync` (nieuw) verwijdert de `SupplierSelection`-rij(en)
voor die regel volledig (geen "geen offerte"-representatie mogelijk voor het verplichte
`SelectedOfferId`-veld, en het audit-log — een apart, append-only logboek — bewaart toch al wie wat
wanneer koos) en zet de aanvraag terug naar **Sourcing**. Geblokkeerd zodra de aanvraag al Ordered is
(er bestaat dan al een echte ERP-PO; deselecteren zou die toch niet ongedaan maken).

**Testaanvragen niet meer standaard zichtbaar** (zelfde user feedback: "dit zijn uitzonderingen, die
wil ik niet standaard zien"): een nieuwe checkbox **"Show Test Requests"** in de "Display"-groep
(hernoemd vanuit "Weergeven" — zie "Alle UI-labels omgezet naar Engels" verderop), uit by default.
Handmatige testaanvragen ("TEST-"-prefix) werden tot nu toe altijd getoond, ongeacht de
status-/Due Date-/Select By-filters (ze waren nooit onderdeel van een MAX-query, dus die filters zouden
ze anders onterecht lijken te "verbergen") — dit nieuwe vakje overstemt die uitzondering juist weer: uit
betekent nu echt niet zichtbaar, ook niet via de andere filters.

**Verplichte reden bij handmatige keuze vervallen** (zelfde user feedback): `SelectOfferManuallyAsync`
eiste tot nu toe een niet-lege `reason` (gooide anders een `ArgumentException`) — een bewuste
audit-eis uit spec §8.2. Met offers nu rechtstreeks via een checkbox te kiezen in plaats van via een
apart scherm met tekstveld-plus-knop, koos de gebruiker er expliciet voor die eis te laten vervallen in
plaats van een extra invoerschermpje bij het aanvinken te tonen. `reason` blijft wel een parameter
(voor eventuele toekomstige aanroepers die er nog een willen meegeven) — `ReasonSummary` in de audit
trail/`SupplierSelection` noemt dan nog steeds wie de keuze maakte, alleen zonder verplichte
toelichting erbij.

Direct na **DueDate** staat **PO DueDate** (user request, okt 2026): de datum die een PO-regel voor
deze aanvraag zou krijgen als er nu een order aangemaakt werd, dus `DueDate` met MAX's eigen
`CalculateMRPDueDateFromPODueDate` erop toegepast (zie de "Resultaat van een derde test" alinea
verderop voor waarom het die functie is, niet `CalculatePODueDateFromMRPDueDate`) — precies dezelfde
berekening en dezelfde `MaxOrderRepository`-aanroep als `MaxPurchaseOrderRepository.BuildPurchaseOrderLine`
straks echt gebruikt, hier alleen vooraf getoond zodat de juiste datum al bij de leverancier opgegeven
kan worden vóórdat de PO er daadwerkelijk is. Eén gebatchte query per grid-load
(`CalculatePoDueDatesAsync`, `MainForm.GetPoDueDatesByKeyAsync`) in plaats van één aanroep per rij;
leeg zolang DueDate of PartID
van de regel nog onbekend is. Geen eigen opslag van deze waarde — altijd live opgevraagd bij MAX,
nooit in de lokale database gecached.

**Statusfilter ("Display")**: een aparte groep checkboxen (los van de MAX-Query-filters ernaast),
één per `PurchaseRequest.Status`-waarde **behalve Ordered**, bepaalt welke Workflow-statussen in het
grid getoond worden. Dit raakt nooit MAX aan: aan- of uitvinken filtert enkel lokaal al
gesynchroniseerde aanvragen opnieuw (`PurchaseRequestRepository.GetAllAsync()`, client-side gefilterd),
ook na een "Query"-klik. Sourcing starten/Alles sourcen slaat Ordered-aanvragen expliciet over (zou
anders een al geplaatste order stilletjes terugzetten naar ReadyToOrder — elke regel heeft al een
SupplierSelection zonder openstaande exception/goedkeuring, dus de sourcing-uitkomst zou opnieuw
ReadyToOrder worden); Order plaatsen was dat al veilig via de bestaande `HasOrderForLineAsync`-check in
`ProcurementEngine.PlaceOrdersAsync`.

**Ordered komt niet meer voor in dit grid, punt** (user feedback, 20 okt 2026 — "dit scherm moet
alleen PR's en PL's tonen, de daadwerkelijke orders wil ik hier helemaal niet meer terugzien"): Ordered
had hiervoor nog wel een checkbox (standaard uit, maar alsnog aan te vinken om een al bestelde
aanvraag terug op te zoeken, met een lichtgroene rij-highlight erbovenop). Die checkbox en highlight
(`MainForm.HighlightOrderedRows`) zijn beide verwijderd — `BuildRequestStatusFilterGroup` slaat
`PurchaseRequestStatus.Ordered` nu expliciet over bij het opbouwen van de checkboxgroep, dus een
Ordered-aanvraag kan via dit scherm helemaal niet meer getoond worden, ook niet op aanvraag. Het
"Orders..."-scherm hieronder is en blijft de enige plek om geplaatste orders te bekijken — die toont
bovendien de actuele PO-data (bevestigde hoeveelheid/prijs/leverdatum), niet de bevroren PR-snapshot
van het moment van bestellen die dit grid anders zou laten zien.

**"Orders"-scherm (los van de PR-grid, spec-toevoeging sep 2026)**: een `PurchaseOrder` is geen
PR-achtig ding — één PO kan regels van meerdere aanvragen bundelen (gegroepeerd per leverancier, niet
per aanvraag), en heeft zijn eigen levenscyclus (`Created → Submitted → Acknowledged →
Confirmed/PartiallyConfirmed/Backorder → PartiallyShipped → Shipped → Completed`, los van
`Cancelled`/`Rejected`) die niets met de PR-Workflow-status te maken heeft. `OrdersForm` (knop
"Orders...") toont daarom alle PO's van deze administratie in één los, globaal overzicht — inclusief
`ConfirmedQuantity`/`ConfirmedUnitPrice`/`EstimatedShipDate`, wat een leverancier daadwerkelijk
bevestigd heeft, iets wat nergens anders zichtbaar was (ook niet in de Selected-kolommen hierboven,
die alleen de gekozen offerte tonen, niet een latere bevestiging ervan). Het regels-grid toont per
regel expliciet of hij al bevestigd is (`ConfirmedQuantity.HasValue` — de leverancier bevestigt per
regel, niet per hele PO in één keer) en hergebruikt `OrderConfirmationException`'s eigen (al geteste)
`QuantityMismatch`/`PriceMismatch`/`DeliveryIsLate`-logica om af te lezen of het bevestigde aantal/
prijs/leverdatum afwijkt van wat besteld/gevraagd was — als drieledige (leeg/aan/uit) checkbox zolang
een regel nog niet bevestigd is, plus een lichtoranje rij-highlight bij een afwijking, zodat je dat
niet per regel hoeft na te rekenen. Dit kwam destijds naast `OrderDetailForm` ("Order Details"), dat
gericht bleef op de PO('s) bij één specifieke geselecteerde aanvraag — die knop/dat scherm is
inmiddels verwijderd (zie "Menubalk i.p.v. losse knoppen" verderop): hij werkte alleen vanaf een
geselecteerde rij in het requests-grid, en een Ordered-rij kan daar sinds de "Ordered komt niet meer
voor in dit grid"-wijziging helemaal niet meer verschijnen, dus de knop was al onbereikbaar geworden.
Dit "Orders..."-scherm is sindsdien de enige plek om een geplaatste order te bekijken.

**`PurchaseOrder.CompanyId` (sep 2026)**: `PurchaseOrder` had tot nu toe geen company-scoping,
in tegenstelling tot `PurchaseRequest` — elke `PurchaseOrderRepository`-methode retourneerde orders
van *alle* MAX-administraties door elkaar. Vooral relevant voor `GetAwaitingConfirmationAsync`
(zou "Orderbevestiging verwerken" bevestigingen van een andere administratie hebben laten
verwerken/naar MAX terugschrijven) en nu voor dit nieuwe globale Orders-scherm. Opgelost door
`CompanyId` toe te voegen (zelfde patroon als `PurchaseRequest`, spec §15) en elke read/write ermee
te filteren.

**Hoe dit samenwerkt met de rest van de engine**: `ProcurementEngine`, de approval-flow en het
plaatsen van orders werken volledig in termen van dit prototype's eigen `PurchaseRequest`-tabel
(`CompanyId`-gescheiden, zie boven). In plaats van MAX-orders los daarvan te verwerken, worden ze
bij elke `GetOpenPurchaseRequestsAsync()`-aanroep (dus bij elke klik op "Query") **gesynchroniseerd**:
elke MAX-order zonder bestaande `PurchaseRequest` (gededupliceerd op `ErpRequestNumber` =
`Order_Master.ORDNUM_10`) wordt één keer aangemaakt. Handmatig toegevoegde testaanvragen (§8.1) staan
gewoon naast de gesynchroniseerde MAX-orders in dezelfde tabel/lijst.

**MAX is leidend (spec, sep 2026)**: bij elke Query wordt ook elke nog-openstaande lokale aanvraag
(`GetOpenAsync` — dus nooit een al bestelde) rechtstreeks tegen MAX gecheckt, los van het huidige
status-/Due Date-/Select By-filter (`MaxErpConnector.ReconcileAgainstMaxAsync`,
`MaxOrderRepository.GetByOrderNumbersAsync`). Bestaat de MAX-PR nog: `RequestedQuantity`/
`RequiredDate`/`MaxOrderStatus` worden bijgewerkt naar de actuele MAX-waarden. Bestaat hij niet meer:
de lokale aanvraag wordt verwijderd, inclusief alle al opgebouwde `SupplierOffer`/`SupplierSelection`/
`ApprovalRequest`-rijen voor die regel (dus ook een aanvraag die al op WaitingApproval staat) —
gelogd in het audit-log vóór het verwijderen, want de rij zelf is er straks niet meer om dat later uit
te leggen. Status **"1 - Planned"** had hier lange tijd een uitzondering: de aanname was dat MAX zelf
elke nacht (de MRP-run) alleen een nieuw ordernummer aan zo'n order toekent, zonder hem echt te
verwijderen — "niet meer gevonden onder het oude nummer" zou dan niets betekenen. De gebruiker
corrigeerde dit (20 okt 2026): de nachtelijke run verwijdert Status 1-orders daadwerkelijk, dus een
verdwenen Status 1-order wordt voortaan hetzelfde behandeld als Status 2 — ook lokaal verwijderd.
Als extra vangnet checkt `MaxPurchaseOrderRepository.CreatePurchaseOrderAsync` vlak vóór elke `AddPODetail`-call
nogmaals of de PR nog bestaat (er kan tijd zitten tussen de laatste Query en het klikken op "Order
plaatsen") — is hij inmiddels weg, dan wordt er geen PO aangemaakt en breekt die regel af met een
duidelijke foutmelding; de daadwerkelijke lokale opruiming gebeurt pas bij de eerstvolgende Query.

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

**Uitzondering op "nooit overschrijven": een gecachte mapping met een lege `SupplierPartNumber`**
(gevonden door de gebruiker, okt 2026): zo'n rij is onbruikbaar — `BuildOfferAsync` gooit dan
"SupplierPartNumber is required" (zichtbaar in het Audit-log als `OFFER_BUILD_FAILED`, zonder een
voorafgaande `PRODUCT_SEARCHED`-regel, hét signaal dat `ResolveOneMappingAsync` de gecachte rij
blindelings hergebruikte in plaats van opnieuw te zoeken). Dit kan ontstaan als een eerdere zoekopdracht
wél een match vond, maar die match zelf geen bruikbaar partnummer had (bv. een marketplace-only
listing). Zo'n mapping blokkeerde daarna *permanent* elke toekomstige sourcing-poging voor dat
artikel/die supplier, want de code deed dan nooit meer een nieuwe zoekopdracht. `ResolveOneMappingAsync`
behandelt een lege `SupplierPartNumber` nu hetzelfde als "nog geen mapping": er wordt alsnog een verse
zoekopdracht gedaan, en bij succes wordt de bestaande rij **ge-update in plaats van een nieuwe rij
toegevoegd** (`SupplierProductMappingRepository.UpdateSupplierPartAsync`) — anders zou de
`(ErpArticleId, SupplierCode)`-unique-index een tweede insert blokkeren. Zelfherstellend dus: gewoon
opnieuw sourcen op dezelfde aanvraag is genoeg, geen handmatige opschoning via het mapping-scherm nodig.

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

**Let op — dit script wist ook ingevoerde supplier-credentials, niet alleen testaanvragen** (ontdekt
okt 2026): omdat het script écht *elke* `Procurement_`-tabel dropt en opnieuw aanmaakt, inclusief
`Procurement_Supplier`, verdwijnen bij het draaien ervan ook alle al via Instellingen ingevoerde
Client Id/Client Secret/ApiKey/refresh-token én account-/verzendgegevens van elke supplier — niet
alleen lokale testaanvragen zoals eerder hier stond. Na het draaien van dit script dus altijd even
**Credentials bewerken...** en **Verzendgegevens bewerken...** opnieuw invullen voor elke supplier
die al echte sandbox-credentials had, vóórdat je verder test.

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

**Account-/verzendgegevens per leverancier** (`Supplier.AccountId`/`ContactName`/`ContactEmail`/
`ContactTelephone`/`AddressLine1`/`AddressLine2`/`City`/`Province`/`PostalCode`/`CountryCode`, sep
2026): wat een echte Ordering-API-aanroep nodig heeft (confirmed tot nu toe tegen DigiKey Ordering
v3's `OrderRequest.BuyerContact`/`ShippingContact`) — generiek gemodelleerd, niet DigiKey-specifiek,
want elke leverancier's echte order-plaatsing zal ongeveer dezelfde vorm nodig hebben (een
account-referentie plus een verzendadres/contactpersoon). In tegenstelling tot credentials **niet**
versleuteld (een accountnummer/adres is geen secret) en bewerkbaar via een aparte, wél vooraf
ingevulde dialoog: **Instellingen → Suppliers → "Verzendgegevens bewerken..."**. Niet in de
Suppliers-grid zelf getoond (tien extra kolommen voor iets dat je een handvol keer ooit instelt, is
pure ruis).

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
- **DigiKey-foutmeldingen zijn uitgebreid met alles voor een support-ticket** (okt 2026, op verzoek
  van de gebruiker): `DigiKeyHttpClientWrapper.DescribeRequest` voegt de HTTP-methode + volledige URL,
  het Client Id (nooit het secret) en een UTC-tijdstip toe aan elke gegooide `SupplierException`,
  naast de bestaande `correlationId`/`detail` uit DigiKey's eigen foutrespons (`DescribeError`) — zo
  bevat het Audit-log's `Error`-veld zelf al voldoende context om rechtstreeks naar DigiKey support
  door te sturen, zonder los de aanroep-URL te hoeven opzoeken. `OFFER_BUILD_FAILED` logt er ook het
  opgezochte `SupplierPartNumber` bij (`requestPayload`), voorheen leeg.

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

**Bestellen bij DigiKey (Ordering v3, sep 2026)**: als enige van de elf suppliers heeft DigiKey een
door de gebruiker aangeleverde, bevestigde Ordering-API-spec (Swagger/OpenAPI) — `CreateOrderAsync`
in real-mode (`UseMockData=false`) plaatst daarmee een echte `POST /Ordering/v3/Orders`-aanroep in
plaats van de eerdere `SupplierException`. Belangrijke punten:
- **3-legged OAuth**: in tegenstelling tot Product Information (2-legged client-credentials) eist
  Ordering v3 expliciet een Authorization Code-flow met gebruikersinstemming. DigiKey **weigert elke
  Callback URL die met `http://` begint** ("Invalid redirection uri") — en voor een desktop-app zonder
  een echt, publiek bereikbaar callback-endpoint documenteert DigiKey zelf de oplossing: registreer de
  letterlijke waarde **`https://localhost`** (geen poort, geen pad) als Callback URL. Er draait daar
  nooit echt iets — na goedkeuren stuurt de browser door naar `https://localhost/?code=...&state=...`,
  wat gewoon mislukt te laden (geen server op dat adres), maar de adresbalk toont nog wel de volledige
  aangevraagde URL inclusief de query string. `DigiKeyOrderingAuthorizer` opent daarom alleen de
  browser naar DigiKey's autorisatiepagina; **Instellingen → Suppliers → "Ordering autoriseren..."**
  (alleen zichtbaar effect bij de DigiKey-rij) toont daarna een dialoogvenster waarin je die URL uit de
  adresbalk plakt, waaruit de `code`- en `state`-parameters worden geëxtraheerd (met CSRF-check op
  `state`) en ingewisseld voor een access- en refresh-token — er is dus bewust **geen lokale
  HttpListener**, dat vereist een Callback URL die DigiKey's portal toch niet accepteert. Het
  refresh-token wordt versleuteld opgeslagen (`Supplier.RefreshTokenEncrypted`, zelfde
  `SecretProtector`-patroon als `ClientId`/`ClientSecret`) en daarna automatisch ververst door
  `DigiKeyHttpClientWrapper` — bij rotatie van het refresh-token (sommige OAuth-providers geven er bij
  elke ververing een nieuwe) wordt de nieuwe waarde direct teruggeschreven via
  `SupplierRepository.UpdateRefreshTokenAsync`.
- **Verzendgegevens verplicht**: `EnsureOrderingContactConfigured` weigert een echte order te
  plaatsen zolang AccountId/contactnaam/adres niet zijn ingevuld via "Verzendgegevens bewerken...".
- **Geen server-side dedup bij DigiKey**: DigiKey's eigen spec waarschuwt expliciet dat een
  opnieuw-verstuurde order (bv. na een client-side retry) een tweede keer in rekening gebracht en
  uitgeleverd wordt — er is geen idempotency-key-ondersteuning aan hun kant. De bestaande
  in-memory `OrdersByIdempotencyKey`-cache in `DigiKeyAdapter` wordt daarom vóór elke
  mock/real-vertakking gecontroleerd, zodat een herhaalde aanroep met dezelfde idempotency-key nooit
  een tweede keer echt bij DigiKey terechtkomt.
- **`SalesOrderId`-respons heeft drie betekenissen** (uit de bevestigde spec): een positief nummer is
  de normale echte order; `0` betekent dat de order volledig via DigiKey Marketplace-partners is
  afgehandeld (het echte nummer/de echte nummers staan dan in `SalesOrderIds_DKPlus`); `-1` betekent
  dat DigiKey de order heeft ontvangen maar nog geen nummer had toegekend op het moment van
  antwoorden — expliciet **geen** foutsituatie, en zo'n order mag niet opnieuw verstuurd worden.
- **`GetOrderStatusAsync` is geïmplementeerd tegen twee confirmed specs, en gebruikt er actief één**
  (okt 2026, beide door de gebruiker aangeleverd). Eerst gebouwd tegen **OrderStatus v4**
  (`GET /orderstatus/v4/salesorder/{salesOrderId}`, 2-legged — zelfde token als Product Information),
  met rijke data (een echte `SalesOrderStatus`-enum, prijs per regel). Die API gaf tegen de sandbox
  echter een **403 "not authorized"** ondanks dat 'ie in DigiKey's portal op Enabled staat — hetzelfde
  portal/backend-desyncsymptoom als destijds bij Product Information V4 (toen opgelost door DigiKey na
  contact). In plaats daarvan gebruikt `GetOrderStatusAsync` nu actief **Order Management v1**
  (`GET /OrderManagement/v1/SalesOrders/{salesOrderId}`, 3-legged — hetzelfde token dat Ordering al
  bewezen heeft te werken), met een duidelijke trade-off: `SalesOrderDetailView` heeft **geen
  prijsveld** (dus geen prijsafwijking-detectie via dit pad) en **geen status-enum** (`PurchaseOrderStatus`
  wordt afgeleid uit Quantity/QuantityShipped/QuantityBackorder in plaats van een echte DigiKey-status).
  De OrderStatus v4-implementatie (`DigiKeyAdapter.GetOrderStatusViaOrderStatusApiAsync`) is **niet
  verwijderd** — bewust aangehouden als kant-en-klare, rijkere vervanging zodra DigiKey de 403 oplost;
  `GetOrderStatusAsync` hoeft dan alleen de aanroep terug te wisselen. `CancelOrderAsync` blijft
  mock-only — daarvoor is nog geen contract aangeleverd.

**Een eerste echte testorder plaatsen zonder MAX te raken** (`Procurement.Tools.DigiKeyOrderTester`,
okt 2026): er is geen MAX-testomgeving beschikbaar, en de normale "Order plaatsen"-knop schrijft altijd
eerst een echte PO naar MAX (`ProcurementEngine.PlaceSupplierPurchaseOrderAsync`), ongeacht of de
supplier-kant sandbox/mock is — niet geschikt dus om de eerste echte DigiKey-sandboxorder mee te
testen. Dit losse console-tooltje (`src/Procurement.Tools.DigiKeyOrderTester`) roept
`DigiKeyAdapter.CreateOrderAsync` rechtstreeks aan, met de al-opgeslagen, al-geautoriseerde
DigiKey-credentials/refresh-token uit `Procurement_Supplier` (alleen-lezen toegang tot die ene tabel,
nooit enige MAX-tabel). Draai het met:

```
set PROCUREMENT_SECRET_KEY=<dezelfde sleutel als de hoofdapplicatie>
set PROCUREMENT_TEST_CONNECTION_STRING=<connection string naar de echte Unitron-database>
Procurement.Tools.DigiKeyOrderTester.exe <DigiKeyPartNumber> [quantity] [unitPrice]
Procurement.Tools.DigiKeyOrderTester.exe status <SalesOrderId>
```

De eerste vorm roept `CreateOrderAsync` aan (een nieuwe testorder plaatsen); de tweede
(`status <SalesOrderId>`, okt 2026) roept het nieuwe `GetOrderStatusAsync` aan met een al bestaand
`SalesOrderId` (bv. het nummer dat de eerste vorm teruggaf). Beide vereisen dat "Ordering
autoriseren..." al minstens één keer succesvol is doorlopen (anders is `Supplier.RefreshToken` leeg en
stopt het tooltje meteen met een duidelijke melding). Print het volledige resultaat
(`SupplierOrderNumber`/`Status`/`OrderTotal`, of per status-regel `ConfirmedQuantity`/
`ConfirmedUnitPrice`/`EstimatedShipDate`) of de `SupplierException` as-is — geen assertions, puur om te
observeren wat DigiKey's sandbox teruggeeft, inclusief welk `DigiKeyPartNumber` die sandbox
daadwerkelijk accepteert (daarover was bij het schrijven van deze tool nog niets gedocumenteerd
gevonden, dus dat is deels gewoon uitproberen).

**Nog niet gebouwd — bestellen (de overige tien suppliers)**: `CreateOrderAsync`/`GetOrderStatusAsync`/
`CancelOrderAsync` blijven bij de rest van groep 1 in real-mode (`UseMockData=false`) een expliciete
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
hieronder. Die dialoog heeft sinds okt 2026 een **"Ophalen uit MAX"**-knop naast het interne
artikelnummer: vult Desc1/Desc2/PartType/Customer (en, als "Omschrijving" nog leeg is, ook dat veld)
in vanuit `Part_Master`, dezelfde velden die een echte gesyncte MAX-order al automatisch krijgt
(`MaxOrderRepository.GetPartByIdAsync`) — voorheen bleven die altijd leeg voor een handmatige
testaanvraag.

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
- **MAX PO-aanmaak: aanpak gegrond in MaxOrderModule's eigen (gedecompileerde) broncode, sinds okt
  2026 actief in test tegen de live administratie** — `MaxErpConnector.UseMockPurchaseOrders`
  (default sinds okt 2026: **`false`**) bepaalt of "Order plaatsen" naar dit prototype's eigen
  `Procurement_PurchaseOrder` blijft schrijven, of naar een echte MAX-PO via
  `MaxPurchaseOrderRepository`. Wat wij in het requests-grid zien is in MAX een
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

  **Er is geen MAX test-/sandbox-administratie** — dit wordt daarom bewust stapsgewijs, direct tegen
  de live administratie, uitgeprobeerd:
  1. **Eerste stap (nu actief)**: alleen `AddPODetail` (een echte PO aanmaken) — getest met een
     handmatig toegevoegde testaanvraag (§8.1, "Nieuwe testaanvraag toevoegen") in plaats van een
     echte, synced MAX-PR. Zo'n testaanvraag krijgt wel een nep-`ErpRequestNumber` ("TEST-...") maar
     nooit een `PurchaseRequestLine.MaxLineNumber` (dat veld wordt alleen door `MaxOrderRepository`'s
     sync vanuit een echte `Order_Master`-rij gezet) — en zowel
     `EnsurePurchaseRequisitionStillExistsAsync` als `RemoveOriginalOrder` slaan hun werk over zodra
     `MaxLineNumber` leeg is. Resultaat: een echte PO wordt wél aangemaakt (dat wordt dus getest), maar
     stap 2 hieronder (de originele PR verwijderen) gebeurt vanzelf niet — geen risico dat deze eerste
     test een echte openstaande aanvraag laat verdwijnen.
  2. **Nog niet getest**: `DeletePurchaseRequisitionLineItem` zelf — dat vereist een echte, gesyncte
     PR (dus een echte openstaande MAX-aanvraag) om tegen te testen, een logische vervolgstap zodra
     stap 1 bevestigd goed gaat.
  3. **Let op**: dezelfde vlag (`UseMockPurchaseOrders=false`) zet ook **"Orderbevestiging
     verwerken"**'s echte MAX-schrijfkant aan (`ApplyConfirmationAsync` hieronder) — niet alleen
     "Order plaatsen". Wie stap 1 geïsoleerd wil houden, klikt die knop dus nog niet.

  Bevestigd via de volledige, aangeleverde `MaxOrderModule`-broncode (niet giswerk): `GetErrors()` is
  publiek (gebruikt voor foutmeldingen na een mislukte `AddPODetail`), en een PR-rij gebruikt `"00"`
  voor zowel `LINNUM_10` als `DELNUM_10` (niet `"01"` zoals eerst aangenomen). Zie de class-comment
  van `MaxPurchaseOrderRepository` voor de resterende openstaande punten: `AssignPRsToPO` (en de
  verwante bulkmethode `AssignPONumber`) lijken mogelijk een correctere, atomaire aanpak (PR direct
  ombouwen naar PO-regel i.p.v. nieuw aanmaken + apart verwijderen), maar `OrderAssign`'s volledige
  veldenoverzicht, `TargetOrder`'s exacte gedrag, en of een eigen hoeveelheid meegegeven kan worden
  (nodig na order-multiples/MOQ-afronding) zijn niet bevestigd; de betekenis van `AddPODetail`'s
  `FixVar`/`RoundType`- en `ChangePODetail`'s `IncOrdRev`-parameters staat nog niet vast.

  **Resultaat van de eerste echte test (okt 2026), met drie correcties van de gebruiker**:
  `REVLEV_10` is voor een nieuwe PO altijd `"000"` (stond op `"1"`); `STK_10`/`BUYER_10`/`PLANID_10`
  komen niet uit een hardcoded fallback of de lokale `PurchaseRequestLine`, maar uit een verse
  `Part_Master`-lookup per regel (`MaxOrderRepository.GetPartByIdAsync`, uitgebreid met
  `DELSTK_01`/`BUYER_01`/`PLANID_01`): Stock blijft leeg zonder PrimaryStock (geen `"FGI"`-fallback
  meer), Buyer valt terug op `"000"` zonder artikel-Buyer, Planner wordt gewoon gekopieerd.

  **Resultaat van een tweede echte test (okt 2026, via Farnell als supplier), met een correctie op de
  eerste aanname hierboven**: `PurchaseRequestLine.RequiredDate` (zoals gesynct vanuit MAX) heeft de
  Stock Lead Time van het artikel (`Part_Master.PURSTK_01`) al verwerkt — die datum is dus de datum
  waarop het bedrijf het onderdeel écht nodig heeft, lead time inbegrepen. Een nieuwe PO-regel's eigen
  `CURDUE_10` moet daarom die gevraagde datum **min** de Stock Lead Time zijn, geteld in
  **werkdagen** (zaterdag/zondag overgeslagen, geen feestdagenkalender) — niet in kalenderdagen, en
  niet opgeteld. De eerste aanname (optellen, kalenderdagen) was dus zowel het verkeerde teken als de
  verkeerde eenheid, en liet elke regel precies één lead-time te laat uitkomen.

  **Resultaat van een derde test (okt 2026), met nog een correctie**: beide eigen pogingen
  (optellen in kalenderdagen, daarna aftrekken in werkdagen) zijn vervangen door een MAX-eigen
  SQLCLR-functie — al kostte dat zelf ook een foute eerste poging. De
  gebruiker leverde eerst `dbo.CalculatePODueDateFromMRPDueDate(@MRPDate datetime, @PartNum
  nvarchar(30))` aan (`EXTERNAL NAME [ExactMAXSQLUtilities].[DateFunctions].
  [CalculatePODueDateFromMRPDueDate]`), maar die bleek — ondanks de naam — de due date juist een
  volle lead time **na** de MRP due date te leggen in plaats van ervoor, exact hetzelfde symptoom als
  de allereerste (foute) handmatige poging. In gebruik is nu `dbo.CalculateMRPDueDateFromPODueDate
  (@PODate datetime, @PartNum nvarchar(30))` — dezelfde signatuur, aangeroepen met de MAX-gesynct
  `RequiredDate` in de `@PODate`-parameter — wat wél het verwachte resultaat geeft. Nog niet bevestigd
  tegen een echte MAX-test. `CURDUE_10` komt rechtstreeks uit `MaxOrderRepository.CalculatePoDueDateAsync`
  (één regel tegelijk) resp. `CalculatePoDueDatesAsync` (gebatcht, via een `VALUES`-tabel tegen de
  functie, ook gebruikt voor de PO DueDate-kolom in de grids) — een scalar-functie-aanroep op de
  MAX-database, geen eigen datumrekenwerk meer. `MaxPurchaseOrderRepository.BuildPurchaseOrderLine`
  krijgt `curDue` daarom als kant-en-klare, al-berekende parameter mee en schrijft die ongewijzigd naar
  `CURDUE_10`. `Part_Master.PURSTK_01` wordt om die reden ook niet meer apart opgehaald of lokaal
  opgeslagen (`MaxPartInfo.StockLeadTimeDays` en `MaxPurchaseOrderRepository.SubtractWorkingDays` zijn
  beide verwijderd).
  **`Purchase_Order_Code`'s eigen Buyer-veld op headerniveau** (`BUYID_16`, anders dan de per-regel
  `BUYER_10` hierboven, die artikel-afgeleid is) moet de **daadwerkelijke** besteller koppelen. Geen
  MAX-native link daarvoor beschikbaar — in plaats daarvan een handmatige conventie die de gebruiker
  zelf heeft opgezet (okt 2026): elke Buyer's Windows-gebruikersnaam staat in `Buyers.UDFKEY_95`.
  `MaxOrderRepository.GetBuyerIdByWindowsUsernameAsync` matcht `ISessionContext.UserName` (de al
  domeinloze Windows-gebruikersnaam, zie `LoginForm.ResolveWindowsUserName`) daartegen op; valt terug
  op `"000"` als er geen match is. Omdat `AddPODetail` de header zelf volledig uit `Vendor_Master`
  opbouwt (geen parameter om een Buyer ID direct mee te geven), gebeurt dit als follow-up
  `ChangePOHeading`-call vlak na het aanmaken van de header (`MaxPurchaseOrderRepository.
  SetHeaderBuyerIdAsync`) — hetzelfde read-modify-write-patroon als `ApplyHeaderConfirmationAsync`
  hieronder gebruikt voor `CONFRM_16`. Let op: dit is een handmatig bijgehouden koppeling, geen
  MAX-garantie — een Buyer die nog geen `UDFKEY_95` heeft gekregen valt stil terug op `"000"`.

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
- **Schermstructuur/navigatie is niet optimaal** (gebruiker, okt 2026, na het testen van de volledige
  MAX-PO-flow): met de losse "Orders..."-, "Order details"-, requests-grid- en
  "Orderbevestiging verwerken"-vensters naast elkaar is het niet altijd logisch waar je welke
  informatie vindt. Twee concrete stukken hiervan zijn inmiddels opgelost:
  1. (20 okt 2026) Het hoofdscherm laat Ordered-aanvragen niet meer zien (enkel nog via het
     "Orders..."-scherm) en toont onderaan rechtstreeks de offers van de geselecteerde aanvraag met
     een "Select"-checkbox, in plaats van het oude, vrijwel-letterlijk-dubbele "regels"-grid plus een
     apart "Offers bekijken"-scherm — zie "Onderste grid vervangen door offers" hierboven.
  2. **Menubalk i.p.v. losse knoppen** (zelfde dag, user feedback: "ik heb nu bovenin mijn form een
     aantal losse knoppen staan, kunnen we hier gewoon een menubalk van maken"): een `MenuStrip` met
     twee hoofdmenu's vervangt de Order Details/Orders/Approvals/Supplier Mapping/Settings/Audit
     Log-knoppen, **Activity** eerst, dan **Options** (user request, zelfde dag: aanvankelijk
     andersom gebouwd, direct omgewisseld). **Activity**: Purchase Requests (activeert dit venster
     zelf — bedoeld om terug te schakelen als het Orders/Approvals-venster ervoor stond), Purchase
     Orders (opent "Orders..."), en Approvals — bewust ook onder Activity gezet (niet expliciet door
     de gebruiker genoemd, maar dezelfde categorie: terugkerend werk om te checken, geen instelling).
     **Options**: Supplier Mapping, Settings, Audit Log (configuratie/beheer). De vijf primaire
     actieknoppen (Add New Test Request/Start Sourcing/Source All/Place Order/Process Order
     Confirmations) blijven gewoon knoppen — frequente acties op dit scherm's eigen data, geen
     navigatie. **Order Details is volledig verwijderd** (knop én `OrderDetailForm` zelf), niet
     verplaatst naar het menu: die knop werkte alleen vanaf een geselecteerde requests-grid-rij, en
     sinds punt 1 hierboven kan een Ordered-rij daar niet meer verschijnen — de knop was dus al
     onbereikbaar geworden, precies de twijfel die de gebruiker er zelf al bij had.
  3. **Elk menu-item een onafhankelijk venster, geen kind van Purchase Requests** (zelfde dag, user
     feedback: "Alle windows worden nu weergegeven als childs van Purchase requests. Ik zou ze graag
     allemaal als afzonderlijke windows behandelen, waarbij Component Procurement de container is"):
     `OrdersForm`/`ApprovalForm`/`SupplierMappingForm`/`SettingsForm`/`AuditLogForm` openden allemaal
     via `ShowDialog(this)` — modaal, en met `MainForm` als Owner (dus visueel een kind-venster
     ervan). Nu gewoon `Show()`, zonder Owner: een eigen, onafhankelijk toplevel-venster met een
     eigen taakbalk-item, tegelijk bruikbaar met `MainForm` en met elkaar. `StartPosition` voor deze
     vijf schermen staat daarom ook op `CenterScreen` i.p.v. `CenterParent` (die laatste doet toch
     niets zonder Owner). Omdat `ShowDialog` blokkeert tot het venster sluit en `Show()` meteen
     teruggeeft, kon het oude `using`-blok (dat bij het verlaten van de `using` meteen `Dispose()`
     aanriep) niet blijven staan — dispose gebeurt nu op het venster's eigen `FormClosed`-event, en
     `ApprovalForm`'s "ververs de aanvragenlijst na het sluiten"-logica (een goedkeuringsbeslissing
     kan een Workflow-status veranderen) verhuisde om dezelfde reden van direct-na-`ShowDialog` naar
     diezelfde `FormClosed`-handler.
  4. **`ShellForm` (nieuw) is nu de echte Application.Run()-root, niet langer `MainForm`** (zelfde
     dag, user feedback: "De Purchase Requests ziet er nog steeds uit als een hoofdform. Ik kan dat
     niet afsluiten of minimaliseren. En ik wil het bij opstarten gewoon zelf kunnen openen via menu -
     Purchase Requests. Nu opent het default."). Beide klachten kwamen uit dezelfde oorzaak: `MainForm`
     was `Application.Run()`'s eigen venster, dus letterlijk de applicatie zelf — het sluiten ervan
     sloot de hele app i.p.v. alleen dat ene scherm, en het werd dus ook altijd automatisch getoond bij
     opstarten, los van punt 3 hierboven (die alleen de ándere vijf schermen al onafhankelijk had
     gemaakt). `ShellForm` is nu een kaal venster met enkel de menubalk (Activity/Options, verplaatst
     vanuit `MainForm`) en wordt als enige aan `Application.Run()` gegeven (`Program.cs`); `MainForm`
     zelf opent voortaan ook via een kale `Show()` (net als de andere vijf), vanuit "Activity >
     Purchase Requests" — niets opent nog automatisch bij opstarten. Anders dan de andere vijf schermen
     houdt `ShellForm` wél één referentie bij naar een al-open `MainForm`-instantie: een tweede klik op
     "Purchase Requests" activeert het bestaande venster (en herstelt het uit geminimaliseerd) in
     plaats van een verwarrende tweede kopie met zijn eigen Query/selectie-status te openen — de andere
     vijf schermen hebben die bescherming bewust niet (minder impactvol, geen reden om dat erbij te
     bouwen). `ApprovalForm`'s "ververs Purchase Requests na het sluiten"-logica (zie punt 3) verhuisde
     mee naar `ShellForm`, en checkt nu expliciet of `MainForm` op dat moment wel open is. `ShellForm`
     opende aanvankelijk klein en vast (`FormBorderStyle.FixedSingle`, `MaximizeBox = false`) — bewust
     bedoeld als "enkel een menubalk, niets om te vergroten", maar dat oogde als een kapot venster
     i.p.v. minimalistisch (user feedback, zelfde dag: "Het scherm moet wel full screen openen"). Nu
     een heel normaal, maximaliseerbaar venster dat gemaximaliseerd opent, met een witte achtergrond
     (eerste poging kleurde per ongeluk `MainForm` donkerder i.p.v. `ShellForm` wit — teruggedraaid
     zodra de gebruiker dat meldde: "Dit was niet de bedoeling... Ik bedoelde enkel de achtergrond van
     het startscherm"). De menubalk zelf heeft sindsdien ook het lichtblauwe kleurenschema van MAX's
     eigen MRP-menu (`ShellForm.ClassicBlueColorTable`, een `ProfessionalColorTable`-subklasse — de
     standaard WinForms-manier om een `MenuStrip` buiten `BackColor` om te hertekenen, nodig omdat de
     normale `ToolStripRenderMode.System` die kleur grotendeels negeert ten gunste van het
     OS-thema), op basis van een door de gebruiker aangeleverde screenshot.
- **Alle UI-labels omgezet naar Engels, kolombreedtes app-breed nagelopen** (user feedback, 20 okt
  2026 — "het is nu een beetje een ratjetoe"): elke Button/Label/CheckBox/GroupBox-titel/
  kolomkop/Form-titel in `Procurement.UI/Forms/*.cs` die nog Nederlands was, is omgezet naar Engels
  (bv. "Opslaan"→"Save", "Annuleren"→"Cancel", "Weergeven"→"Display", "Instellingen"→"Settings",
  "Testaanvragen tonen"→"Show Test Requests") — inclusief de onderliggende C#-propertynamen achter
  elke kolomkop (bv. `Regels`→`Lines`, `Reden`→`Reason`), niet enkel een `HeaderText`-override, zodat
  code en scherm consistent Engels blijven. Bewust **niet** meegenomen: statusbalk-tekst
  (`SetStatus`/inline label-tekst die bij elke actie verandert), `MessageBox`-titels/-berichten en
  exception-teksten — die blijven Nederlands, dat viel buiten de vraag ("kolommen, velden en
  knoppen", geen meldingen). Tegelijk zijn alle ~16 grids in de app nagelopen op kolombreedte: elke
  kolom die geen expliciete breedte had (en dus door `AutoSizeColumnsMode.Fill` werd uitgerokken,
  vooral zichtbaar in het nieuwe offers-grid, waar destijds alleen "Select" en "Currency" een
  breedte hadden) kreeg een op de inhoud afgestemde `SetCharacterBasedColumnWidth`/`SetMinimumWidth`
  — precies één (soms twee) écht vrije-tekstkolom per grid blijft in Fill staan (bv. Desc1/Reference
  in het hoofdscherm, SupplierPartNumber in de offers-/orderregels-grids, Reason bij Approvals), de
  rest krijgt een vaste, op de inhoud afgestemde breedte. Resultaat: lege ruimte rechts in plaats van
  overal uitgerokken, onleesbare kolommen (user request, 20 okt 2026: "ik heb liever dat we rechts
  wat ruimte over houden").
- **`GridFormatting.SetMinimumWidth` kon een NullReferenceException gooien** (bug gevonden door de
  gebruiker, 20 okt 2026, direct bij het openen van Settings via de menubalk) — een bevestigde,
  bekende WinForms-eigenaardigheid diep in het framework zelf
  (`DataGridViewBand.set_Thickness`/`set_MinimumThickness`): `MinimumWidth` instellen op een kolom die
  nog in Fill-modus staat, op een grid dat op een nog-niet-geselecteerd TabPage staat (dus zonder
  gecreëerd window handle — `SettingsForm`'s vier tabs worden alle vier meteen bij het laden gevuld,
  niet pas bij het eerste keer openen van elk tabblad), triggert intern een Fill-kolomherberekening die
  iets nog-niet-geïnitialiseerds aanspreekt. Fix: de kolom tijdelijk uit Fill-modus halen vlak vóór
  `MinimumWidth` gezet wordt, en meteen daarna terugzetten — omzeilt precies dat interne code-pad,
  zonder het eindresultaat (Fill, met een ondergrens) te veranderen zodra het tabblad wél zichtbaar
  wordt.
- **Opnieuw testen zodra er niet meer tegen de DigiKey-sandbox getest wordt (user request, 20 okt
  2026 — expliciet vastleggen voor go-live)**: het `ConfirmedQuantity` niet gevuld krijgen bij
  "Orderbevestiging verwerken" is zo goed als zeker een sandbox-testdata-beperking, geen bug in deze
  app. Order Management v1's sandbox geeft altijd hetzelfde vaste testartikel + aantal terug
  (`296-1005-5-ND`, 1000 stuks), los van de werkelijk opgevraagde order — de matching in
  `PurchaseOrderRepository.ApplyOrderConfirmationAsync` (exacte `SupplierPartNumber`-match) vindt dan
  terecht niets. OrderStatus v4's sandbox bleek het nog bonter te maken: die gaf zelfs een 404 terug
  voor een echt bestaande sandboxorder (zie "OrderStatus v4 opnieuw geprobeerd..." hierboven) — dat
  endpoint lijkt zelf geplaatste testorders daar helemaal niet te kennen. Beide bevindingen zijn
  specifiek aan DigiKey's **sandbox**-omgevingen toe te schrijven, niet aan iets in deze code. Zodra
  er tegen een echte (niet-sandbox) DigiKey-omgeving getest kan worden: dezelfde flow
  (order plaatsen → "Orderbevestiging verwerken") opnieuw doorlopen en controleren of
  `ConfirmedQuantity`/`ConfirmedUnitPrice` dan wél correct gevuld worden, en of OrderStatus v4 dan wél
  bruikbaar is (rijkere data dan Order Management v1 — zie die methode's eigen comment).
- **Kleurstelling van de overige schermen afgestemd op MAX zelf** (user feedback, 20 okt 2026, naar
  aanleiding van een screenshot van MAX's eigen "MRP Detail 11"-invoerscherm: "Ok en de schermen
  graag iets als deze kleur"). Dat screenshot combineerde drie losse stijlkeuzes (gele
  invoervelden, grijze formulierachtergrond, blauwe grid-headers), dus is er eerst expliciet
  gevraagd welke(n) precies gewenst waren in plaats van alle drie te gokken — het antwoord was
  **"Lichtgrijze formulierachtergrond, Blauwe grid-headers"**; de gele invoervelden zijn bewust
  **niet** meegenomen. Doorgevoerd op `MainForm`, `OrdersForm`, `ApprovalForm`,
  `SupplierMappingForm`, `SettingsForm`, `AuditLogForm` en `OrderConfirmationExceptionsForm` (niet
  `ShellForm` zelf — die kreeg zijn eigen witte achtergrond al bij de vorige wijziging, bewust
  bedoeld als contrast tegenover de rest, zie het punt hierboven). Elke `BackColor` is nu expliciet
  `SystemColors.Control` in plaats van impliciet (het was toevallig al de WinForms-default, maar nu
  staat elk scherm er bewust op, niet bij toeval van een ongezette property). Voor de grid-headers
  is een nieuwe gedeelde `GridFormatting.ApplyClassicHeaderStyle` toegevoegd — zet
  `EnableHeadersVisualStyles = false` (zonder dat negeert Windows de eigen headerkleur volledig en
  tekent het OS-thema overheen) en een lichtblauwe `ColumnHeadersDefaultCellStyle.BackColor`
  (`RGB 205,221,240`) — en die wordt nu op alle 13 grids in de app aangeroepen, naast de bestaande
  `EnableDoubleBuffering`/`EnableRowNumberHeaders` op dezelfde plek.
- **MDI-container + echt lichtblauwe schermachtergrond** (user feedback, 20 okt 2026): "De schermen
  van bv Purchase Request kan ik nog steeds buiten het hoofdscherm plaatsen, bedoeling is dat die
  binnen het hoofdscherm blijven, MDI-container? De achtergrond van de schermen is niet gewijzigd,
  die wil ik ook hetzelfde hebben als het voorbeeld dat ik eerder stuurde, dus lichtblauwachtig."
  Twee aparte problemen:
  - **Vensters nog los te verslepen**: de vorige wijziging maakte van elk scherm een onafhankelijk,
    eigenaarloos topleveltvenster (`Show()` zonder Owner) — dat kon dus letterlijk overal op het
    bureaublad gesleept worden, buiten `ShellForm` om. Dat was niet de bedoeling van "waarbij
    Component Procurement de container is". `ShellForm` is nu een echte MDI-container
    (`IsMdiContainer = true`), en elk scherm (`MainForm`, `OrdersForm`, `ApprovalForm`,
    `SupplierMappingForm`, `SettingsForm`, `AuditLogForm`) krijgt bij het openen
    `MdiParent = this` in plaats van alleen `Show()` — ze blijven nu letterlijk binnen
    `ShellForm`'s cliëntgebied en kunnen er niet meer buiten gesleept worden.
    `OrderConfirmationExceptionsForm` blijft ongewijzigd een modaal dialoogvenster (geopend vanuit
    `MainForm` met `ShowDialog`, niet vanuit de menubalk) en wordt dus bewust geen MDI-child.
  - **Achtergrond "niet gewijzigd"**: terecht opgemerkt — de vorige wijziging zette `BackColor`
    expliciet op `SystemColors.Control`, maar dat was toevallig al de impliciete WinForms-default,
    dus er was visueel niets veranderd. Vervangen door een echte lichtblauwe kleur
    (`GridFormatting.ClassicScreenBackColor`, `RGB 230,240,250`) op diezelfde zeven schermen — een
    tint lichter dan de blauwe grid-headers (`RGB 205,221,240`) zodat de headers nog als een apart
    "bandje" blijven afsteken in plaats van in de achtergrond te verdwijnen. Het verborgen
    `MdiClient`-besturingselement achter de menubalk (automatisch aangemaakt zodra
    `IsMdiContainer = true` is) kreeg dezelfde kleur mee (via `ShellForm.OnLoad`, want dat element
    bestaat pas zodra het form-handle is aangemaakt), zodat het gebied rond/achter de MDI-children
    niet in een afwijkend grijs (`SystemColors.AppWorkspace`) overblijft. `ShellForm` zelf
    (het "startscherm") behoudt zijn eigen witte achtergrond, zoals eerder expliciet gevraagd.
- **Knoppen en GroupBox-randen in dezelfde "classic MAX"-stijl** (user feedback, 20 okt 2026, naar
  aanleiding van een screenshot van een MAX-queryscherm met "Query"/"Graph"-knoppen en getitelde,
  omrande vakken "Range"/"Dates"/"Transaction Types"): eerst gevraagd wat precies bedoeld werd —
  antwoord: "Ik bedoel de opmaak van de knoppen aanpassen naar de layout op het voorbeeld, evenals
  de rand van groupboxen etc. (Voor alle schermen voor zover van toepassing)".
  - **Knoppen**: `Application.EnableVisualStyles()` (Program.cs) laat elke `Button` standaard
    renderen met het moderne Windows-thema (verloop, afgeronde hoeklook) — niets zoals de vlakke,
    dunomrande knoppen in het voorbeeld. Nieuwe gedeelde `GridFormatting.ApplyClassicButtonStyle`
    zet `FlatStyle = FlatStyle.Flat` met een dunne blauwe rand (dezelfde kleurfamilie als de
    menubalk/grid-headers/schermachtergrond elders in de app) en een witte achtergrond — toegepast
    op alle 30 knoppen in de app buiten `LoginForm` (bewust niet meegenomen: dat is het
    inlogscherm vóór de sessie, geen onderdeel van de eigenlijke procurement-schermen).
  - **GroupBox-randen**: de standaard GroupBox-rand wordt getekend door het actieve Windows-thema
    en negeert `ForeColor`/`BackColor` daarvoor volledig — de kleur aanpassen kan dus alleen door de
    rand zelf te tekenen. Nieuwe `GridFormatting.ClassicGroupBox` (een `GroupBox`-subklasse met een
    eigen `OnPaint` die rand + titel met de hand tekent, dezelfde blauwe kleur als de knoppen)
    vervangt de kale `GroupBox` die `MainForm.WrapInGroupBox` al gebruikte voor "Fetch Orders from
    MAX"/"Due Date Range"/"Filter"/"Display" — vooralsnog de enige plek in de app met
    GroupBox-secties.
  - Ook de laatste losse plekken zonder de lichtblauwe schermachtergrond kregen die er nu bij:
    `NewPurchaseRequestForm` (nieuw: had nog geen `ClassicScreenBackColor`/knopstijl) en de vier
    los-geopende detail-/credential-dialogen (`AuditLogForm`'s rij-detailvenster,
    `SettingsForm`'s credentials-/shipping-/DigiKey-authorize-dialogen).
- **DigiKey-herbevestiging: gecheckt, bewust niet gewijzigd** (user vraag, 20 okt 2026): "Wat
  gebeurt er nu als DigiKey een herbevestiging stuurt? Komt die ook automatisch in MAX terecht als
  we op de knop Process Order Confirmation drukken?" Antwoord: nee — `PurchaseOrderRepository.
  GetAwaitingConfirmationAsync` selecteert alleen orders met status `Submitted`/`Acknowledged`/
  `PartiallyConfirmed`; zodra de eerste bevestiging de status naar `Confirmed` zet, valt de order
  blijvend buiten die candidates-lijst, dus een latere herbevestiging wordt nooit meer opgehaald.
  User: "Ik wil dit voor nu graag zo houden zoals het is, later kunnen we dat nog finetunen" — geen
  codewijziging, bewust geparkeerd voor een latere iteratie (twee opties al besproken: welke
  statussen blijven rechecken, en of een wijziging t.o.v. de vórige bevestiging — i.p.v. t.o.v. de
  oorspronkelijke bestelling — een eigen soort exception moet worden).
- **Orders-scherm: regel al bevestigd (`Confirmed`-checkbox), maar Order Status toch "Confirmed"**
  (user vraag, 20 okt 2026) — geen bug, bevestigde sandbox-beperking: `DigiKeyAdapter.
  GetOrderStatusViaOrderManagementApiAsync`'s `InferStatusFromOrderManagementDetails` berekent de
  order-status puur uit aggregate hoeveelheden (ongeacht artikelmatching), terwijl de
  regel-`Confirmed`-checkbox in `OrdersForm`/`MainForm` pas waar wordt als
  `PurchaseOrderRepository.ApplyOrderConfirmationAsync` een regel vindt met exact dezelfde
  `SupplierPartNumber` — en DigiKey's Order Management v1 sandbox geeft altijd hetzelfde vaste
  testartikel terug, dus die match mislukt bijna altijd. Zelfde al-gedocumenteerde sandbox-beperking
  als de niet-gevulde `ConfirmedQuantity`; lost zichzelf op zodra tegen een echte DigiKey-omgeving
  getest wordt. User bevestigde: "Ja, dit is nog sandbox, dus duidelijk."
- **Orders-scherm: "Completed Orders"-filter op de Lines-grid** (user request, 20 okt 2026, met een
  screenshot van MAX's eigen "PO Schedule Inquiry 1"-scherm als stijlvoorbeeld): "bij de Purchase
  orders wil ik standaard enkel de orderlijnen zien die nog open staan, niet de lijnen die al
  geleverd zijn. Graag hier een checkbox toevoegen incl Completed Orders... De status kan gehaald
  worden uit Order_Master.STATUS_10 (char(1))." Nieuwe, getitelde `ClassicGroupBox` "Include" (naast
  de Refresh-knop) met één checkbox "Completed Orders", **uitgevinkt** by default — exact MAX's eigen
  "Status 4 Orders (Completed)"-conventie uit de screenshot, niet gegokt. Live, batched lookup
  (`MaxOrderRepository.GetLineStatusesAsync`, nieuw) van `Order_Master.STATUS_10` per PO-regel,
  gematcht op exact dezelfde `(ORDNUM_10, LINNUM_10, DELNUM_10)`-sleutel als
  `MaxPurchaseOrderRepository.ApplyLineConfirmationAsync` al gebruikt om Order_Master-rijen te
  vinden — bewust geen nieuwe opgeslagen kolom (zelfde reden als PO Due Date/voorkeursleverancier:
  een nieuwe EF-kolom triggert het schema-regenereer-script, dat ook Supplier-credentials wist). Eén
  keer opgehaald per "Refresh", daarna puur client-side gefilterd bij het aan/uitvinken van de
  checkbox of het wisselen van geselecteerde order — geen extra MAX-round-trip per toggle. Een regel
  zonder gevonden MAX-status (geplaatst met `UseMockPurchaseOrders=true`, of nog geen
  `MaxLineNumber`) blijft zichtbaar (fail-open) in plaats van stilzwijgend verborgen te worden.
  `MainForm.WrapInGroupBox` is voor deze gelegenheid verhuisd naar de gedeelde
  `GridFormatting`-klasse (was `private static` op `MainForm`) zodat `OrdersForm` 'm ook kan
  gebruiken — gedrag ongewijzigd.
- **GroupBox-rand sloot niet altijd** (user feedback, 20 okt 2026, met een screenshot van het nieuwe
  "Include"-vak: "Kun je de groupboxen nog eens dicht proberen te krijgen... dit geldt eigenlijk
  voor alle groupboxen"). Oorzaak gevonden: `ClassicGroupBox.OnPaint` mat de titeltekst met
  `Graphics.MeasureString`, terwijl `WrapInGroupBox` de box zelf had gegroepeerd/gesized met
  `TextRenderer.MeasureText` — die twee rapporteren een andere breedte voor dezelfde tekst/font. Bij
  een box waar de titel breder is dan de inhoud (zoals "Include" rond één enkele checkbox) viel het
  hervatte stukje bovenrand (na de titel) daardoor net buiten de werkelijke rechterrand van de box,
  en werd het simpelweg nooit getekend — de rand oogde daardoor blijvend "open". Bij de bredere
  groupboxen in MainForm (Fetch Orders from MAX/Due Date Range/Filter/Display) viel ditzelfde defect
  niet op omdat de inhoud daar al breder was dan de titel, maar de onderliggende bug zat er net zo
  goed in — vandaar "dit geldt voor alle groupboxen". Fix: `OnPaint` gebruikt nu dezelfde
  `TextRenderer.MeasureText` als `WrapInGroupBox`, plus een `Math.Min`-ondergrens die het hervatte
  stuk sowieso nooit voorbij de rechterrand laat beginnen, ongeacht toekomstige font-/
  meetverschillen.
- **MAX Status-kolom in Lines of Selected PO + checkbox-tekst matcht MAX's eigen scherm** (user
  feedback, 20 okt 2026): "De status in de Order Master heeft betrekking op de lijnen. Er is geen
  status beschikbaar voor de hele inkooporder. Kun je in het onderste grid de status (getal)
  toevoegen als kolom. Graag de tekst i[n] de groepbox aanpassen naar de tekst die ik je in het
  voorbeeld gestuurd had, dat verbeterd de adaptie wellicht." Nieuwe "MAX Status"-kolom toont het
  rauwe `Order_Master.STATUS_10`-cijfer per regel (ongevertaald, zoals MAX het zelf ook toont), via
  een nieuwe gedeelde `OrdersForm.GetMaxStatus`-helper (ook hergebruikt door de bestaande
  `IsCompletedInMax`-filterlogica, zodat er maar één plek is die `_lineStatusesByKey` opzoekt). De
  "Include"-checkbox heet nu "Status 4 Orders (Completed)" i.p.v. de eigen bewoording "Completed
  Orders" — letterlijk de tekst uit de door de gebruiker aangeleverde "PO Schedule Inquiry
  1"-screenshot.
- **Kolomnaam "MAX Status" → "Status"** (user feedback, 20 okt 2026: "De kolomnaam mag gewoon
  Status zijn, zonder MAX ervoor") — alleen de `HeaderText` aangepast, de onderliggende kolomnaam
  (`MaxStatus`) en de `GetMaxStatus`-logica blijven ongewijzigd. Tegelijk gemeld: de kolom staat
  bij de gebruiker leeg, mogelijk voor orders die in MAX zijn verwijderd — en de bijbehorende,
  terechte vraag waarom zulke orders dan nog wél in het bovenste "All Purchase Orders"-grid
  verschijnen. Dat grid (`OrdersForm._ordersGrid`) komt uitsluitend uit deze app's eigen lokale
  `Procurement_PurchaseOrder`-tabel, zonder enige reconciliatie tegen MAX's actuele stand — in
  tegenstelling tot PurchaseRequests, die wél tegen MAX gereconcilieerd worden vóórdat ze besteld
  worden (`MaxErpConnector.ReconcileAgainstMaxAsync`). Nog niet gewijzigd — eerst overlegd met de
  gebruiker hoe dit moet (zie chat): blijft de lege Status-waarde het juiste, eerlijke signaal dat
  MAX de regel niet meer kent, of moet dit expliciet gelabeld/gefilterd worden?
- **Drie losse punten in één keer afgehandeld** (user feedback, 20 okt 2026, met een screenshot van
  MainForm's filterrij):
  1. **GroupBox-rand had nog geen afgeronde hoeken** ("De kwaliteit van de border van de
     goupboxen is nog niet verbeterd") — de vorige fix loste enkel op dat de rand altijd sluit, maar
     tekende nog rechte hoeken, terwijl elke door de gebruiker aangeleverde MAX-screenshot deze
     sessie steeds afgeronde hoeken laat zien. `ClassicGroupBox.OnPaint` tekent de rand nu met
     `Graphics.DrawArc` op elke hoek (straal 6px) in plaats van scherpe `DrawLine`-hoeken, met
     `SmoothingMode.AntiAlias` zodat de bogen ook daadwerkelijk glad ogen. De titel-gap-logica (zie
     de vorige fix) blijft verder ongewijzigd, enkel ingepast tussen de nieuwe hoekbogen.
  2. **Offers-grid in Purchase Requests mocht lager, ten gunste van het PR-grid** ("Over het
     algemeen zullen we niet meer dan een stuk of 5 offers per PR hebben"): `_requestsGrid` en
     `_offersGrid` zijn verwisseld van rol — `_requestsGrid` is nu `Dock = Fill` (krijgt de meeste
     ruimte, kan honderden PR's bevatten) en `_offersGrid` is nu `Dock = Bottom` met een vaste hoogte
     van 190px (genoeg voor een header plus ~7 rijen, ruim boven de verwachte ~5). Vereiste ook het
     herordenen van de `Controls.Add`-volgorde, omdat WinForms docking-controls in omgekeerde
     toevoegvolgorde verwerkt (laatst toegevoegde binnen elke richting komt het verst naar de rand).
  3. **Orders verwijderd in MAX nu uit de standaardlijst gefilterd** (antwoord op de eerder
     voorgestelde optie 2: "Als ze verwijderd zijn uit MAX dan graag uit de lijst verwijderen...
     maar dat ze nog wel in de tabel blijven staan"): nieuwe `OrdersForm.IsNotFoundInMax`-helper
     (true zodra élke MAX-gekoppelde regel van een order geen Order_Master-rij meer oplevert — een
     order die alleen met `UseMockPurchaseOrders=true` is geplaatst telt niet mee, die had sowieso
     nooit een echte MAX-koppeling) plus een nieuwe checkbox "Deleted in MAX" in dezelfde
     "Include"-groupbox, **uitgevinkt** by default. De lokale `Procurement_PurchaseOrder`-rij wordt
     nooit verwijderd — enkel uit het standaard zichtbare grid gefilterd; de checkbox toont ze weer.
     `OrdersForm.RefreshAsync`'s grid-opbouw is hiervoor verplaatst naar een herbruikbare
     `PopulateOrdersGrid`-methode, zodat het aan/uitvinken van de checkbox direct herfiltert zonder
     opnieuw bij MAX te hoeven opvragen.
- **GroupBox-rand nog steeds niet volledig zichtbaar, ondanks gesloten + afgeronde hoeken** (user
  feedback, 20 okt 2026, met een nieuwe screenshot: "De hoeken zijn prima zo, maar als de border
  niet volledig getekend is dan heb ik er niet zoveel aan"). Oorzaak: `SmoothingMode.AntiAlias`
  stond aan voor de hele tekening, inclusief de rechte `DrawLine`-stukken — een 1px rechte lijn op
  een exacte integer-coördinaat ligt precies op de grens tussen twee pixelrijen; zónder AntiAlias
  rondt GDI+ dat af naar één volledig dekkende rij, mét AntiAlias juist naar twee halftransparante
  rijen, wat tegen de lichtblauwe achtergrond als een nauwelijks zichtbare, uitgewassen lijn oogt —
  exact "de hoeken (bogen) zijn prima, de rest (rechte stukken) niet". Fix: `SmoothingMode` wisselt
  nu per onderdeel — `Default` (geen anti-aliasing) voor elke rechte `DrawLine`, `AntiAlias` enkel
  rond de hoek-`DrawArc`-aanroepen, waar het daadwerkelijk helpt (een boog heeft geen rechte
  pixel-grens om te vervagen).
- **Approvals-grid: Id-kolom smaller, PartID-kolom toegevoegd** (user feedback, 20 okt 2026: "IN
  APprovals is de kolom Id nu belachelijk breed. Graag smaller maken. Verder zou ik hier ook nog
  een kolom bij willen hebben met ons PartID, liefst links naast Part"). `Id` had geen expliciete
  breedte en deelde dus mee in `AutoSizeColumnsMode.Fill`'s verdeling — nu vast op 6 tekens. Nieuwe
  `PartID`-kolom (`PurchaseRequestLine.ErpArticleId`, ons eigen interne artikelnummer, niet de
  Manufacturer Part Number die `Part` al toont) direct links van `Part` gezet, zelfde 14-tekens
  breedte als dezelfde kolom elders in de app (MainForm/OrdersForm).
- **Approvals-grid: PR-nummer toegevoegd** (user feedback, 20 okt 2026: "Kun je hier ook het PR
  nummer nog bijzetten?") — nieuwe `ErpRequestNumber`-kolom (`PurchaseRequestLine.PurchaseRequest.
  ErpRequestNumber`), vóór `Line` gezet. Vereiste een aanpassing in
  `ApprovalRequestRepository.GetPendingAsync`: `.Include(a => a.PurchaseRequestLine)` ving enkel de
  PR-regel zelf, niet de PurchaseRequest erboven — vervangen door
  `.Include(a => a.PurchaseRequestLine.PurchaseRequest)` zodat die ene hop extra ook eager geladen
  wordt, in plaats van op EF6's lazy loading te vertrouwen (consistent met hoe de rest van de app
  altijd expliciet Include't wat een scherm nodig heeft).
- **Approvals-grid: Id verborgen, Line weg** (user feedback, 20 okt 2026: "ALs het ID veld trouwens
  gewoon een teller is dan kan die net zo goed weg, want in de row header staat ook een teller.
  Line ben ik eerlijk gezegd ook niet in geinteresseerd, het gaat mij om de PR"). `Id` blijft wél in
  de DataSource (`DecideAsync` leest 'm terug via `_grid.Columns["Id"]`), maar staat nu op
  `Visible = false` — dezelfde aanpak als `OrdersForm._ordersGrid`'s eigen `Id`-kolom. `Line`
  (`PurchaseRequestLineId`) is volledig uit de projectie gehaald, niet enkel verborgen — nergens
  anders in dit scherm gebruikt.
- **OPEN PUNT — GroupBox-rand nog steeds niet (volledig) gesloten** (user feedback, 20 okt 2026:
  "Graag even noteren dat de groupboxen nog steeds niet gesloten zijn, denk daar nog maar eens over
  na hoe te fixen, want dat ziet er niet netjes uit"). Twee eerdere pogingen zaten ernaast
  (meetverschil tussen `Graphics.MeasureString`/`TextRenderer.MeasureText`, daarna
  `SmoothingMode.AntiAlias` dat rechte lijnen liet verwateren) — beide zijn opgelost, maar het
  probleem blijft kennelijk bestaan, dus de werkelijke oorzaak is nog niet gevonden. Nog te
  onderzoeken bij oppak: mogelijk een laag-niveau WinForms/`GroupBox`-eigenaardigheid die los staat
  van de eigen `OnPaint`-tekenlogica (bv. een ingebouwde Windows-thema-laag die alsnog over de eigen
  tekening heen rendert, een page-herschilder-volgorde-issue, of een DPI/scaling-effect dat hier nog
  niet getest is) — vraagt om een nieuwe screenshot + gerichter kijken naar wélk exact stuk van de
  rand ontbreekt voordat er nog een gok gedaan wordt.
- **Samenvatting sessie 20 okt 2026 — Farnell/Mouser/TME ordering-flow (user: "Ok. Ik ga dit zelf
  opzoeken. Graag even een samenvatting opslaan. Morgen verder")**: onderzocht of Farnell, Mouser en
  TME een vergelijkbare "echte order plaatsen"-flow kunnen krijgen als DigiKey (die al werkt tegen de
  sandbox — zie eerdere punten over Order Management v1 en de 3-legged OAuth-autorisatie).
  - **Wat al op orde staat, ongeacht leverancier**: product search/pricing/availability/packaging
    zijn bij Farnell, Mouser én TME al écht geïmplementeerd (geen mock), credential-opslag loopt al
    via dezelfde generieke `Supplier`-rij/Instellingen-route, en `ProcurementEngine`/sourcing is
    adapter-agnostisch — een leverancier doet al automatisch mee zodra `SupplierPreference.Active`
    staat. Enkel `CreateOrderAsync`/`GetOrderStatusAsync`/`CancelOrderAsync` zijn bij alle drie nog
    mock-only in real-mode (gooien een duidelijke "API contract unconfirmed"-exception) — exact
    dezelfde situatie als DigiKey vóórdat die sessie's werk daaraan begon.
  - **Farnell**: element14's publieke catalog-API (al in gebruik voor search/pricing) heeft geen
    bevestigde Order Submission API — waarschijnlijk een apart, afgeschermd API-product dat nog bij
    Farnell/element14 moet worden opgevraagd. User gaat dit zelf uitzoeken.
  - **Mouser**: wél degelijk gedocumenteerd — een aparte **Order API** (naast de Search API, eigen
    key via "My Mouser" → API-beheer) met een endpoint om een bestaande order in te dienen
    (`SubmitOrder`-vlag: false geeft enkel een samenvatting terug, geen echte plaatsing) en een
    endpoint om een geplaatste order op te zoeken. Let op: Mouser's eigen "API Explorer" is **geen
    sandbox** — testen daar plaatst een echte order op het account. Bronnen:
    [Mouser API Order](https://www.mouser.com/en/api-order/),
    [Mouser API Guide (PDF)](https://www.mouser.lt/pdfDocs/api-guide.pdf),
    [Mouser API Hub](https://www.mouser.com/en/api-hub/). Bestaande comment in `MouserAdapter.cs`
    ("Order API needs a separate, additionally-approved API key") is hiermee bevestigd.
  - **TME**: developer-portal bestaat (`developers.tme.eu`), vermeldt een "Product API"
    (productdata/voorraad/prijzen) en een migratie-opmerking voor apps aangemaakt na 14 mei 2026,
    maar geen publiek vindbare order-plaatsings-/orderstatus-API kon bevestigd worden. Twee
    `WebFetch`-pogingen naar `developers.tme.eu` en `www.tme.eu` liepen beide vast op een
    DNS-foutmelding vanuit deze sessie-omgeving (zelfde soort beperking als eerder deze sessie bij
    andere externe sites) — kon dus niet zelf direct nakijken. User gaat dit zelf uitzoeken (evt.
    ingelogd op een TME-developeraccount, waar meer zichtbaar kan zijn).
  - **Volgende sessie**: wachten op wat de gebruiker bij Farnell/TME vindt voordat
    `CreateOrderAsync`/`GetOrderStatusAsync` voor die twee gebouwd wordt. Voor Mouser zou, zodra
    gewenst, al met de hierboven gevonden documentatie begonnen kunnen worden (eventueel eerst de
    contact-/verzendgegevens-velden laten meelopen in `MouserOptions`, zelfde aanpak als bij
    DigiKey's `AccountId`/`ContactName`/etc.).
- **Farnell Order API — spec aangeleverd (21 okt 2026, via `partner.element14.com/order`, geplakt
  door de gebruiker omdat deze sessie dat domein niet zelf kan bereiken — egress-proxy wijst
  `partner.element14.com` expliciet af, 403 "host not allowed", net als eerder bij
  `developers.tme.eu`/`www.tme.eu`)**. In tegenstelling tot DigiKey's Ordering v3 (één atomaire
  POST met regels + BuyerContact/ShippingContact ineen) is Farnell's Order API een **stateful,
  cart-gebaseerde flow** van meerdere losse aanroepen tegen `https://api.farnell.com`:
  1. `POST /auth/token` (header `api_key`, body `{"tradeAccount": "..."}`) → JWT in
     `Authorization`-respons-veld. Geen OAuth-consentflow zoals DigiKey's "Ordering autoriseren..."
     nodig — gewoon een JWT opnieuw aanvragen per keer, geen refresh-token-rotatie om te bewaren.
  2. `POST /cart/addItem` per regel (`sku`, `quantity`, optioneel `customerPartNumber` — kan onze
     `ErpArticleId` worden — en `lineNote`).
  3. `GET /order/shipping_address` → lijst bestaande, **al op het account opgeslagen** adressen
     (`addressId` + velden); `POST /order/shipping_address` met gekozen `addressId` bevestigt 'm.
     Anders dan DigiKey: hier wordt geen vrij veld met straat/postcode/etc. meegestuurd, enkel een
     ID uit een al bestaande lijst gekozen.
  4. `GET /order/shipping_methods` → lijst (`shippingMethodID`, prijs,
     `shippingPreferredMethod: true/false`); `POST /order/shipping_method` bevestigt de keuze.
  5. `GET /order/order_review` → totalen/regels ter controle.
  6. `POST /order/order_submit` (optioneel `poNumberId` — kan onze `ErpPoNumber`/
     `IdempotencyKey` worden) → `{"orderReference": "<Order Confirmation Number>"}`.
  - Open vragen, nog niet beantwoord/beslist: (a) **geen Order Status/Order History-endpoint
    gezien** in wat is aangeleverd — enkel cart + submit; `GetOrderStatusAsync`/`CancelOrderAsync`
    hebben dus nog geen bevestigd pad, mogelijk staat dat op een aparte pagina die nog niet geplakt
    is; (b) welke `tradeAccount`-waarde gebruiken (nieuw `Supplier`-veld, of hergebruik van
    `AccountId`, net als bij DigiKey?); (c) hoe het te gebruiken `addressId`/`shippingMethodID`
    kiezen als er meerdere beschikbaar zijn — altijd de eerste/preferred, of instelbaar maken?
    (d) cart moet vermoedelijk expliciet geleegd worden (`POST /cart/clear`) vóór elke nieuwe
    orderpoging, om te voorkomen dat een eerder mislukte poging stale regels achterlaat.
