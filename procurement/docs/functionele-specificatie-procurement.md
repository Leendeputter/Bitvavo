# Functionele Specificatie
## Geautomatiseerde Componenteninkoop – Prototype

**Versie:** 1.0
**Doel van dit document:** Input voor ontwikkeling met Claude Code
**Platform:** Visual Studio – Windows Forms – C# – .NET Framework 4.x
**Database:** SQL Server
**Status:** Zelfstandig prototype (nog niet gekoppeld aan UniPro; architectuur wél zo opgezet dat latere integratie als UniPro-module mogelijk is)
**Scope:** Fase 1 MVP – Supplier selectie en automatisch bestellen (DigiKey + Farnell)

---

## 0. Leeswijzer voor de ontwikkelaar (Claude Code)

Dit is een prototype. Er is **nog geen live koppeling met UniPro** en er zijn **nog geen echte DigiKey/Farnell API-credentials** beschikbaar. Bouw daarom:

1. Een **ERP-koppelvlak** (`IErpConnector`) met één concrete implementatie: `MockErpConnector`, die testdata uit de eigen SQL Server-database van dit prototype leest/schrijft in plaats van uit echte UniPro-tabellen. Later kan hier een `UniProErpConnector` naast/in plaats van komen.
2. **Supplier-adapters** (`ISupplierAdapter`) met concrete implementaties `DigiKeyAdapter` en `FarnellAdapter`. Bouw deze zó dat ze achter een `HttpClient`-abstractie zitten, met een **mock-modus** (`IsSandbox`/`UseMockData`) die vaste testresponses teruggeeft, zodat de hele workflow end-to-end te testen is zonder echte API-keys. Zodra credentials beschikbaar zijn, hoeft alleen de HTTP-laag te worden ingevuld.
3. Alle business-regels (packaging, reel-policy, approval-thresholds) als **data in SQL Server**, niet hard-coded, zodat ze later ook vanuit UniPro beheerd kunnen worden.

Doel van het prototype: de volledige workflow van paragraaf 24 van het technisch voorstel (Purchase Request → Sourcing → Selectie → ERP PO → Supplier Order → Confirmation) end-to-end laten werken met mockdata, met een WinForms-UI waarin een gebruiker dit kan volgen en waar nodig handmatig kan goedkeuren.

---

## 1. Technische uitgangspunten

| Onderdeel | Keuze |
|---|---|
| UI-framework | Windows Forms |
| Taal | C# |
| .NET-versie | .NET Framework 4.8 |
| Database | SQL Server (lokaal / LocalDB voor ontwikkeling) |
| Data access | ADO.NET of Entity Framework 6 (EF6, past bij .NET Framework 4.x) — **aanbevolen: EF6 Code First met migraties**, zodat het datamodel eenvoudig kan meegroeien |
| Architectuur | Gelaagd: `Procurement.Core` (domeinmodel + business logic), `Procurement.Data` (EF6/DB), `Procurement.Suppliers.*` (adapters), `Procurement.UI` (WinForms) |
| Configuratie | `App.config` / een `AppSettings`-tabel in SQL Server voor policies |
| Logging | Eenvoudige file- of DB-logger (audit-tabel, zie §9) |
| Secrets | Voor prototype: `App.config` met duidelijke `TODO`-markering dat dit later naar een secrets manager moet; nooit plaintext wegschrijven in de audit-log |

### Voorgestelde solution-structuur

```
Procurement.sln
 ├─ Procurement.Core        (POCO's, interfaces, business rules, geen UI/DB-afhankelijkheid)
 ├─ Procurement.Data        (EF6 DbContext, entiteiten-mapping, repositories)
 ├─ Procurement.Suppliers.DigiKey
 ├─ Procurement.Suppliers.Farnell
 ├─ Procurement.Erp         (IErpConnector + MockErpConnector)
 ├─ Procurement.Engine      (ProcurementEngine: orkestreert sourcing/selectie/order)
 ├─ Procurement.UI          (WinForms-project, startproject)
 └─ Procurement.Tests       (unit tests)
```

