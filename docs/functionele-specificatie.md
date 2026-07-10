# Functionele Specificatie – Bitvavo Trading Bot (WinForms / C# / .NET 8)

**Versie:** 1.0
**Doel van dit document:** deze functionele specificatie is bedoeld als werkdocument voor implementatie door Claude Code. Het beschrijft *wat* de applicatie moet doen (functioneel gedrag, schermen, regels, databehoeften), als aanvulling op de technische specificatie. Het is geschreven zodat een engineer (of AI-agent) de applicatie stap voor stap kan bouwen zonder aannames te hoeven maken over onduidelijke punten.

Ten opzichte van de oorspronkelijke technische specificatie is hier een **Papertrading-module** toegevoegd (zie hoofdstuk 8), waarmee alle strategieën en risicoregels getest kunnen worden zonder echte orders op Bitvavo te plaatsen.

---

## 1. Overzicht en scope

### 1.1 Wat de applicatie doet
Een Windows Forms desktopapplicatie waarmee een gebruiker:
- De spotmarkt van Bitvavo realtime kan monitoren.
- Een geautomatiseerde trading bot kan configureren en laten draaien (spot).
- Optioneel leverage/futures-achtige posities kan beheren (generieke architectuur, exchange-agnostisch).
- Strategieën kan selecteren, configureren en combineren.
- Alles **eerst risicovrij kan testen in Papertrading-modus**, voordat er met echt geld gehandeld wordt.
- Volledig inzicht heeft in orders, posities, logging en resultaten, opgeslagen in een lokale SQLite-database.

### 1.2 Belangrijk uitgangspunt: Live vs. Papertrading
Er is een **globale modus-schakelaar** die geldt voor de hele applicatie:

| Modus | Omschrijving |
|---|---|
| **Live** | Echte orders worden via de Bitvavo REST API geplaatst. Echt saldo wordt gebruikt. |
| **Papertrading** | Geen enkele order wordt daadwerkelijk naar Bitvavo verstuurd. Alle orders, fills, saldo en P&L worden gesimuleerd op basis van live (of historische) marktdata. |

Deze modus moet op elk moment zichtbaar zijn in de UI (zie 3.1) en moet niet per ongeluk verwisseld kunnen worden tijdens een actieve sessie (zie 8.5, bevestigingsstap).

### 1.3 Niet in scope (v1)
- Geen mobiele app.
- Geen cloud-hosting/multi-user; single-user desktopapplicatie.
- Geen daadwerkelijke koppeling met Bybit/Binance in v1 — alleen de architectuur moet dit toelaten (interfaces, zie hoofdstuk 9).

---

## 2. Globale UI-structuur

### 2.1 Hoofdmenu
1. Dashboard
2. Marktmonitor
3. Trading Bot
4. Open Orders
5. Posities
6. Leverage Trading
7. Strategieën
8. **Papertrading** *(nieuw)*
9. Logging
10. Instellingen

### 2.2 Statusbalk (permanent zichtbaar, onderaan elk scherm)
- Huidige modus: `LIVE` (rood) of `PAPERTRADING` (blauw/geel), altijd duidelijk onderscheiden qua kleur.
- WebSocket-verbindingsstatus (Verbonden / Reconnecting / Losgekoppeld).
- Actief strategie-profiel (indien bot draait).
- Laatste sync-tijdstip met Bitvavo.

### 2.3 Dashboard (nieuw scherm, samenvattend)
Een startscherm met:
- Huidig saldo (Live) en/of gesimuleerd saldo (Papertrading), duidelijk gelabeld.
- Aantal open orders / open posities.
- Dagelijkse P&L (Live en Papertrading apart getoond, niet gemengd).
- Status van de bot (Gestart/Gestopt/Gepauzeerd) met start/stop/pause-knoppen.
- Waarschuwingen (bijv. dagelijkse verlieslimiet bijna bereikt).

---

## 3. Marktmonitor — functionele eisen

