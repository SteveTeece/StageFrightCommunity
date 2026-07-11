# Outstanding Balances Dashboard Tile — Implementation Plan

> **Status:** Planned (not started). Plan only — no code changes yet.
> **Branch:** `004-outstanding-balances-tile`.
> **Spec:** `specs/004-outstanding-balances-tile/spec.md` (FR-001–FR-011, SC-001–SC-004).

## Context

The Finance module currently exposes two dashboard tiles — `FinanceDashboardTileProvider` (current balance + month-to-date income/expenses) and `CashFlowDashboardTileProvider` (6-month income/expense bar chart) — both backed by `IFinanceSummaryService`, an organisation-level GL aggregate service (`src/StageFright.Core/Contracts/IFinanceSummaryService.cs`). Per-member outstanding balances already exist as a first-class concept: `IMemberBalanceService.GetAllMemberBalancesAsync()` (`src/StageFright.Core/Modules/Finance/MemberBalanceService.cs`) returns one `MemberBalance` per member with `Balance > 0m`, and is already consumed by `MembersTile` to show an "Outstanding fees" alert chip. What does **not** exist yet is (a) an organisation-level split of outstanding balance by `FeeType` (Attendance vs. Annual), and (b) a month-by-month outstanding-balance trend for the current calendar year. This plan adds both, wires them into a new third Finance dashboard tile, and reuses `IMemberBalanceService` for the member count rather than duplicating that logic.