Deze scheiding is bewust zo gekozen dat `Procurement.Core`, `Procurement.Data` en `Procurement.Engine` later grotendeels 1-op-1 als UniPro-module kunnen worden hergebruikt; alleen `Procurement.UI` en `Procurement.Erp` veranderen dan wezenlijk.

---

## 2. Belangrijkste architectuurprincipe

> De procurement-engine mag nooit rechtstreeks afhankelijk zijn van een specifieke leverancier. Alle leverancierspecifieke logica zit in een `SupplierAdapter`.

Dit vertaalt zich naar de volgende kerninterface:

```csharp
public interface ISupplierAdapter
{
    string SupplierCode { get; }              // bv. "DIGIKEY", "FARNELL"
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
```

`SupplierCapabilities` geeft per functie aan: `Supported`, `NotSupported`, `NotAvailable` of `ManualProcess` (zie §5). Een adapter die iets niet ondersteunt gooit geen exception maar geeft dit terug via de capability-check, zodat de workflow dit netjes kan overslaan.

---

## 3. Domeinmodel (kernentiteiten)

Onderstaande entiteiten vormen de basis van `Procurement.Core` / `Procurement.Data`. Velden zijn indicatief; verfijn tijdens implementatie.

### 3.1 PurchaseRequest / PurchaseRequestLine

```csharp
public class PurchaseRequest
{
    public int Id { get; set; }
    public string ErpRequestNumber { get; set; }
    public DateTime RequestDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string Warehouse { get; set; }
    public string Project { get; set; }
    public int Priority { get; set; }
    public PurchaseRequestStatus Status { get; set; }
    public List<PurchaseRequestLine> Lines { get; set; }
}

public class PurchaseRequestLine
{
    public int Id { get; set; }
    public int PurchaseRequestId { get; set; }
    public string ErpArticleId { get; set; }
    public string Manufacturer { get; set; }
    public string ManufacturerPartNumber { get; set; }
    public string Description { get; set; }
    public int RequestedQuantity { get; set; }
    public DateTime? RequiredDate { get; set; }
    public PackagingRequirement PackagingRequirement { get; set; }
    public bool ReelRequirement { get; set; }
    public List<string> PreferredSuppliers { get; set; }
}
```

### 3.2 Product matching / mapping

```csharp
public class SupplierProductMapping
{
    public int Id { get; set; }
    public string ErpArticleId { get; set; }
    public string SupplierCode { get; set; }
    public string SupplierPartNumber { get; set; }
    public string Manufacturer { get; set; }
    public string ManufacturerPartNumber { get; set; }
    public MatchConfidence MatchConfidence { get; set; } // Exact/Verified/High/Medium/Low/Unknown
    public DateTime CreatedAt { get; set; }
}

public enum MatchConfidence { Exact, Verified, High, Medium, Low, Unknown }
```

### 3.3 SupplierOffer / Packaging

```csharp
public class SupplierOffer
{
    public int Id { get; set; }
    public int PurchaseRequestLineId { get; set; }
    public string SupplierCode { get; set; }
    public string SupplierPartNumber { get; set; }
    public string Manufacturer { get; set; }
    public string ManufacturerPartNumber { get; set; }

    public int RequestedQuantity { get; set; }
    public int OfferedQuantity { get; set; }

    public decimal UnitPrice { get; set; }
    public string Currency { get; set; }
    public decimal TotalPrice { get; set; }

    public int MinimumOrderQuantity { get; set; }
    public int OrderMultiple { get; set; }

    public PackagingType PackagingType { get; set; }
    public int PackagingQuantity { get; set; }
    public ReelType? ReelType { get; set; }
    public decimal ReelingFee { get; set; }

    public int AvailableQuantity { get; set; }
    public int LeadTimeDays { get; set; }
    public DateTime EstimatedDeliveryDate { get; set; }

    public decimal ShippingCost { get; set; }
    public decimal Tax { get; set; }
    public decimal LandedCost { get; set; }

    public MatchConfidence MatchConfidence { get; set; }
    public DateTime OfferTimestamp { get; set; }
    public DateTime ExpiresAt { get; set; }
    public OfferStatus Status { get; set; }
}

public enum PackagingType { OriginalReel, ReReel, EitherReel, CutTape, Tray, Tube, Any }
public enum ReelType { ManufacturerOriginal, SupplierReReel }
```