### 3.1 Gedrag
- Toont alle handelsparen die Bitvavo aanbiedt, met kolommen zoals in de technische spec (Bid, Ask, Spread%, 24h Volume, 24h Change%, Laatste koers, Hoog/Laag 24h).
- Updates komen primair via WebSocket; als de WebSocket-verbinding wegvalt, valt het scherm automatisch terug op REST-polling met het ingestelde interval, en toont dit duidelijk aan de gebruiker ("Fallback naar polling — WebSocket verbroken").
- Sorteren: klik op kolomkop sorteert oplopend/aflopend; huidige sortering blijft behouden bij nieuwe data-updates (geen "springende" rijen).
- Filteren/zoeken: tekstveld filtert op marktnaam (bijv. "BTC" toont alle BTC-paren).
- Favorieten: ster-icoon per rij; favorieten kunnen bovenaan gepind worden via een toggle "Toon alleen favorieten".
- Export naar CSV: exporteert de *huidige gefilterde/gesorteerde weergave*, met kolomkoppen, naar een door de gebruiker gekozen bestandslocatie.

### 3.2 Acceptatiecriteria
- Bij 100+ actieve markten mag de UI niet merkbaar vertragen (zie NFR performance).
- Verlies van WebSocket-verbinding mag nooit een exception tonen aan de gebruiker; altijd nette fallback + logregel (Warning).

---

## 4. Trading Bot — functionele eisen

### 4.1 Configuratie-flow
De gebruiker doorloopt bij het instellen van een bot-profiel de volgende stappen (kan als wizard of als één scherm met secties):
1. Handelsbedrag (vast bedrag óf percentage van saldo).
2. Handelsparen (multi-select, met "alles selecteren" en "favorieten selecteren").
3. Scrape/analyse-interval.
4. Limiet order instellingen (koopmarge %, verkoopmarge %).
5. Open orders controle (minimum % open orders + actie bij onderschrijding).
6. Risicobeheer (max trades/dag, max verlies/dag, max investering, stop loss %, take profit %, max gelijktijdige orders).
7. Strategie-selectie (koppeling naar hoofdstuk 6/Strategieën).
8. **Modus-keuze: Live of Papertrading** (zie hoofdstuk 8).

Elk profiel kan worden opgeslagen onder een naam, zodat meerdere configuraties naast elkaar kunnen bestaan (bijv. "Conservatief BTC" en "Agressief Altcoins").

### 4.2 Berekeningsregels (expliciet, om ambiguïteit te voorkomen)

**Koopmarge:**
```
Kooplimietprijs = Huidige bid × (1 − koopmarge%)
```
Voorbeeld: bid = €100, koopmarge = 1% → kooplimiet = €99,00.

**Verkoopmarge:**
```
Verkooplimietprijs = Aankoopprijs × (1 + verkoopmarge%)
```
Voorbeeld: aankoop = €100, verkoopmarge = 2% → verkooplimiet = €102,00.

**Percentage open orders:**
```
% open = (aantal orders met status "open" / totaal geplaatste actieve orders binnen het profiel) × 100
```
Zakt dit percentage onder de ingestelde drempel (bijv. 80%), dan:
1. Waarschuwing tonen in UI (Dashboard + Logging).
2. Logregel wegschrijven (type: Warning).
3. Optioneel (instelbaar aan/uit): automatisch nieuwe orders plaatsen om weer boven de drempel te komen, met inachtneming van "Max gelijktijdige orders" en overige risicolimieten.

**Dagelijkse limieten:**
- Max trades/dag en max verlies/dag resetten om 00:00 lokale tijd.
- Wanneer max verlies/dag wordt overschreden: bot stopt automatisch (nieuwe orders geblokkeerd), bestaande open orders blijven staan tenzij de gebruiker expliciet "Alles annuleren" gebruikt. Er wordt een duidelijke melding + logregel (Error/Warning) gegenereerd.