**Non-negotiables that shape everything (CLAUDE.md / constitution):** one class per file; `.razor` + `.razor.cs` pairs, no `@code` blocks; custom exceptions at service boundaries (not needed here — this feature is read-only aggregation, no new failure modes beyond what `GetTileDataAsync`'s existing try/catch-per-tile isolation already covers); exhaustive `Should_X_When_Y` tests; `dotnet build` + full `dotnet test` green after each phase; when summing financial amounts, only sum payment-related credit entries (i.e. the Member Receivable account), never all GL credit entries, to avoid double-counting double-entry postings.

---

## Core design decisions

### 1. Member count reuses `IMemberBalanceService`, not a new query

`IMemberBalanceService.GetAllMemberBalancesAsync()` already returns exactly "members with a positive net Member Receivable balance" — the same definition FR-002/FR-009 need, and it already collapses a member with both an outstanding Annual and an outstanding Attendance fee into one entry (`MemberBalance` is per-member, not per-fee). `MembersTile` already consumes this service for an equivalent count today, so the new tile calls the same service rather than re-deriving member-level logic in a new place. No changes to `IMemberBalanceService`/`MemberBalanceService` are needed.

### 2. Fee-type split is a new GL repository aggregate, scoped to the Member Receivable account only

`IGLRepository` already has three organisation-level aggregate methods with an established tuple-return convention (`GetBalanceTotalsAsync`, `GetAgingBucketsAsync`) and already restricts "outstanding" queries to the Member Receivable account (`GetMemberBalanceAsync`, `GetTotalOutstandingAsync`) specifically to avoid double-counting the other legs of double-entry postings (income, GST clearing). The new method follows the same shape:

```csharp
/// <summary>
/// Returns outstanding balances (Σdebits − Σcredits on the Member Receivable account)
/// split by FeeType, for fee-linked transactions only. Overpayment/adjustment lines
/// (FeeId == null) are excluded from the split but still count toward per-member and
/// total-outstanding figures elsewhere (GetMemberBalanceAsync/GetTotalOutstandingAsync).
/// </summary>
Task<(decimal Attendance, decimal Annual)> GetOutstandingByFeeTypeAsync(CancellationToken ct = default);
```

Implementation in `GLRepository` (EF Core): filter `Transactions` to `AccountId == SystemAccounts.MemberReceivableId && FeeId != null`, join `Fees` on `FeeId` to obtain `FeeType`, group by `FeeType`, project `Σdebits − Σcredits` per group. `FeeType.Other` is computed but simply not surfaced by the tile (out of scope per spec Assumptions).

### 3. Calendar-year trend reuses the existing point-in-time balance query — no new repository method

`IGLRepository.GetAccountBalanceAsync(accountId, asAt)` already returns "net balance of an account for all transactions dated on or before `asAt`" — exactly a month-end outstanding-balance snapshot when called with `SystemAccounts.MemberReceivableId` and each month's last instant. `FinanceSummaryService.GetOutstandingBalanceTrendAsync` calls this once per month from January through the month of `asOf` (mirrors `GetMonthlyCashFlowAsync`'s per-period loop, and `MemberBalanceService`'s existing per-member N-query style — consistent with how this codebase already builds monthly series). No new `IGLRepository` member is needed for this part.

### 4. Both new capabilities live on `IFinanceSummaryService`, not `IMemberBalanceService`

`IFinanceSummaryService`'s docstring is "GL-derived **organisation-level** finance figures for the dashboard" — the fee-type split and the monthly trend are both organisation-level aggregates (not per-member), so they belong here alongside `GetSummaryAsync`/`GetMonthlyCashFlowAsync`, keeping the existing module seam: `IMemberBalanceService` = per-member data, `IFinanceSummaryService` = organisation-level GL rollups. The tile component ends up injecting both services, the same way `MembersTile` already injects two services (`IMemberService` + `IMemberBalanceService`).

```csharp
Task<OutstandingFeeSummary> GetOutstandingFeeSummaryAsync(CancellationToken ct = default);
Task<IReadOnlyList<MonthlyOutstandingBalance>> GetOutstandingBalanceTrendAsync(DateTime asOf, CancellationToken ct = default);
```

New plain models (mirroring `FinanceSummary.cs` / `MonthlyCashFlow.cs` shape exactly):

- `OutstandingFeeSummary { decimal OutstandingAttendanceFees; decimal OutstandingAnnualFees; }`
- `MonthlyOutstandingBalance { int Year; int Month; decimal OutstandingBalance; }`

### 5. New tile follows the established "self-loading body, static provider TileData" pattern

Like `CashFlowDashboardTileProvider`/`AttendanceTrendDashboardTileProvider`, the provider returns an empty `TileData` (aside from `NavigateRoute`) and the tile body component loads its own data in `OnInitializedAsync`. `TileId = "finance-outstanding-balances"`, `DisplayOrder = 45` (between Finance at 40 and Cash flow at 50 — keeps the three Finance tiles adjacent in dashboard order), `NavigateRoute = "/reports/member-account-summary"`, `ActionText = "View Report"` — this satisfies User Story 3 / FR-011 by linking straight to the existing `MemberAccountSummaryReportProvider` (`ReportId => "member-account-summary"`), reachable today at `/reports/{ReportId}` via `ReportsPage.razor`; no new report or route is introduced.

### 6. Tile body layout: `.tile-stats` 3-column row (existing CSS) + a compact line chart (existing CSS/BlazorBootstrap pattern)

The three point-in-time metrics (member count, outstanding attendance $, outstanding annual $) render using the already-defined `.tile-stats` / `.tile-stat-value` / `.tile-stat-label` classes (`src/StageFright.App/wwwroot/app.css:520-547`), the same markup shape `MembersTile.razor` already uses for its Active/Inactive/Total row — no new CSS. Below that, a `BlazorBootstrap.LineChart` (Height 90, smaller than `CashFlowTile`/`AttendanceTrendTile`'s 110 to leave room for the stats row above) plots the calendar-year trend, initialized in `OnAfterRenderAsync` guarded by `_chartInitialized`, exactly mirroring `AttendanceTrendTile.razor.cs`.

**Zero-state (FR-008) is asymmetric between the two halves of the tile**, unlike `CashFlowTile`/`AttendanceTrendTile` (which hide their entire body and show one muted message when there's no data at all): the three stats **always** render, even as 0 / $0.00 / $0.00 — because FR-008 requires the member count and both totals to always be visible, never suppressed. Only the chart section independently degrades to a muted "No outstanding balances this year" note when every month in the trend is zero, matching the existing `_hasData` chart-only degradation pattern used elsewhere, but scoped to just the chart half of this tile.

---

## Phases (each ends green: `dotnet build` + full `dotnet test`)

```
Phase 1: Core aggregates (GL repo + FinanceSummaryService + models)
   └── Phase 2: Dashboard tile (provider + component + DI registration)
```

Phase 2 depends on Phase 1's new service methods.

### Phase 1 — Core aggregates

**New files:**
- `src/StageFright.Core/Modules/Finance/OutstandingFeeSummary.cs` — `{ decimal OutstandingAttendanceFees; decimal OutstandingAnnualFees; }`.
- `src/StageFright.Core/Modules/Finance/MonthlyOutstandingBalance.cs` — `{ int Year; int Month; decimal OutstandingBalance; }`.

**Changed files:**
- `src/StageFright.Core/Contracts/IGLRepository.cs` — add `Task<(decimal Attendance, decimal Annual)> GetOutstandingByFeeTypeAsync(CancellationToken ct = default)` (Core design decision 2).
- `src/StageFright.Data/Repositories/GLRepository.cs` — implement `GetOutstandingByFeeTypeAsync`: filter Member Receivable transactions with non-null `FeeId`, join `Fees` for `FeeType`, group and sum `DebitAmount − CreditAmount` per `FeeType`, project `Attendance`/`Annual` from the grouped results (default `0m` when a type has no rows).
- `src/StageFright.Core/Contracts/IFinanceSummaryService.cs` — add `GetOutstandingFeeSummaryAsync` and `GetOutstandingBalanceTrendAsync` (Core design decision 4).
- `src/StageFright.Core/Modules/Finance/FinanceSummaryService.cs`:
  - `GetOutstandingFeeSummaryAsync` — calls `_glRepository.GetOutstandingByFeeTypeAsync(ct)`, maps the tuple onto `OutstandingFeeSummary`.
  - `GetOutstandingBalanceTrendAsync(DateTime asOf, ct)` — loops `month = 1..asOf.Month`, computing each month's end-of-month instant and calling `_glRepository.GetAccountBalanceAsync(SystemAccounts.MemberReceivableId, endOfMonth, ct)`, building one `MonthlyOutstandingBalance` per month, oldest (January) first (Core design decision 3).

**Tests:**
- `GLRepositoryIntegrationTests`: `GetOutstandingByFeeTypeAsync` — seeded Annual + Attendance fees with partial FIFO payments split correctly by type; an overpayment (null-`FeeId` credit line) does not distort either total; no fee-linked transactions at all returns `(0m, 0m)`.
- `FinanceSummaryServiceTests`: `GetOutstandingFeeSummaryAsync` maps the repo tuple onto the model correctly (mock `IGLRepository`); `GetOutstandingBalanceTrendAsync` returns exactly `asOf.Month` entries starting at January of `asOf.Year`, in order, with each entry's value coming from the corresponding `GetAccountBalanceAsync` call (verify call count/args via mock); `asOf.Month == 1` (January) returns exactly one entry without requiring any "prior month" data (spec Edge Case).

### Phase 2 — Dashboard tile

**New files:**
- `src/StageFright.UI/Modules/Finance/OutstandingBalancesDashboardTileProvider.cs` — `TileId => "finance-outstanding-balances"`, `Title => "Outstanding Balances"`, `ModuleName => "Finance"`, `DisplayOrder => 45`, `NavigateRoute => "/reports/member-account-summary"`, `ActionText => "View Report"`, `TileComponentType => typeof(OutstandingBalancesTile)`, `GetTileDataAsync` returns `new TileData { NavigateRoute = NavigateRoute }` (Core design decisions 5).
- `src/StageFright.UI/Modules/Finance/OutstandingBalancesTile.razor` + `.razor.cs` — injects `IMemberBalanceService` and `IFinanceSummaryService`; `OnInitializedAsync` loads `GetAllMemberBalancesAsync()` (for `.Count`), `GetOutstandingFeeSummaryAsync()`, and `GetOutstandingBalanceTrendAsync(DateTime.Today)` in parallel (`Task.WhenAll`, matching this project's general async-loading style); renders the `.tile-stats` 3-column row unconditionally, plus a `LineChart` (Height 90) when the trend has any non-zero point, else a muted "No outstanding balances this year" note (Core design decision 6). Catches exceptions into the existing `_error` pattern (`_error = true` on any failure, matching `CashFlowTile`/`FinanceTile`/`AttendanceTrendTile`).

**Changed files:**
- `src/StageFright.App/MauiProgram.cs` — add `services.AddScoped<IDashboardTileProvider, OutstandingBalancesDashboardTileProvider>();` alongside the other Finance tile registrations (after `CashFlowDashboardTileProvider`, line ~208).

**Tests:**
- `OutstandingBalancesDashboardTileProviderTests` (mirrors `CashFlowDashboardTileProviderTests`): `TileId`/`Title`/`ModuleName`/`DisplayOrder`/`NavigateRoute`/`ActionText`/`TileComponentType` assertions; `GetTileDataAsync` returns `TileData` carrying `NavigateRoute`.
- `OutstandingBalancesTileTests` (bUnit, mirrors `CashFlowTileTests`/pattern used for `MembersTile`): renders member count / attendance total / annual total from mocked services; zero-outstanding case still renders `0` / `$0.00` / `$0.00` (never an empty/error state — FR-008); a member owing on both fee types is counted once (mock `GetAllMemberBalancesAsync` returning one `MemberBalance` for that member, assert count reflects one, not two — FR-009); trend with all-zero months shows the "No outstanding balances this year" note instead of a chart; service exception surfaces the existing `_error`/"Unable to load" state.
- `V8_DashboardPluginTests` (or equivalent dashboard integration test) — no exact-tile-count assertions exist today (`Assert.True(coreTiles.Count >= 3, ...)`), so no update needed there; spot-check the new tile appears in `DashboardService.GetTilesAsync()` output alongside the existing six.

---

## Explicitly NOT changing

- `IMemberBalanceService`/`MemberBalanceService` — reused as-is for the member count (Core design decision 1).
- `MemberAccountSummaryReportProvider` or any report — the tile links to the existing report; no new report is introduced.
- `Fee`, `Payment`, `Transaction` entities, `PaymentService`'s FIFO allocation, or any GL posting logic — this feature is read-only aggregation over existing data.
- `GetMemberBalanceAsync`/`GetTotalOutstandingAsync`/`GetAgingBucketsAsync` — unchanged; the new `GetOutstandingByFeeTypeAsync` is additive.
- Dashboard grid layout/CSS (`Dashboard.razor`, `.sf-dash-tile`, `.tile-stats`) — the new tile fits the existing uniform card grid with zero layout changes.

## Risks / watch-outs

- `GetOutstandingByFeeTypeAsync`'s EF Core join (`Transactions` → `Fees` on `FeeId`) must be verified as translating to a single SQL query against SQLite, not client-evaluated — confirm via the integration test that it executes without a client-eval warning/exception.
- `GetOutstandingBalanceTrendAsync` makes up to 12 sequential `GetAccountBalanceAsync` calls; acceptable for a dashboard tile (matches existing per-period-loop and per-member-loop precedent elsewhere), but if this pattern is ever reused somewhere hotter than a dashboard, it should be revisited as a single grouped query.
- The zero-state split (stats always show, chart alone degrades) is a deliberate deviation from `CashFlowTile`/`AttendanceTrendTile`'s whole-body zero-state — must not accidentally copy-paste their "hide everything" `_hasData` gating onto the stats row.
- `DisplayOrder = 45` sits between two existing tiles (40, 50); confirm no other module has already claimed 45 (checked: Members=10/20/30 range unconfirmed exactly, but Events=30, Finance=40, CashFlow=50, AttendanceTrend=60 are the only core `DisplayOrder`s below 100 today — 45 is free).

## Verification

1. `dotnet build` and full `dotnet test` (no `--no-build`) — all 5 test projects green, after both Phase 1 and Phase 2.
2. Manual E2E (via `dotnet run --project src/StageFright.App/`): seed a mix of paid/unpaid Annual and Attendance fees across several months of the current year (via the Debug seeder or manual entry), open the dashboard, confirm the new tile shows the correct member count and per-type totals, the chart renders a plausible trend, and clicking the tile navigates to `/reports/member-account-summary`.
3. Zero-data check: on a freshly set-up organisation with no fees at all, confirm the tile shows `0` members / `$0.00` / `$0.00` and the "No outstanding balances this year" chart note, without an error state.