### 3.4 Selectie, PO, Supplier Order

```csharp
public class SupplierSelection
{
    public int Id { get; set; }
    public int PurchaseRequestLineId { get; set; }
    public int SelectedOfferId { get; set; }
    public string ReasonSummary { get; set; }     // menselijk leesbare uitleg, zie §7
    public DateTime SelectedAt { get; set; }
    public SelectionMode Mode { get; set; }        // Automatic / Manual
}

public class PurchaseOrder
{
    public int Id { get; set; }
    public string ErpPoNumber { get; set; }
    public int PurchaseRequestId { get; set; }
    public List<PurchaseOrderLine> Lines { get; set; }
}

public class SupplierOrder
{
    public int Id { get; set; }
    public int ErpPoId { get; set; }
    public string ErpPoNumber { get; set; }
    public string SupplierCode { get; set; }
    public string SupplierOrderNumber { get; set; }
    public string IdempotencyKey { get; set; }
    public DateTime OrderDate { get; set; }
    public string Currency { get; set; }
    public decimal OrderTotal { get; set; }
    public SupplierOrderStatusEnum Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public List<SupplierOrderLine> Lines { get; set; }
}

public enum SupplierOrderStatusEnum
{
    Created, Submitted, Acknowledged, Confirmed, PartiallyConfirmed,
    Backorder, PartiallyShipped, Shipped, Completed, Cancelled, Rejected
}
```

### 3.5 Configuratie-entiteiten (business rules als data)

```csharp
public class SupplierPreference
{
    public string SupplierCode { get; set; }
    public int Priority { get; set; }
    public bool Active { get; set; }
    public bool AllowedForAutoOrder { get; set; }
}

public class PackagingPolicy
{
    public string ComponentCategory { get; set; }
    public int MinimumQuantity { get; set; }
    public PackagingType PreferredPackaging { get; set; }
    public List<PackagingType> AllowedPackaging { get; set; }
    public bool OriginalReelRequired { get; set; }
}

public class ApprovalPolicy
{
    public decimal MaxOrderValueForAutoApproval { get; set; }
    public decimal MaxPriceVariancePercentage { get; set; }
    public bool AllowExternalSupplier { get; set; }
    public bool AllowNonOriginalPackaging { get; set; }
    public bool AllowAlternativePart { get; set; }
}
```

Deze drie policy-tabellen worden via een **Instellingen-scherm** in de WinForms-app beheerd (zie §8.6) — geen hard-coded regels in de code.

---

## 4. ERP-koppelvlak (mock voor dit prototype)

```csharp
public interface IErpConnector
{
    Task<IReadOnlyList<PurchaseRequest>> GetOpenPurchaseRequestsAsync();
    Task<string> CreatePurchaseOrderAsync(PurchaseOrderDraft draft); // retourneert ErpPoNumber
    Task UpdatePurchaseOrderStatusAsync(string erpPoNumber, string status);
}
```

`MockErpConnector` implementeert dit bovenop de eigen SQL Server-database van het prototype: een scherm in de UI laat de gebruiker testmatige purchase requests invoeren/importeren (zie §8.1), die vervolgens als "ERP-data" door de rest van de engine worden behandeld. Dit maakt de knip naar een echte UniPro-koppeling later klein: alleen `IErpConnector` opnieuw implementeren.

---

## 5. Supplier Capability Registry

```csharp
public class SupplierCapabilities
{
    public CapabilityStatus ProductSearch { get; set; }
    public CapabilityStatus Pricing { get; set; }
    public CapabilityStatus Availability { get; set; }
    public CapabilityStatus Packaging { get; set; }
    public CapabilityStatus Ordering { get; set; }
    public CapabilityStatus OrderStatus { get; set; }
    public CapabilityStatus Shipment { get; set; }
    public CapabilityStatus Tracking { get; set; }
    public CapabilityStatus Invoice { get; set; }
}

public enum CapabilityStatus { Supported, NotSupported, NotAvailable, ManualProcess }
```