**Stop loss / Take profit:**
- Percentage-gebaseerd op aankoopprijs; monitoring gebeurt op basis van live marktdata (WebSocket) met een fallback-check via REST als de WebSocket wegvalt langer dan X seconden (instelbaar, default 30s).

### 4.3 Acceptatiecriteria
- Alle berekeningen moeten met minimaal 8 decimalen intern rekenen (om afrondingsfouten bij kleine cryptobedragen te voorkomen), afronden op de tick-size van de markt pas vlak vóór het versturen van de order.
- Elke geplaatste, gewijzigde of geannuleerde order (Live én Papertrading) wordt gelogd met alle relevante parameters.

---

## 5. Open Orders & Posities — functionele eisen

### 5.1 Open Orders
- Realtime overzicht (WebSocket) van alle orders die door de bot of handmatig geplaatst zijn.
- Filter op modus: Live-orders en Papertrading-orders worden **nooit** door elkaar getoond — een duidelijke toggle/tab bovenaan het scherm ("Live" / "Papertrading") bepaalt welke set zichtbaar is.
- Acties: individueel annuleren, "Alles annuleren" (met bevestigingsdialoog bij Live-modus), handmatig vernieuwen.

### 5.2 Posities
- Toont per asset: aantal, gemiddelde aankoopprijs, huidige prijs, P&L in € en %.
- Ook hier: aparte weergave/toggle voor Live vs. Papertrading-posities.
- Gemiddelde aankoopprijs wordt herberekend volgens gewogen gemiddelde bij elke nieuwe aankoop binnen dezelfde asset.

---

## 6. Leverage Trading — functionele eisen

- Architectuur via een generieke `IExchangeLeverageClient`-interface (zie hoofdstuk 9), zodat latere toevoeging van Bybit/Binance Futures geen wijziging in de UI-laag vereist.
- Instellingen: leverage (1x–20x), positietype (Long/Short), margin type (Cross/Isolated), positiegrootte (vast bedrag/percentage saldo).
- Risicobeheer: liquidatiebuffer (%), maximaal risico per trade (% van accountwaarde), verplichte Auto Stop Loss (kan niet uitgezet worden — UI-control is disabled/grayed-out, niet slechts "aangeraden"), optionele Auto Take Profit.
- **Ook leverage trading moet volledig in Papertrading-modus te testen zijn**, inclusief gesimuleerde liquidatieprijs-berekening.

---

## 7. Strategiebeheer — functionele eisen

Ondersteunde strategieën, elk als los, uitwisselbaar strategie-object achter een gemeenschappelijke `IStrategy`-interface:

| Strategie | Parameters |
|---|---|
| Grid Trading | Grid afstand %, aantal grids |
| Market Making | Spread %, ordergrootte, aantal niveaus |
| Mean Reversion | RSI-periode, RSI koopniveau, RSI verkoopniveau |
| Moving Average | Fast MA, Slow MA |

- Strategieën zijn te combineren met meerdere handelsparen tegelijk, elk met eigen parameterset indien gewenst.
- Elke strategie moet, gegeven dezelfde marktdata-invoer, deterministisch dezelfde signalen genereren in zowel Live als Papertrading-modus (cruciaal voor betrouwbaar testen — zie 8.2).
- Strategie-resultaten (backtests/papertrades) worden per strategie-run opgeslagen zodat prestaties onderling vergeleken kunnen worden.

---

## 8. Papertrading-module (nieuw)

### 8.1 Doel
De gebruiker moet elke bot-configuratie, elke strategie en elke risico-instelling kunnen testen **zonder echte orders te plaatsen en zonder echt saldo te riskeren**, met resultaten die zo realistisch mogelijk het gedrag van de Live-modus benaderen.

