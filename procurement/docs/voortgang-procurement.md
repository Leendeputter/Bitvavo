# Voortgang componenteninkoop-prototype

Status per 2026-08-15, branch `claude/functionele-specificatie-wu6bom`. Dit document legt vast waar
het project nu staat: wat werkt, wat gebouwd maar nog niet getest is, en wat er nodig is om verder te
komen. Voor de architectuur/ontwerpkeuzes in detail: zie `procurement/README.md`. Voor de oorspronkelijke
requirements: zie `functionele-specificatie-procurement.md` in deze map.

## Wat vandaag end-to-end werkt (mock-modus)

De volledige workflow uit spec §6 is te doorlopen zonder externe afhankelijkheden, op basis van
deterministische testdata:

1. **Inloggen** — echt, via dezelfde MAX-infrastructuur (`MaxSQL`, `ExactRMCompanies`) als UniPro.
2. **Purchase requests ophalen** — echt, rechtstreeks uit MAX's `Order_Master`/`Part_Master`
   (Query-knop), met filters (status, Due Date range, Select By).
3. **Sourcing** — leverancier-vergelijking via 11 supplier-adapters (zie hieronder), matching tegen
   `SupplierProductMapping` (incl. automatische sync vanuit MAX's `Part_Vendor`), automatische keuze
   of handmatige goedkeuring bij afwijking.
4. **Order plaatsen** — bundelt regels per leverancier tot één PO; in mock-modus (huidige default)
   geschreven naar dit prototype's eigen `Procurement_PurchaseOrder`-tabellen, nog niet naar een
   echte MAX-PO (zie "MAX-integratie" hieronder).
5. **Orderbevestiging verwerken** — haalt bevestigingen op bij de leverancier-adapter, past ze
   automatisch toe, toont alleen afwijkingen (aantal/prijs/te late levering) ter beoordeling.
6. **Goedkeuringen, supplier-mapping, audit-log, instellingen** — allemaal werkend.

## Suppliers — status per leverancier

| Supplier | Zoeken/prijzen/voorraad | Bestellen | Vertrouwen |
|---|---|---|---|
| DigiKey | Echte HTTP-laag (OAuth2) | Mock-only | Ongetest tegen sandbox, geen credentials |
| Farnell/element14 | Echte HTTP-laag (API-key) | Mock-only | Ongetest tegen sandbox, geen credentials |
| Mouser | Echte HTTP-laag (API-key) | Mock-only | Ongetest, geen credentials |
| TME | Echte HTTP-laag (HMAC-signing) | Mock-only | Ongetest **en** het signing-schema zelf is het minst zekere stuk code in dit hele project — uit herinnering gereconstrueerd, niet uit documentatie |
| Arrow, Rutronik, Avnet/Silica, Karl Kruse, RS Components, Distrelec, Conrad | Mock-only | Mock-only | Geen bevestigde publieke API — vermoedelijk account-/EDI-toegang nodig, bewust geen giswerk |

Bestellen (`CreateOrderAsync`/`GetOrderStatusAsync`) blijft voor **alle** suppliers mock-only, ook
zodra credentials er zijn: het risico van een verkeerde aanname is daar een echte, foute bestelling
bij een leverancier — een ander risiconiveau dan een mislukte zoekopdracht.

## MAX-integratie — gebouwd, nog niet live getest

Alles hieronder is gebaseerd op MaxOrderModule's eigen (gedeeltelijk gedecompileerde) broncode en,
voor de orderbevestiging-velden, directe bevestiging door de gebruiker van de exacte MAX-velden.
Niets hiervan is ooit tegen een echte MAX-administratie gedraaid.

- **PR → PO omzetten** (`MaxPurchaseOrderRepository.CreatePurchaseOrderAsync`, via `AddPODetail`):
  zet aan via `MaxErpConnector.UseMockPurchaseOrders = false` (nu: `true`, de veilige default).
  Verwijdert ook de originele PR-regel (`DeletePurchaseRequisitionLineItem`) zodat hij niet dubbel
  in MAX blijft staan.
- **Orderbevestiging naar MAX schrijven** (`MaxPurchaseOrderRepository.ApplyConfirmationAsync`, via
  `ChangePOHeading`/`ChangePODetail`): schrijft `Purchase_Order_Code.CONFRM_16` (leverancier
  order-/confirmation-nummer), `Order_Master.ORDREF_10` (`"O "`/`"OP "`-prefix, spec bevestigd door
  gebruiker), en `Order_Master.CURDUE_10` (bevestigde leverdatum, alleen bij afwijking). Dit is de
  **eerste** plek die een bestaande MAX-rij wijzigt i.p.v. alleen nieuwe rijen toevoegt — bewust
  smal gehouden tot precies deze drie velden.
- **Openstaande onzekerheden** (zie ook de class-comment van `MaxPurchaseOrderRepository`):
  - `AssignPRsToPO`/`AssignPONumber` zijn mogelijk een "nettere", atomaire manier om een PR om te
    zetten (i.p.v. nieuw aanmaken + apart verwijderen), maar `OrderAssign`'s velden en `TargetOrder`'s
    exacte gedrag zijn niet bekend uit de aangeleverde bron.
  - `FixVar`/`RoundType` (parameters van `AddPODetail`) — betekenis nog niet bevestigd, waarden
    ongewijzigd overgenomen uit het originele werkende voorbeeld.
  - `ChangePODetail`'s interne herberekeningslogica bij een `CURDUE_10`-wijziging (rond
    `DUEQTY_10`/mogelijk `Requirement_Detail`) is niet volledig na te trekken uit de sterk
    geobfusceerde gedecompileerde bron — gebruiker heeft bevestigd dat dat voor dit gebruik
    acceptabel is (de dagelijkse MRP-run herstelt `Requirement_Detail` sowieso).

