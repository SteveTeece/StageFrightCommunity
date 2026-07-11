# Tasks: Outstanding Balances Dashboard Tile

**Input**: Design documents from `/specs/004-outstanding-balances-tile/` (plan.md, spec.md)

**Tests**: Included — CLAUDE.md mandates exhaustive `Should_X_When_Y` coverage before merge; each phase ends green (`dotnet build` + full `dotnet test`).

**Organization**: Tasks grouped by user story. Foundational (tile provider shell, tile component shell, DI registration) blocks all three stories. User Story 1 (stats row), User Story 2 (calendar-year chart), and User Story 3 (drill-down navigation) are independent of each other once Foundational is done — each adds a self-contained slice of the same tile.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

**Purpose**: Baseline verification — no scaffolding needed (existing solution).

- [X] T001 Verify baseline: `dotnet build` and full `dotnet test` green on branch `004-outstanding-balances-tile` before any change (1069 tests)

---

## Phase 2: Foundational

**Purpose**: The new Core-layer models, the tile provider, and the tile component shell — required by all three user stories. No user-story work can begin until this phase is complete.

- [X] T002 [P] Create `OutstandingFeeSummary` model (`decimal OutstandingAttendanceFees`, `decimal OutstandingAnnualFees`) in src/StageFright.Core/Modules/Finance/OutstandingFeeSummary.cs
- [X] T003 [P] Create `MonthlyOutstandingBalance` model (`int Year`, `int Month`, `decimal OutstandingBalance`) in src/StageFright.Core/Modules/Finance/MonthlyOutstandingBalance.cs
- [X] T004 Create `OutstandingBalancesDashboardTileProvider` (`TileId => "finance-outstanding-balances"`, `Title => "Outstanding Balances"`, `ModuleName => "Finance"`, `DisplayOrder => 45`, `TileComponentType => typeof(OutstandingBalancesTile)`; `NavigateRoute`/`ActionText` left at their default `null` for now — added in User Story 3) in src/StageFright.UI/Modules/Finance/OutstandingBalancesDashboardTileProvider.cs
- [X] T005 Create skeleton `OutstandingBalancesTile.razor` + `.razor.cs` (loading/"Unable to load" error states only, no stats or chart content yet) in src/StageFright.UI/Modules/Finance/ (depends on T004)
- [X] T006 Register `OutstandingBalancesDashboardTileProvider` as `IDashboardTileProvider` in src/StageFright.App/MauiProgram.cs, alongside the other Finance tile registrations (after `CashFlowDashboardTileProvider`) (depends on T004)

### Tests

- [X] T007 [P] `OutstandingBalancesDashboardTileProviderTests`: `TileId`/`Title`/`ModuleName`/`DisplayOrder`/`TileComponentType` assertions — in tests/StageFright.UI.Tests/Modules/Finance/
- [X] T008 Verify checkpoint: `dotnet build` + full `dotnet test` green (1071 tests, up from 1069 baseline)

**Checkpoint**: Foundation ready — User Story 1, User Story 2, and User Story 3 can now proceed independently (and in parallel).

---

## Phase 3: User Story 1 — Committee member checks who still owes fees (Priority: P1) 🎯 MVP

**Goal**: The tile shows a stats row: count of members with any outstanding balance, total outstanding attendance fees, total outstanding annual fees — always rendering, including a 0 / $0.00 / $0.00 zero-state.

**Independent Test**: Seed several members with outstanding annual and/or attendance fees, including one member owing on both fee types; load the dashboard; verify the member count, attendance total, and annual total are correct and the member owing both types is counted once. Then clear all outstanding fees and verify the tile shows 0 / $0.00 / $0.00 rather than hiding or erroring.

### Implementation for User Story 1

- [X] T009 [US1] Add `Task<(decimal Attendance, decimal Annual)> GetOutstandingByFeeTypeAsync(CancellationToken ct = default)` to `IGLRepository` in src/StageFright.Core/Contracts/IGLRepository.cs
- [X] T010 [US1] Implement `GetOutstandingByFeeTypeAsync` in src/StageFright.Data/Repositories/GLRepository.cs: filter `Transactions` to `AccountId == SystemAccounts.MemberReceivableId && FeeId != null`, join `Fees` on `FeeId` for `FeeType`, group and sum `DebitAmount − CreditAmount` per `FeeType`, defaulting absent types to `0m` (depends on T009)
- [X] T011 [US1] Add `Task<OutstandingFeeSummary> GetOutstandingFeeSummaryAsync(CancellationToken ct = default)` to `IFinanceSummaryService` in src/StageFright.Core/Contracts/IFinanceSummaryService.cs (depends on T002)
- [X] T012 [US1] Implement `GetOutstandingFeeSummaryAsync` in src/StageFright.Core/Modules/Finance/FinanceSummaryService.cs — calls `GetOutstandingByFeeTypeAsync` and maps the tuple onto `OutstandingFeeSummary` (depends on T010, T011)
- [X] T013 [US1] Update src/StageFright.UI/Modules/Finance/OutstandingBalancesTile.razor.cs to inject `IMemberBalanceService` and `IFinanceSummaryService`; `OnInitializedAsync` loads `GetAllMemberBalancesAsync().Count` and `GetOutstandingFeeSummaryAsync()`; exposes `_memberCount`/`_attendanceOutstanding`/`_annualOutstanding`; catches exceptions into the existing `_error` pattern (depends on T005, T012)
- [X] T014 [US1] Update src/StageFright.UI/Modules/Finance/OutstandingBalancesTile.razor to render the `.tile-stats` 3-column row (Members owing / Outstanding Attendance / Outstanding Annual) unconditionally once loaded, including the zero-state (depends on T013)