Voor de MVP-adapters:

| Capability | DigiKey | Farnell |
|---|---|---|
| ProductSearch | Supported | Supported |
| Pricing | Supported | Supported |
| Availability | Supported | Supported |
| Packaging | Supported | Supported |
| Ordering | Supported | Supported |
| OrderStatus | Supported | Supported |
| Shipment | ManualProcess (fase 2) | ManualProcess (fase 2) |
| Tracking | ManualProcess (fase 2) | ManualProcess (fase 2) |
| Invoice | ManualProcess (fase 2) | ManualProcess (fase 2) |

Ontbrekende functionaliteit mag de workflow nooit doen falen — de engine slaat de betreffende stap over of markeert deze als "niet beschikbaar bij deze leverancier".

---

## 6. Kernworkflow (fase 1)

Dit is de centrale flow die `ProcurementEngine` moet implementeren:

1. **Purchase Request ophalen** via `IErpConnector.GetOpenPurchaseRequestsAsync()`.
2. **Valideren** van de request (verplichte velden aanwezig).
3. **Product resolven**:
   - Bekende `SupplierProductMapping`? → gebruik direct.
   - Anders: zoek op `Manufacturer + ManufacturerPartNumber` bij elke actieve, geconfigureerde supplier-adapter (parallel, via `Task.WhenAll`).
   - Sla nieuwe matches op als `SupplierProductMapping` met een `MatchConfidence`.
4. **Offers per supplier opbouwen**: pricing, availability en packaging ophalen en normaliseren naar `SupplierOffer`.
5. **Packaging/hoeveelheid bepalen**: `ordered_quantity = ceil(requested_quantity / order_multiple) * order_multiple`, rekening houdend met MOQ, standaard reel-grootte en de geconfigureerde `PackagingPolicy` / reel-policy (zie §7).
6. **Landed cost berekenen** per offer: `product_cost + reeling_cost + shipping_cost + fees`.
7. **Supplier selection**: filter op geldige match (`Exact`/`Verified`), voldoende voorraad, packaging-eis, levertijd ≤ required date, en kies laagste landed cost onder de toegestane opties (zie voorbeeldberekening in §7).
8. **Automatisch vs. handmatig**: toets tegen `ApprovalPolicy` (orderwaarde, prijsafwijking, externe leverancier, alternatief onderdeel, fuzzy match). Bij twijfel → **Manual approval**-scherm.
9. **ERP PO aanmaken** via `IErpConnector.CreatePurchaseOrderAsync()` — dit gebeurt **altijd vóór** de supplier order.
10. **Supplier order plaatsen** via de adapter, met idempotency key (zie §10).
11. **Order confirmation verwerken** en ERP PO-status bijwerken.
12. **Alles auditen** (zie §9).

---

## 7. Selectielogica – voorbeeld (uit het technisch voorstel, ter referentie voor unit tests)

```
Gevraagd: 12.000 stuks, reel vereist, originele reel voorkeur

DigiKey:    reel = 5.000  → 15.000 stuks × €0,11
Farnell:    reel = 2.500  → 12.500 stuks × €0,115
Supplier C: reel = 4.000  → 12.000 stuks × €0,105 (re-reel)

Als ORIGINAL_REEL_REQUIRED = true  → Supplier C afgewezen
Als EITHER_REEL = true             → Supplier C blijft in de vergelijking
```

Deze casus is een goede basis voor een unit test van de selectie-engine: implementeer 'm als testscenario in `Procurement.Tests`.

Elke automatische selectie moet een `ReasonSummary` opslaan, bijvoorbeeld:

```
Geselecteerde leverancier: DigiKey
Reden: exacte MPN-match, originele reel beschikbaar, voldoende voorraad,
levering binnen vereiste datum, laagste landed cost.
```

---

