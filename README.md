# Bitvavo Trading Bot

A Windows Forms (.NET 8) desktop application for monitoring the Bitvavo spot market and running
an automated trading bot, with a full **Papertrading** simulation mode so strategies and risk
rules can be tested without risking real funds. See
[`docs/functionele-specificatie.md`](docs/functionele-specificatie.md) for the full functional
specification this implementation follows.

This repository also contains an unrelated, standalone prototype — an automated components
procurement app (DigiKey/Farnell) built on Windows Forms/.NET Framework 4.8/EF6 — under
[`procurement/`](procurement/README.md). It shares no code with the trading bot; see its own
README for build/run instructions.

## Project structure

```
BitvavoBot.sln
src/
  BitvavoBot.Domain              Entities, enums, interfaces, calculation rules (spec 4.2) — no dependencies
  BitvavoBot.Data                SQLite schema/migrations + Dapper repositories
  BitvavoBot.Exchange.Bitvavo    Live Bitvavo REST/WebSocket client
  BitvavoBot.Exchange.Paper      Papertrading simulation engine (order matching, fees, leverage)
  BitvavoBot.Trading             Risk engine, trading engine, the 4 strategies, bot orchestration
  BitvavoBot.Infrastructure      Serilog logging, JSON settings, Windows DPAPI credential protection
  BitvavoBot.App                 WinForms UI (net8.0-windows) — the executable project
tests/
  BitvavoBot.Tests               xUnit tests for calculation rules and Papertrading order matching
```

## Prerequisites

- **Windows 10/11** (the app itself; WinForms does not run on Linux/macOS).
- Any **.NET 8 or .NET 9 SDK** ([download](https://dotnet.microsoft.com/download/dotnet)), with
  the Windows Desktop workload (included by default on Windows). The solution targets `net8.0` /
  `net8.0-windows`; a .NET 9 SDK can build it as-is (newer SDKs always support building
  still-supported older target frameworks), and `RollForward=LatestMajor` is set on the runnable
  projects (`BitvavoBot.App`, `BitvavoBot.Tests`) so they also *run* on a machine that only has
  the .NET 9 runtime installed, without needing the .NET 8 runtime side-by-side.
- A Bitvavo account and API key/secret if you intend to use **Live** mode
  ([Bitvavo API settings](https://account.bitvavo.com/user/api)). Not required for Papertrading.

## Building

```powershell
dotnet restore
dotnet build
```

`BitvavoBot.App` targets `net8.0-windows` with `UseWindowsForms=true` and can only be built on a
machine with the .NET Windows Desktop SDK components installed (standard on Windows; not available
on Linux/macOS SDK installs). All other projects (`Domain`, `Data`, `Exchange.*`, `Trading`,
`Infrastructure`, `Tests`) are plain `net8.0` and build/run cross-platform.

## Running

```powershell
dotnet run --project src/BitvavoBot.App
```

On first launch the app:
1. Creates its data directory at `%LocalAppData%\BitvavoBot\` (SQLite database, `settings.json`,
   and `logs\`).
2. Runs database migrations automatically.
3. Starts in **Papertrading** mode by default — no real orders can be placed until you
   deliberately switch to Live mode (functional spec 1.2, 8.5). The active mode is always shown in
   the status bar at the bottom of the window.

## Configuring Bitvavo API credentials (for Live mode)

1. Open **Instellingen** in the app.
2. Enter your Bitvavo API key and secret. They are encrypted at rest with Windows DPAPI
   (`ICredentialProtector`) before being written to `settings.json` — never stored in plain text.
3. Restart the application (the REST/WebSocket clients are constructed once at startup with
   whatever credentials are available at that time).
4. Create a bot profile via **Trading Bot → Nieuw profiel** and choose Mode = Live in step 8, or
   use **Promoveer naar Live** on an existing, tested Papertrading profile.

## Papertrading

- Create one or more Papertrading profiles from the **Papertrading** screen (name + starting
  balance + optional slippage %). Each profile has its own isolated virtual balance, orders,
  positions and trade history — Papertrading data is never mixed with Live data or with another
  Papertrading profile (functional spec 8.3).
- Papertrading uses the same live market data (ticker/order book) as Live mode; only order
  execution is simulated (functional spec 8.2).
- Use **Reset profiel** to wipe a profile's balance/orders/positions/history back to its starting
  state.

## Running the tests

```powershell
dotnet test tests/BitvavoBot.Tests/BitvavoBot.Tests.csproj
```

These tests run on any OS (no Windows Desktop dependency) and cover the buy/sell margin formulas,
open-orders-percentage rule, stop-loss/take-profit triggers, tick-size rounding, Position
weighted-average tracking, Papertrading order matching (market/limit fills, fees, cancellation,
realized P&L), the risk engine's daily/investment/concurrency limits, and determinism of all four
strategies (Grid Trading, Market Making, Mean Reversion, Moving Average).

## Known limitations (v1)

- Leverage/margin trading (functional spec 6) is only usable in Papertrading — Bitvavo's public API
  has no Live margin/futures product, so the Live leverage client throws `NotSupportedException` by
  design. The architecture (`IExchangeLeverageClient`) is ready for a future Bybit/Binance Futures
  implementation.
- Historical/backtest simulation (functional spec 8.4) is not implemented; only real-time
  Papertrading is, as the spec marks this the v1 requirement.
- The bot profile wizard applies one strategy + parameter set to every selected market in that
  profile (the data model supports per-market overrides via `StrategyAssignment`, but the current
  UI does not expose editing them individually).
- Market Monitor favorites (star toggle) are session-only; they reset when the app restarts.
