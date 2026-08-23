# Tasks: Print Chart of Accounts

**Input**: Design documents from `/specs/020-print-chart-of-accounts/` (plan.md, spec.md, research.md, data-model.md, contracts/chart-of-accounts-report.md)

**Tests**: Included throughout — CLAUDE.md's "Exhaustive code-path test coverage" rule is project-wide and non-negotiable. Each phase's `### Tests` block is written first, deliberately failing to compile/pass until its `### Implementation` block lands.

**Organization**: Grouped by user story per spec.md's priority order (US1 P1 → US2 P2 → US3 P3). All three stories converge on the same two files — `ChartOfAccountsReportProvider.cs` and `ChartOfAccountsPage.razor`/`.razor.cs` — each edited incrementally across phases (never in the same wave). Per `RehearsalListTests`' documented precedent (the print-roll button), the happy-path render→temp-file→launch (`File.WriteAllBytes` + `Process.Start`) has no seam to intercept in a bUnit test, so it is never click-tested directly; button rendering and every guard/error path are.

## Format: `[ID] [P?] [Story] Description · file`

- **[P]**: Independent of the other tasks in its wave — different file, no incomplete dependency — buildable in any order (or in parallel).
- **[US#]**: Maps to spec.md's US1–US3.
- A **wave** groups tasks that can be built in any order; **⟶** join lines mark a hard wait for the previous wave.

---

## Phase 1: Setup

- [x] **T001** Confirm baseline: `dotnet build` and the full `dotnet test` suite (no `--no-build`) are green on branch `020-print-chart-of-accounts` before any change, per CLAUDE.md's Build & Test Verification rule.

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: No user story can begin until this phase is complete — the screen's Print button (US1) and the Reports-menu surface (US3) both require `ChartOfAccountsReportProvider` to exist and be registered in `IReportProviderRegistry` before either can resolve it; US2 edits the same provider file this phase creates.

### Tests

**Wave 1 (single task):**

- [x] **T002** New `ChartOfAccountsReportProviderTests`: `ReportId == "chart-of-accounts"`; `ReportName == "Chart of Accounts"`; `ModuleName == "Finance"`; `DisplayOrder == 15`; `GenerateAsync` returns exactly five `Sections` headed `"Assets"`, `"Liabilities"`, `"Equity"`, `"Income"`, `"Expenses"` in that fixed order; a type with zero matching accounts still appears with zero rows (spec Edge Case 1); rows within a section are ordered by `AccountNumber` ascending; a system account's Name cell reads `"{Name} (System)"`, a bank account reads `"{Name} (Bank)"`, and an account that is both reads `"{Name} (System, Bank)"` — a plain account has no suffix; `Columns` is exactly `["No.", "Name"]` and every row has `Cells.Count == 2`; `GrandTotal` is always `null`; `SummaryColumns` is always `null`; the provider never calls `IAccountBalanceService.GetArchivedAccountBalancesAsync` (proves FR-011 by construction, since `GetActiveAccountBalancesAsync` is the only source read). Model the fixture on `TrialBalanceReportProviderTests` (NSubstitute `IAccountBalanceService`, one `[Fact]` per assertion) · `tests/StageFright.Reports.Tests/ChartOfAccountsReportProviderTests.cs` (NEW)

### Implementation

**Wave 1 (single task):**

- [x] **T003** New `ChartOfAccountsReportProvider : IReportProvider`: constructor takes `IAccountBalanceService`; `ReportId => "chart-of-accounts"`, `ReportName => "Chart of Accounts"`, `ModuleName => "Finance"`, `DisplayOrder => 15`; `Filters => Array.Empty<ReportFilterDefinition>()` for now (US2 replaces this with the `includeBalances` definition); `GenerateAsync` calls `_balanceService.GetActiveAccountBalancesAsync(ct)` once, groups the result by `Type` into the five fixed `ReportSection`s (each `Rows` ordered by `AccountNumber` — already the order the service returns, per its own contract), a private `FormatName(AccountBalance)` helper building the `" (System)"`/`" (Bank)"`/`" (System, Bank)"` suffix, `Columns = [new ReportColumn { Header = "No." }, new ReportColumn { Header = "Name" }]`, each row's `Cells = [a.AccountNumber, FormatName(a)]`, `GrandTotal = null`, `SummaryColumns = null`, `Title = "Chart of Accounts"`. Satisfies FR-003/004/005/006/011/012 · `src/StageFright.Reports/Providers/ChartOfAccountsReportProvider.cs` (NEW). Depends on T002.

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 (single task):**

- [x] **T004** Register the provider: `services.AddScoped<IReportProvider, ChartOfAccountsReportProvider>();` inserted immediately after the `IncomeStatementReportProvider` line and before `TrialBalanceReportProvider` (`DisplayOrder` 15 sits between 10 and 20, per research.md's Decision) · `src/StageFright.App/MauiProgram.cs`. Depends on T003.

**Checkpoint**: `ChartOfAccountsReportProvider` exists, is registered, and produces the correct five-section, correctly-ordered, archived-excluding, no-grand-total structural report with the include-balances option not yet wired up. Every story can now build on it.

---

## Phase 3: User Story 1 - Print the chart of accounts structure (Priority: P1) 🎯 MVP

**Goal**: A committee member on the Chart of Accounts screen clicks "Print Chart of Accounts" and a document opens listing every active account, grouped under its account type, in account-number order, with no balance figures.

**Independent Test**: From the Chart of Accounts screen, click "Print Chart of Accounts" with the balance option left off. A document opens listing every active account, grouped under its account type, in account-number order.

### Tests

**Wave 1 (single task):**

- [x] **T005** [US1] Extend `ChartOfAccountsPageTests`: add `IReportProviderRegistry` and `IPdfReportRenderer` NSubstitutes to the fixture (mirroring `RehearsalListTests`' `IAttendanceRollPdfRenderer`/`ISettingsService` wiring) plus an `ISettingsService` substitute returning an organization name. New cases — a "Print Chart of Accounts" button renders (verbatim label, FR-001); clicking it when `_reportProviderRegistry.GetProvider("chart-of-accounts")` returns `null` shows a friendly error alert and never calls `PdfRenderer.Render`; clicking it when the resolved provider's `GenerateAsync` throws shows a friendly error alert and never calls `PdfRenderer.Render` (mirrors `RehearsalListTests.ClickPrintRoll_ServiceThrows_ShowsErrorAlert_AndDoesNotRenderPdf`) · `tests/StageFright.UI.Tests/Pages/Finance/ChartOfAccountsPageTests.cs`

### Implementation

**Wave 1 (single task):**

- [x] **T006** [US1] `ChartOfAccountsPage.razor`/`.razor.cs`: add a "Print Chart of Accounts" button (verbatim label, FR-001) above or beside the type filter; inject `IReportProviderRegistry`, `IPdfReportRenderer`, `ISettingsService`, `ILogger<ChartOfAccountsPage>`; `PrintAsync` handler resolves `Registry.GetProvider("chart-of-accounts")`, shows a friendly error and returns if `null`, otherwise builds an empty `ReportFilterValues` (US2 adds the `includeBalances` entry), calls `GenerateAsync`, renders via `PdfRenderer.Render(report, orgName)` from `SettingsService.GetAsync()`, writes to a temp `.pdf` file, and opens it with `Process.Start(..., UseShellExecute = true)` — the exact sequence `ReportViewer.PrintReport()`/`RehearsalList.PrintRoll()` already use; wraps the whole handler in try/catch, logging and setting a friendly `_errorMessage` on failure (FR-013, matches `ReportViewer.PrintReport()`'s catch pattern) · `src/StageFright.UI/Pages/Finance/ChartOfAccountsPage.razor`, `ChartOfAccountsPage.razor.cs`. Depends on T003, T004, T005.

**Checkpoint**: US1 is independently functional and testable — clicking "Print Chart of Accounts" with the balance option off opens a document listing every active account, correctly grouped and ordered, with archived accounts and balance figures absent.

---

## Phase 4: User Story 2 - Include current account balances (Priority: P2)

**Goal**: Turning on an "include current account balances" option before printing adds a Balance column to every row, sourced from the same figures the screen already shows, with per-account calculation failures shown as an error indicator instead of blocking the rest of the report.

**Independent Test**: Turn on the "include current account balances" option on the Chart of Accounts screen, then print. The resulting document shows each account's current balance alongside its number and name.

### Tests

**Wave 1 — independent (different files):**

- [x] **T007** [P] [US2] Extend `ChartOfAccountsReportProviderTests`: `Filters` returns exactly one `ReportFilterDefinition` — `Key == "includeBalances"`, `Type == ReportFilterType.Boolean`, `Label == "Include Current Balances"`, `DefaultValue == "false"`; `GenerateAsync` with `includeBalances` unset or `"false"` still returns the two-column, two-cell-per-row shape from T002 (unchanged); `GenerateAsync` with `includeBalances == "true"` returns `Columns = ["No.", "Name", "Balance"]`, `Cells.Count == 3` per row, the Balance cell formatted `"F2"` (matching `TrialBalanceReportProvider`'s convention) for a row with `HasError == false`, and the literal string `"Error"` for a row with `HasError == true` (every other row still prints normally) · `tests/StageFright.Reports.Tests/ChartOfAccountsReportProviderTests.cs`
- [x] **T008** [P] [US2] Extend `ChartOfAccountsPageTests`: an "Include Current Balances" `RadzenSwitch` renders, defaulting to unchecked (`aria-checked="false"`) per CLAUDE.md's toggle-control testing convention (`cut.Find("[role=switch]")`, not `.Change(bool)`); after clicking the switch on and then clicking Print, `Registry`'s resolved provider substitute receives `GenerateAsync(Arg.Is<ReportFilterValues>(f => f.Get("includeBalances") == "true"), Arg.Any<CancellationToken>())` — assert this by making the substituted `PdfRenderer.Render(...)` throw so the flow never reaches `File.WriteAllBytes`/`Process.Start` (same seam-avoidance technique as T005, just deferred one step later in the pipeline); the default (switch left off) case asserts `f.Get("includeBalances") == "false"` the same way · `tests/StageFright.UI.Tests/Pages/Finance/ChartOfAccountsPageTests.cs`

### Implementation

**Wave 1 (single task):**

- [x] **T009** [US2] Extend `ChartOfAccountsReportProvider`: `Filters` now returns the single `includeBalances` `ReportFilterDefinition` (`Boolean`, Label `"Include Current Balances"`, `DefaultValue "false"`) per contracts/chart-of-accounts-report.md; `GenerateAsync` reads `filters.Get("includeBalances") == "true"` — when off, `Columns`/`Cells` are unchanged from T003 (two entries, Balance column structurally absent, not blank, per research.md's Decision); when on, `Columns` gains a `"Balance"` header and each row's `Cells` gains a third entry: `a.Balance?.ToString("F2")` when `!a.HasError`, else the fixed string `"Error"` (FR-007/008/009/010 — FR-008 holds by construction since `Balance` comes straight from the same `IAccountBalanceService` call the screen uses) · `src/StageFright.Reports/Providers/ChartOfAccountsReportProvider.cs`. Depends on T007. Same file T003 (Foundational) already created — sequential edit, not parallel with it.

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 (single task):**

- [x] **T010** [US2] `ChartOfAccountsPage.razor`/`.razor.cs`: add an off-by-default `RadzenSwitch` labeled "Include Current Balances" (FR-002, matching the Members List "show inactive" reference usage) bound to a new `_includeBalances` field with a `Change` callback (not `@bind:after`, per CLAUDE.md's toggle-control rule); `PrintAsync` sets `filters.Set("includeBalances", _includeBalances ? "true" : "false")` before calling `GenerateAsync` · `src/StageFright.UI/Pages/Finance/ChartOfAccountsPage.razor`, `ChartOfAccountsPage.razor.cs`. Depends on T008, T009. Same files T006 (US1) already touched — sequential edit, not parallel with it.

**Checkpoint**: US2 is independently functional and testable — turning the option on prints a Balance column matching the screen's figures with per-account error isolation; turning it off prints no balance column at all; US1's base behavior is unchanged.

---

## Phase 5: User Story 3 - Generate the report from the Reports menu (Priority: P3)

**Goal**: "Chart of Accounts" appears in the central Reports menu's Finance section, offering the same grouping/ordering and include-balances option as the screen button, and exports to a spreadsheet file like every other report.

**Independent Test**: Open the Reports menu, select "Chart of Accounts", generate it, and export it to a spreadsheet file.

### Tests

**Wave 1 (single task):**

- [x] **T011** [US3] Extend `ChartOfAccountsReportProviderTests` with a CSV round-trip case: `CsvReportExporter.Export(await _sut.GenerateAsync(...))` (the same generic exporter `PdfAndCsvRendererTests` already exercises against synthetic `ReportData`) produces one header line matching `Columns` and one data line per row for both `includeBalances` states, proving the exported spreadsheet content matches what `GenerateAsync` produced (SC-006) with no reliance on any new production code · `tests/StageFright.Reports.Tests/ChartOfAccountsReportProviderTests.cs`

### Implementation

No new production code — `ReportMenuItemProvider` and `ReportsPage`/`ReportViewer` already build the Finance menu section, the on-screen `includeBalances` filter control, and CSV export generically from every `IReportProvider` registered in `IReportProviderRegistry` (T004 already registered this one). FR-014 is satisfied entirely by T003's `ModuleName => "Finance"` (already asserted in T002) plus T004's DI registration.

**Checkpoint**: US3 is independently functional and testable — "Chart of Accounts" is discoverable in the Reports menu's Finance section, generates the same grouped/ordered report with the same include-balances option, and exports to CSV matching what was shown on screen.

---

## Phase 6: Polish

**Wave 1 (single task):**

- [x] **T012** Run `dotnet build` and the full `dotnet test` suite (all five test projects, no `--no-build`) from the repo root and confirm everything is green, per CLAUDE.md's Build & Test Verification rule.

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 (single task):**

- [x] **T013** Walk every Acceptance Scenario in spec.md (US1's 4, US2's 4, US3's 3) plus the Edge Cases against a running `dotnet run --project src/StageFright.App/` instance: only system accounts existing still prints every type heading with no rows beneath an empty one; an unusually long account name wraps/fits via the existing renderer; a system/bank account's row visibly shows its plain-text indicator; toggling the balance option between separate prints (and between the screen button and the Reports menu) reflects only that print's own state; no combined grand-total figure ever appears.

---

## Dependencies & Execution Order

- **Setup (Phase 1, T001)** → **Foundational (Phase 2, T002–T004)**: T002 (tests) is written first and fails until T003 (the provider) lands; T004 (DI registration) needs T003's class to exist.
- **Foundational → US1 (Phase 3, T005–T006)**: T005 (page tests) is written first; T006 (Print button + handler) depends on T003/T004 existing and makes T005 pass.
- **Foundational → US2 (Phase 4, T007–T010)**: Tests wave (T007, T008) is 2 independent files, written first. Implementation Wave 1 (T009) extends the provider (depends on T007); Wave 2 (T010) extends the page (depends on T008, T009) — sequenced after T009 since the page's filter-passing test needs the provider's real `Filters`/branching to mean anything.
- **Foundational → US3 (Phase 5, T011)**: Test-only phase; depends only on T003/T004 (Foundational) and the already-generic `CsvReportExporter`/`ReportMenuItemProvider` infrastructure — independent of US2's balance-column work, sequenced last per spec.md's P3 priority.
- **US1 + US2 + US3 → Polish (Phase 6, T012–T013)**: T012 (full build/test) needs every story's tests written first to include them in the run; T013 (manual walkthrough) needs T012 green before it's a meaningful check.

---

## Requirement Coverage

| Requirement | Tasks |
|---|---|
| FR-001 | T005, T006 |
| FR-002 | T008, T010 |
| FR-003 | T002, T003 |
| FR-004 | T002, T003 |
| FR-005 | T002, T003 |
| FR-006 | T002, T003 |
| FR-007 | T007, T009 |
| FR-008 | T007, T009 |
| FR-009 | T007, T009 |
| FR-010 | T007, T009 |
| FR-011 | T002, T003 |
| FR-012 | T002, T003 |
| FR-013 | T005, T006 |
| FR-014 | T002, T003, T004, T011 |