## 8. WinForms – schermen (prototype-UI)

### 8.1 Purchase Requests-overzicht (hoofdscherm)
- Grid met openstaande purchase requests (status: Pending, Sourcing, Waiting approval, Ready to order, Ordered, Exception).
- Knop "Nieuwe testaanvraag toevoegen" (omdat er nog geen echte ERP-koppeling is) → eenvoudig invoerformulier voor `PurchaseRequestLine` (artikel, manufacturer, MPN, hoeveelheid, packaging-eis, gewenste datum).
- Knop "Sourcing starten" per regel/aanvraag.

### 8.2 Offer-vergelijkingsscherm
- Grid met alle `SupplierOffer`'s voor een regel, kolommen: leverancier, supplier P/N, match confidence, aangeboden hoeveelheid, unit price, packaging/reel, landed cost, levertijd, voorraad.
- Duidelijke markering welke offer wordt voorgesteld door de engine, met de `ReasonSummary` zichtbaar.
- Mogelijkheid om handmatig een andere offer te kiezen (met verplichte reden-invoer, wat de audit-log ingaat).

### 8.3 Goedkeuringsscherm (Manual approval)
- Toont alleen regels die niet aan de auto-approve criteria voldoen, met de reden waarom (bv. "onbekende leverancier", "fuzzy match", "prijsafwijking >10%").
- Goedkeuren / afwijzen, met commentaarveld.

### 8.4 Order-detailscherm
- Toont `PurchaseOrder` + gekoppelde `SupplierOrder`(s), status, ERP PO-nummer, supplier-ordernummer, regels.
- Statusbalk conform de centrale statussen uit §3.4.

### 8.5 Supplier-mapping beheer
- Overzicht van `SupplierProductMapping` met filter op match confidence; hier kan een gebruiker een `Unknown`/`Low` match handmatig corrigeren naar `Verified`.

### 8.6 Instellingen
- Tabbladen voor `SupplierPreference`, `PackagingPolicy`, `ApprovalPolicy` (zie §3.5) — CRUD-schermen zodat regels niet in code hoeven te worden aangepast.
- Tabblad "Supplier capabilities" (read-only weergave van §5-tabel, later evt. bewerkbaar).

### 8.7 Audit-log viewer
- Doorzoekbaar overzicht van `ProcurementEvent`s (zie §9), filterbaar op entiteit, type, supplier, datum.

---

## 9. Audit log

```csharp
public class ProcurementEvent
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public string EventType { get; set; }   // bv. PRODUCT_SEARCHED, SUPPLIER_SELECTED, ERP_PO_CREATED, ...
    public string UserOrSystem { get; set; }
    public string SupplierCode { get; set; }
    public string RequestPayload { get; set; }   // gemaskeerd/zonder credentials
    public string ResponsePayload { get; set; }
    public string Status { get; set; }
    public string Error { get; set; }
}
```

Elke stap uit §6 schrijft minimaal één event weg. Credentials, persoonsgegevens en betaalinformatie worden nooit in `RequestPayload`/`ResponsePayload` gelogd — desnoods maskeren vóór opslag.

---

## 10. Idempotency

- Genereer per supplier order een idempotency key, bv. `PROC-{jaar}-{volgnummer}-{SUPPLIERCODE}`.
- Sla deze op vóórdat de order wordt verstuurd.
- Bij timeout: **niet** opnieuw versturen. Eerst `GetOrderStatusAsync`/query op de supplier uitvoeren om te checken of de order al bestaat; alleen bij afwezigheid opnieuw proberen.
- Combinatie `ErpPoNumber + SupplierCode + OrderVersion` moet uniek zijn (unique constraint in SQL Server).

---

## 11. Foutafhandeling (gestandaardiseerde supplier-fouten)

```csharp
public enum SupplierErrorCode
{
    ProductNotFound, InsufficientStock, InvalidQuantity, PackagingNotAvailable,
    PriceChanged, SupplierRejectedOrder, AuthenticationError, RateLimit,
    Timeout, UnknownError
}
```