## Recent opgeloste bugs (deze sessie, bij de eerste echte rebuild)

Tot nu toe is alle code hier zorgvuldig gelezen maar nooit gecompileerd (geen .NET-toolchain
beschikbaar in de ontwikkelomgeving) — de eerste paar keer dat de gebruiker daadwerkelijk bouwde
kwamen er dan ook een aantal echte fouten naar boven, ondertussen allemaal opgelost:
- Verkeerde namespace-aanname (`MaxOrderNET` i.p.v. `MaxOrder`) voor `MaxOrderModule`.
- `decimal`→`double`-conversiefout op `FORCUR_10`.
- `ORDER_10` bleef leeg op niet-eerste PO-regels (AddPODetail vult dat alleen bij de eerste regel).
- Dapper's `QuerySingleOrDefaultAsync` bestaat niet in de vastgepinde oude Dapper-versie (1.40.0).
- Twee losstaande UI-bugs: elke actieknop schakelde alleen zichzelf uit (dus een andere knop
  klikken tijdens een lopende actie liet twee operaties tegelijk op dezelfde MAX-connectie botsen),
  en een bekende DataGridView-cursor-eigenaardigheid (cursor bleef "busy" tonen boven de grid).

## Wat nodig is om verder te komen

1. **Supplier-credentials** (DigiKey/Farnell/Mouser/TME) — verwacht via inkoop. Zodra beschikbaar:
   één supplier tegelijk op `UseMockData = false` zetten en stap voor stap testen (Zoeken → Product →
   Prijzen → Voorraad → Verpakking) vóórdat erop vertrouwd wordt, zeker voor TME.
2. **Een MAX test-/sandbox-administratie** — om `UseMockPurchaseOrders = false` en de
   orderbevestiging-velden voor het eerst tegen echte data te testen, vóórdat dit tegen een
   live-administratie draait.
3. **MAX Part_Vendor.VENID_07-codes** voor de negen nieuwe distributeurs (Arrow, Rutronik, etc.) —
   nu leeg, in te vullen via Instellingen → Suppliers zodra bekend.

## Wat nog helemaal niet gebouwd is

- Real-mode HTTP-integratie voor de zeven distributeurs zonder bevestigde publieke API.
- Elk vorm van bestellen (`CreateOrderAsync`) buiten mock-modus, voor alle elf suppliers.
- Een bevestigde MAX PO-statusupdate-methode los van de nu gebouwde Confirming/Reference/duedate-
  route (bv. voor een geannuleerde of volledig ontvangen order).