### 8.2 Functioneel gedrag
- Papertrading gebruikt **dezelfde live marktdata** (WebSocket/REST) als de Live-modus — er wordt niet met vertraagde of nagemaakte data gewerkt, tenzij de gebruiker expliciet kiest voor "Historische simulatie" (zie 8.4).
- Orders die de bot in Papertrading-modus wil plaatsen, worden **niet** naar de Bitvavo API gestuurd. In plaats daarvan:
  - Een limietorder wordt als "gevuld" beschouwd zodra de marktprijs (bid/ask, afhankelijk van orderzijde) het limietniveau bereikt of passeert, met een realistische wachttijd/slippage-simulatie (instelbaar, default: geen slippage, optioneel een slippage% toevoegen om realistischer te simuleren).
  - Een marktorder wordt direct gevuld tegen de actuele bid/ask, eventueel met een instelbare slippage%.
  - Orderboek-diepte kan optioneel meegenomen worden om te bepalen of een grote simulatie-order "realistisch" gevuld zou kunnen worden (geavanceerde optie, mag in v1 als eenvoudige aanname geïmplementeerd worden: order vult volledig zodra prijs bereikt is).
- Een gesimuleerd startsaldo is instelbaar door de gebruiker (bijv. €10.000 virtueel EUR-saldo), per Papertrading-profiel.
- Alle Papertrading-transacties, orders, posities en P&L worden **in dezelfde databasestructuur** opgeslagen als Live-data, maar met een duidelijk onderscheidend veld (`Mode = Live | Paper`) zodat rapportages, logging en UI-schermen consistent kunnen filteren.
- Transactiekosten (Bitvavo maker/taker fees) worden in de simulatie meegenomen op basis van de actuele Bitvavo-fee-structuur (op te halen via API of instelbaar in Instellingen), zodat de gesimuleerde P&L realistisch is.

### 8.3 Losstaand van Live
- Papertrading-saldo en Live-saldo zijn volledig gescheiden; het gesimuleerde saldo heeft geen enkele invloed op echte orders of het echte Bitvavo-saldo.
- Meerdere Papertrading-profielen kunnen naast elkaar bestaan (bijv. om twee strategieën te vergelijken), elk met eigen startsaldo en resultatengeschiedenis.
- Een "Reset Papertrading-profiel"-actie zet saldo, orders, posities en historie van dat profiel terug naar de beginstaat (met bevestigingsdialoog).

### 8.4 Optionele historische simulatie (backtesting-achtig, mag als latere iteratie)
- Naast "live" papertrading (real-time simulatie tegen actuele marktdata) kan optioneel historische data (candles, opgeslagen in SQLite) gebruikt worden om een strategie versneld te testen over een verleden periode.
- Dit onderdeel mag als aparte, latere fase geïmplementeerd worden; de kernvereiste voor v1 is de **real-time** Papertrading-simulatie.

### 8.5 UI-eisen
- Duidelijk zichtbare, niet te missen indicator van de actieve modus (zie 2.2), bijvoorbeeld een gekleurde banner bovenaan elk relevant scherm.
- Wisselen van Live naar Papertrading (of andersom) tijdens een actief draaiende bot vereist een bevestigingsdialoog ("Weet u zeker dat u wilt wisselen? De bot wordt gestopt voordat u kunt wisselen.").
- Een apart "Papertrading"-menu-item toont: huidig gesimuleerd saldo, open paper-orders, paper-posities, en een resultatenoverzicht (totaal rendement %, aantal trades, winrate, gemiddelde winst/verlies per trade).
- Rapportage-export (CSV) van de volledige paper-tradinghistorie, analoog aan de marktmonitor-export.

### 8.6 Acceptatiecriteria
- Het is voor de gebruiker op geen enkel moment mogelijk om per ongeluk een Papertrading-order als echte order bij Bitvavo te laten uitvoeren, en vice versa.
- Alle strategieën, risicobeheerregels (stop loss, take profit, dagelijkse limieten, open-orders-controle) werken **identiek** in Papertrading als in Live — enige verschil is dat de order-uitvoering gesimuleerd is in plaats van echt.
- Een geteste configuratie kan met één actie ("Promoveer naar Live") worden overgenomen als Live bot-profiel, zonder de instellingen opnieuw te moeten invoeren.