### Tests for User Story 1

- [X] T015 [P] [US1] `GLRepositoryIntegrationTests`: `GetOutstandingByFeeTypeAsync` — seeded Annual + Attendance fees with partial FIFO payments split correctly by type; an overpayment (null-`FeeId` credit line) does not distort either total; no fee-linked transactions returns `(0m, 0m)` — in tests/StageFright.Data.Tests/Repositories/
- [X] T016 [P] [US1] `FinanceSummaryServiceTests`: `GetOutstandingFeeSummaryAsync` maps a mocked `IGLRepository` tuple onto `OutstandingFeeSummary` correctly — in tests/StageFright.Core.Tests/Modules/Finance/
- [X] T017 [US1] `OutstandingBalancesTileTests` (bUnit): renders member count / attendance total / annual total from mocked services; zero-outstanding case renders `0` / `$0.00` / `$0.00` rather than an empty/error state (FR-008); a member owing on both fee types is counted once (FR-009); a service exception surfaces the existing `_error`/"Unable to load" state — in tests/StageFright.UI.Tests/Modules/Finance/
- [X] T018 [US1] Verify checkpoint: `dotnet build` + full `dotnet test` green (1082 tests, up from 1071 Foundational baseline)

**Checkpoint**: User Story 1 fully functional and testable independently — a viable MVP tile.

---

## Phase 4: User Story 2 — Committee member spots the trend across the year (Priority: P2)

**Goal**: The tile adds a chart plotting outstanding balance for each month from January through the current month of the current calendar year; degrades to a muted note when every month is zero.

**Independent Test**: Seed fee/payment activity across multiple months of the current year (e.g. balances present in January, paid off by June); load the dashboard; verify the chart shows the expected month-by-month trend and plots only January through the current month, never future months.

### Implementation for User Story 2

- [X] T019 [US2] Add `Task<IReadOnlyList<MonthlyOutstandingBalance>> GetOutstandingBalanceTrendAsync(DateTime asOf, CancellationToken ct = default)` to `IFinanceSummaryService` in src/StageFright.Core/Contracts/IFinanceSummaryService.cs (depends on T003)
- [X] T020 [US2] Implement `GetOutstandingBalanceTrendAsync` in src/StageFright.Core/Modules/Finance/FinanceSummaryService.cs — loops `month = 1..asOf.Month`, calling the existing `IGLRepository.GetAccountBalanceAsync(SystemAccounts.MemberReceivableId, endOfMonth, ct)` per month, building one `MonthlyOutstandingBalance` per month oldest-first (depends on T019)
- [X] T021 [US2] Update src/StageFright.UI/Modules/Finance/OutstandingBalancesTile.razor.cs: load `GetOutstandingBalanceTrendAsync(DateTime.Today)` alongside the existing loads (`Task.WhenAll`); build `ChartData`/`LineChartOptions`; add `_chartInitialized`/`_hasChartData` state; initialize the chart in `OnAfterRenderAsync` (mirrors `AttendanceTrendTile.razor.cs`) (depends on T013, T020)
- [X] T022 [US2] Update src/StageFright.UI/Modules/Finance/OutstandingBalancesTile.razor: render a `BlazorBootstrap.LineChart` (Height 90) below the stats row when the trend has any non-zero point, else a muted "No outstanding balances this year" note (depends on T014, T021)

### Tests for User Story 2

- [X] T023 [P] [US2] `FinanceSummaryServiceTests`: `GetOutstandingBalanceTrendAsync` returns exactly `asOf.Month` entries starting at January of `asOf.Year`, in order, with values from the corresponding mocked `GetAccountBalanceAsync` calls; `asOf.Month == 1` (January) returns exactly one entry without requiring prior-month data — in tests/StageFright.Core.Tests/Modules/Finance/
- [X] T024 [US2] `OutstandingBalancesTileTests`: chart renders when the trend has non-zero data; an all-zero trend shows the muted note instead of a chart; only January through the current month are plotted (no future months) — in tests/StageFright.UI.Tests/Modules/Finance/
- [X] T025 [US2] Verify checkpoint: `dotnet build` + full `dotnet test` green (1087 tests, up from 1082 US1 baseline; one transient flaky failure in an unrelated pre-existing Events test — GUID-random substring collision — confirmed passing in isolation and on re-run)