Elke adapter vertaalt de eigen (mock-)foutcodes naar deze enum, zodat `ProcurementEngine` nooit supplier-specifieke fouten hoeft te kennen.

---

## 12. Database (SQL Server) – kernindicatie

Onderstaande tabellen zijn een startpunt voor het EF6 Code First-model (namen indicatief, pas gerust aan):

```
Supplier, SupplierCapability
SupplierProduct, SupplierProductMapping
PurchaseRequest, PurchaseRequestLine
SupplierOffer, SupplierOfferPackaging
SupplierSelection
PurchaseOrder, PurchaseOrderLine
SupplierOrder, SupplierOrderLine
ProcurementEvent
SupplierPreference, PackagingPolicy, ApprovalPolicy
```

Relaties volgen het schema uit het technisch voorstel (§34 aldaar): een intern artikel kan meerdere `SupplierProductMapping`s hebben; een `PurchaseRequestLine` heeft meerdere `SupplierOffer`s; één offer wordt via `SupplierSelection` gekoppeld aan een `PurchaseOrder` → `SupplierOrder`.

---

## 13. Fase 1 MVP – scope (expliciet)

**Wel:**
- ERP Purchase Request ophalen (mock)
- Artikelidentificatie + supplier mapping
- Product search, pricing, availability, packaging bij DigiKey- en Farnell-adapter (mock-modus)
- Reel/re-reel-selectie, MOQ, order multiple, quantity calculation
- Supplier selection met uitlegbare reden
- ERP PO aanmaken (mock), supplier order plaatsen (mock)
- Order confirmation verwerken
- Foutafhandeling, audit log, handmatige goedkeuring

**Nog niet (fase 2, buiten scope van dit prototype):**
- Volledige shipment tracking, automatische goods receipt
- Invoice processing / 3-way match / AP-automatisering
- Echte UniPro-koppeling
- Echte DigiKey/Farnell-credentials en live API-calls

---

## 14. Acceptatiecriteria voor het prototype

1. Een testmatige purchase request kan via de UI worden ingevoerd.
2. Sourcing doorloopt beide (mock-)adapters parallel en toont genormaliseerde offers.
3. MOQ, order multiple en packaging/reel-logica rekenen zichtbaar correct (zie testcasus §7).
4. Landed cost wordt correct berekend en gebruikt voor selectie.
5. Automatische selectie toont een leesbare `ReasonSummary`.
6. Regels die niet aan de approval-policy voldoen gaan verplicht naar het goedkeuringsscherm.
7. Een (mock-)ERP PO wordt aangemaakt vóórdat de (mock-)supplier order wordt geplaatst.
8. Een herhaalde/dubbele orderpoging bij dezelfde idempotency key leidt niet tot een dubbele order.
9. Elke stap is terug te vinden in de audit-log viewer.
10. Een nieuwe (mock-)supplier-adapter kan worden toegevoegd door alleen `ISupplierAdapter` te implementeren, zonder wijzigingen aan `ProcurementEngine`.

---

## 15. Aannames (graag bevestigen of corrigeren voordat Claude Code begint)

- **EF6 Code First** wordt gebruikt voor de datalaag (alternatief: puur ADO.NET — laat het weten als dit de voorkeur heeft).
- Mock-adapters gebruiken **vaste, realistische testdata** (vergelijkbaar met het voorbeeld in §7) in plaats van echte API-calls; de HTTP-laag wordt als losse, makkelijk vervangbare klasse opgezet.
- De UI is functioneel/eenvoudig (standaard WinForms-controls, DataGridView), geen custom styling nodig voor dit prototype.
- Eén gebruiker/geen rollen-/rechtenmodel in dit prototype (kan later worden toegevoegd als UniPro-integratie een rechtensysteem meebrengt).
- Valuta: aanname is EUR als standaard; ondersteuning voor meerdere valuta's is voorzien in het model (`Currency`-veld) maar hoeft in dit prototype niet volledig te worden uitgewerkt.

Laat het weten als een van deze aannames niet klopt, dan pas ik de spec aan voordat de implementatie start.