---

## 9. Architectuur — functionele randvoorwaarden

- Duidelijke scheiding tussen UI (WinForms views), presentatie-/applicatielogica (ViewModels/Presenters) en domeinlogica (Trading Engine, Strategie-engine, Risk Engine).
- Exchange-toegang achter interfaces (`IExchangeClient`, `IExchangeLeverageClient`) zodat:
  - Bitvavo de eerste, volledige implementatie is.
  - Een `PaperExchangeClient` dezelfde interface implementeert als de echte Bitvavo-client, maar orders lokaal simuleert in plaats van ze te versturen — dit is de kern van hoe Papertrading technisch wordt gerealiseerd (dependency injection bepaalt welke implementatie actief is, gebaseerd op de gekozen modus).
  - Latere exchanges (Bybit, Binance Futures) dezelfde interfaces kunnen implementeren.
- Strategie-engine werkt op basis van marktdata-events, ongeacht of die van een live WebSocket of een gesimuleerde/historische bron komen.

---

## 10. Data die per module moet worden vastgelegd (functioneel, niet DB-schema)

| Entiteit | Belangrijkste velden | Mode-veld nodig? |
|---|---|---|
| Trade | markt, prijs, aantal, kosten, winst/verlies, tijdstip | Ja (Live/Paper) |
| Order | order ID, status, markt, side, prijs, aantal, tijdstip | Ja (Live/Paper) |
| Positie | asset, aantal, gem. aankoopprijs, mode | Ja |
| Candle-historie | markt, timeframe, OHLCV | Nee (marktdata is mode-onafhankelijk) |
| Strategie-run | strategie, parameters, periode, resultaat-samenvatting | Ja |
| Papertrading-profiel | naam, startsaldo, huidig saldo, aanmaakdatum | N.v.t. (is zelf de mode-context) |
| Logregel | tijdstip, type (Info/Warning/Error/Trade), bericht, mode | Ja, indien trade-gerelateerd |

---

## 11. Niet-functionele eisen (aanvullend op technische spec)

- UI blijft te allen tijde responsief; alle Bitvavo API- en databasecalls zijn async.
- Automatische reconnect van de WebSocket-verbinding, met exponential backoff en duidelijke UI-status.
- API-credentials worden nooit in platte tekst opgeslagen (Windows DPAPI), ook niet voor Papertrading-gerelateerde instellingen zoals opgehaalde fee-structuur.
- Alle acties die geld raken (Live orders plaatsen/annuleren, modus wisselen) worden auditbaar gelogd.
- Duidelijk, niet-dubbelzinnig onderscheid tussen Live- en Papertrading-data in database, UI en exports, zodat er nooit per abuis Live-cijfers en Paper-cijfers gemengd gerapporteerd worden.

---

## 12. Opleverpunten voor Claude Code (implementatievolgorde-suggestie)

1. Projectstructuur en architectuur (solution, interfaces, dependency injection, incl. `PaperExchangeClient`).
2. SQLite-datamodel (incl. `Mode`-velden) en migraties.
3. Bitvavo REST/WebSocket-integratie (Live).
4. Papertrading-simulatie-engine (order matching tegen live marktdata, fees, slippage).
5. Trading Engine + Risk Engine (gedeeld tussen Live en Paper).
6. Strategie-engine met de vier strategieën uit hoofdstuk 7.
7. WinForms-schermen: Dashboard, Marktmonitor, Trading Bot, Open Orders, Posities, Leverage Trading, Strategieën, **Papertrading**, Logging, Instellingen.
8. Logging (Serilog) en instellingenbeheer (incl. DPAPI-encryptie).
9. Unit tests, met name voor de berekeningsregels (hoofdstuk 4.2) en de Papertrading order-matching-logica.
10. Installatiehandleiding.

---

*Einde functionele specificatie.*