**Checkpoint**: User Stories 1 and 2 both independently functional.

---

## Phase 5: User Story 3 — Committee member drills from the tile into full detail (Priority: P3)

**Goal**: Selecting the tile navigates to the existing Member Account Summary report, where outstanding balances can be examined per member.

**Independent Test**: Click/activate the tile; verify navigation to `/reports/member-account-summary`.

### Implementation for User Story 3

- [X] T026 [US3] Add `NavigateRoute => "/reports/member-account-summary"` and `ActionText => "View Report"` to `OutstandingBalancesDashboardTileProvider`; update `GetTileDataAsync` to return `new TileData { NavigateRoute = NavigateRoute }` — in src/StageFright.UI/Modules/Finance/OutstandingBalancesDashboardTileProvider.cs (depends on T004)

### Tests for User Story 3

- [X] T027 [US3] Update `OutstandingBalancesDashboardTileProviderTests`: assert `NavigateRoute`/`ActionText`, and `GetTileDataAsync` returns `TileData` carrying `NavigateRoute` — in tests/StageFright.UI.Tests/Modules/Finance/ (depends on T007, T026)
- [X] T028 [US3] Verify checkpoint: `dotnet build` + full `dotnet test` green (1087 tests)

**Checkpoint**: All three user stories independently functional — the tile is feature-complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T029 [P] Manual E2E per plan.md Verification: launched StageFright.App with WebView2 remote debugging against the existing dev database and inspected the DOM via CDP `Runtime.evaluate` — tile shows "2 Members Owing / $0.00 Attendance / $20.00 Annual", chart canvas renders, and clicking the tile navigates to `/reports/member-account-summary` (confirmed via page URL change)
- [X] T030 [P] Zero-data check: covered by automated bUnit coverage (`Should_ShowZeroValues_When_NoOutstandingBalancesExist`, `Should_ShowStatsAndNote_When_NoOutstandingBalancesAtAll` in OutstandingBalancesTileTests.cs, both passing) — a second live-app zero-data run was skipped after a screenshot during T029 unexpectedly captured unrelated content from another window; the automated coverage already exercises this path deterministically
- [X] T031 Full regression pass: `dotnet build` + full `dotnet test` (no `--no-build`) across all 5 test projects — 1087/1087 passing, up from the 1069 Phase 1 baseline (+18 new tests)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — can start immediately.
- **Foundational (Phase 2)**: depends on Setup — BLOCKS all three user stories.
- **User Story 1 (Phase 3)**, **User Story 2 (Phase 4)**, **User Story 3 (Phase 5)**: all depend only on Foundational; independent of each other and can proceed in parallel (though US2/US3 build on the stats-row plumbing US1 adds to the same two files, so sequential US1 → US2 → US3 avoids repeated merge conflicts in `OutstandingBalancesTile.razor`/`.razor.cs`).
- **Polish (Phase 6)**: depends on all three user stories being complete.

### Within Each Phase

- T002/T003 (models) have no dependency on each other; T004 depends on neither.
- T005 depends on T004 (component references the provider's `TileComponentType`); T006 depends on T004.
- Within US1: T009 before T010; T002 + T011 before T012; T010 + T012 before T013; T013 before T014.
- Within US2: T003 before T019; T019 before T020; T013 + T020 before T021; T014 + T021 before T022.
- Within US3: T004 before T026; T007 + T026 before T027.

### Parallel Opportunities

- T002, T003 (Foundational models) can run in parallel.
- T007 (Foundational test) can run in parallel with T005/T006 once T004 lands.
- T015, T016 [P] can run in parallel with each other within US1.
- T023 [P] can run in parallel with US1's remaining tests within US2.
- T029, T030 [P] (Polish manual checks) can run in parallel.

---

## Parallel Example: Foundational Phase

```bash
Task: "Create OutstandingFeeSummary model in src/StageFright.Core/Modules/Finance/OutstandingFeeSummary.cs"
Task: "Create MonthlyOutstandingBalance model in src/StageFright.Core/Modules/Finance/MonthlyOutstandingBalance.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all three stories)
3. Complete Phase 3: User Story 1 (stats row)
4. **STOP and VALIDATE**: seed mixed outstanding fees, confirm member count and per-type totals are correct, including the zero-state
5. Deploy/demo if ready — the tile already delivers its core "how much is owed, by how many members" value even before the chart or drill-down land

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. User Story 1 → validate independently → demo (MVP)
3. User Story 2 → validate independently → demo
4. User Story 3 → validate independently → demo
5. Polish

---

## Notes

- `[P]` tasks = different files, no dependencies.
- `[Story]` label maps a task to its user story for traceability.
- CLAUDE.md requires exhaustive `Should_X_When_Y` coverage regardless of TDD ordering; write tests alongside or ahead of implementation as convenient.
- Commit after each task or logical group.
- Stop at any story's checkpoint to validate independently.
