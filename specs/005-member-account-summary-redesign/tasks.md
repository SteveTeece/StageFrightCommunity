---

description: "Task list for Member Account Summary Report Redesign"
---

# Tasks: Member Account Summary Report Redesign

**Input**: Design documents from `/specs/005-member-account-summary-redesign/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/report-master-detail-contract.md, quickstart.md

**Tests**: Included — CLAUDE.md mandates exhaustive code-path test coverage before merge, and plan.md's Constitution Check (§11) tracks tests as required task work for this feature.

**Organization**: Tasks are grouped by user story (spec.md priorities P1–P3) to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)

## Path Conventions

Single existing solution (`StageFrightCommunity.slnx`), no new projects. All paths are relative to the repo root.

---

## Phase 1: Setup

**Purpose**: Confirm a clean baseline before making changes

- [ ] T001 Run `dotnet build` and `dotnet test` (full suite) from the repo root and confirm everything is green before starting, per CLAUDE.md's build/test verification requirement

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared model changes that every user story's rendering/data path depends on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T002 [P] Add `SummaryColumns` property (`IReadOnlyList<ReportColumn>?`, nullable/default empty) to `src/StageFright.Reports/Models/ReportData.cs`, per data-model.md
- [ ] T003 [P] Add `SummaryRow` property (`ReportRow?`, nullable) to `src/StageFright.Reports/Models/ReportSection.cs`, per data-model.md

**Checkpoint**: Model supports the optional master-detail contract; all five other report providers are unaffected since they never set these new properties (FR-010).

---

## Phase 3: User Story 1 - Committee member scans aging at a glance (Priority: P1) 🎯 MVP

**Goal**: On first load, each member appears as a single collapsed row showing name and current/30/60/90+ day aging totals, with no transaction rows visible.

**Independent Test**: Seed several members with a mix of paid and unpaid fees at different ages, load the report, and confirm it initially shows one row per member with name and aging bucket totals only — no transaction-level detail visible until a member is expanded.

### Tests for User Story 1

- [ ] T004 [P] [US1] Add test in `tests/StageFright.Reports.Tests/MemberAccountSummaryReportProviderTests.cs` asserting `ReportData.SummaryColumns` has 6 headers (Member/Current/30 Days/60 Days/90+ Days/Balance) and every returned `ReportSection.SummaryRow!.Cells.Count` equals `SummaryColumns.Count`
- [ ] T005 [P] [US1] Add test in `tests/StageFright.Reports.Tests/MemberAccountSummaryReportProviderTests.cs` asserting a member with no outstanding fees still gets a `SummaryRow` with all four aging cells formatted as zero (e.g. `"Current: 0.00"`) rather than being omitted
- [ ] T006 [P] [US1] Add bUnit test in `tests/StageFright.UI.Tests/Shared/ReportViewerTests.cs` asserting that when `ReportData.SummaryColumns` is populated, `ReportViewer` renders one row per section via `RadzenDataGrid` and no `Rows`/transaction-detail content is visible in the initial markup

### Implementation for User Story 1

- [ ] T007 [US1] In `src/StageFright.Reports/Providers/MemberAccountSummaryReportProvider.cs`, populate `ReportData.SummaryColumns` (`Member`, `Current`, `30 Days`, `60 Days`, `90+ Days`, `Balance` — last one right-aligned currency) and set each `ReportSection.SummaryRow` to `[name-with-archived-suffix, "Current: {aging0}", "30 days: {aging30}", "60 days: {aging60}", "90+ days: {aging90Plus}", FormatCurrency(closingBalance)]`, reusing the existing aging computation (depends on T002, T003)
- [ ] T008 [US1] In `src/StageFright.UI/Shared/ReportViewer.razor.cs`, add a computed property `UseMasterDetail => _report?.SummaryColumns?.Count > 0` to select the rendering path
- [ ] T009 [US1] In `src/StageFright.UI/Shared/ReportViewer.razor`, add a `RadzenDataGrid<ReportSection>` rendering path (`AllowPaging="true" PageSize="15" class="rz-shadow-0" AllowSorting="false"`) guarded by `UseMasterDetail`, with one dynamically-generated `RadzenDataGridColumn` per `_report.SummaryColumns` entry (`Template` indexing into `context.SummaryRow!.Cells[i]`); leave the existing hand-rolled flat-table path unchanged for `!UseMasterDetail` (depends on T008)

**Checkpoint**: Loading the Member Account Summary report shows one collapsed row per member with aging totals; the other five reports still render exactly as before.

---

## Phase 4: User Story 2 - Committee member drills into one member's history (Priority: P1)

**Goal**: Clicking a member's collapsed row expands it in place to show that member's opening balance, every transaction, closing balance, and aging breakdown — identical to what the report showed before this redesign — independently of other rows, and operable by keyboard.

**Independent Test**: Seed a member with several transactions across the report period, load the report, click that member's row, and confirm the full opening balance / transaction list / closing balance / aging breakdown for that member appears, matching what the report showed in full before this redesign.

### Tests for User Story 2

- [ ] T010 [P] [US2] Add bUnit test in `tests/StageFright.UI.Tests/Shared/ReportViewerTests.cs` that expands a member's row (via the Radzen expand toggle) and asserts the markup then contains that member's Opening Balance, transaction rows, Closing Balance, and Aging row content
- [ ] T011 [P] [US2] Add bUnit test in `tests/StageFright.UI.Tests/Shared/ReportViewerTests.cs` with two members' rows, expanding one and asserting the other's row remains collapsed (its detail content is absent from the markup)
- [ ] T012 [P] [US2] Add bUnit test in `tests/StageFright.UI.Tests/Shared/ReportViewerTests.cs` asserting the expand/collapse control element is keyboard-focusable (e.g. a `<button>` or has `tabindex`) and exposes an `aria-expanded` attribute reflecting its current state, per FR-012/SC-005

### Implementation for User Story 2

- [ ] T013 [US2] In `src/StageFright.UI/Shared/ReportViewer.razor`, add the `<Template Context="section">` master-detail block to the `RadzenDataGrid` from T009, rendering the existing flat detail table (`section.Rows`/`section.Subtotal`, heading suppressed since the master row already shows the name) inside the expand panel (depends on T009)
- [ ] T014 [US2] Verify/adjust the Radzen master-detail expand toggle markup in `src/StageFright.UI/Shared/ReportViewer.razor` so it is keyboard-operable and carries an `aria-expanded` attribute reflecting expand state, satisfying FR-012 (depends on T013)

**Checkpoint**: User Stories 1 and 2 together deliver the core redesign — collapsed-by-default rows that expand independently to full detail, keyboard-accessible.

---

## Phase 5: User Story 3 - Committee member reviews only active members by default (Priority: P2)

**Goal**: Archived members are excluded from the report by default; an opt-in filter includes them, labeled "(Archived)".

**Independent Test**: Seed both active and archived members with balances, load the report with the archived-members filter off, and confirm only active members appear; toggle the filter on and confirm archived members (labeled "(Archived)") also appear.

### Tests for User Story 3

- [ ] T015 [P] [US3] Update `GenerateAsync_IncludesArchivedMembers` in `tests/StageFright.Reports.Tests/MemberAccountSummaryReportProviderTests.cs` to explicitly set `includeArchived=true` in the filters it passes, since archived members are no longer included by default
- [ ] T016 [P] [US3] Add test `GenerateAsync_ExcludesArchivedMembers_ByDefault` in `tests/StageFright.Reports.Tests/MemberAccountSummaryReportProviderTests.cs` asserting that with default filters (no `includeArchived` key set), an archived member's section is absent from `result.Sections`

### Implementation for User Story 3

- [ ] T017 [US3] In `src/StageFright.Reports/Providers/MemberAccountSummaryReportProvider.cs`, add `includeArchived` to the `Filters` list (`ReportFilterType.Boolean`, Label `"Show Archived Members"`, `DefaultValue = "false"`)
- [ ] T018 [US3] In `MemberAccountSummaryReportProvider.GenerateAsync`, only concatenate `archivedMembers` into `allMembers` when `filters.Get("includeArchived") == "true"` (depends on T017)

**Checkpoint**: Archived members are hidden by default and shown on request, without touching any GL/aging calculation (FR-009).

---

## Phase 6: User Story 4 - Committee member reads a member's history in standard accounting order (Priority: P3)

**Goal**: Confirm transactions within an expanded member's detail remain chronological (oldest-first), with Opening Balance first and Closing Balance immediately before the Aging row — unchanged from today.

**Independent Test**: Seed a member with transactions on at least three different dates, expand that member, and confirm transactions are listed from oldest date to newest date, with Opening Balance still first and Closing Balance still last (before Aging).

### Tests for User Story 4

- [ ] T019 [P] [US4] Add test in `tests/StageFright.Reports.Tests/MemberAccountSummaryReportProviderTests.cs` seeding transactions dated 2026-01-01, 2026-02-09, and 2026-02-16 in reverse/shuffled input order, and asserting `section.Rows` reads Opening Balance, then the three transaction rows oldest-to-newest, then Closing Balance, then Aging

### Implementation for User Story 4

- [ ] T020 [US4] Confirm `periodTxns.OrderBy(t => t.Date)` in `MemberAccountSummaryReportProvider.GenerateAsync` already produces oldest-first ordering (no code change expected); this task exists to lock the behavior in with the T019 regression test

**Checkpoint**: All four user stories are independently implemented and testable; the redesign is functionally complete.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Confirm no regressions in adjacent tests/reports and validate end-to-end

- [ ] T021 [P] Review `tests/StageFright.Integration.Tests/Scenarios/V6_AccountingReportsTests.cs` `MemberAccountSummary_GeneratesReport_WithMemberSection` test against the new default-excludes-archived behavior; update only if its seed data includes an archived member this test relies on
- [ ] T022 [P] Review `tests/StageFright.Integration.Tests/Scenarios/V11_ReportsMenuTests.cs` for any assertions on Member Account Summary report content beyond registration/menu presence; update only if found
- [ ] T023 Run the full `dotnet test` suite and confirm the five unaffected reports (Income Statement, Trial Balance, Account Register, Member List, Committee) still pass unchanged, plus all new/updated tests from T004–T022
- [ ] T024 Run `dotnet build` and `dotnet test` (full suite, no `--no-build`) one final time and report results, per CLAUDE.md's build/test verification requirement
- [ ] T025 Execute the manual validation checklist in `quickstart.md` against the running app (`dotnet run --project src/StageFright.App/`)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational (T002, T003)
- **User Story 2 (Phase 4)**: Depends on User Story 1's `RadzenDataGrid` scaffold (T009) — both are P1 and are typically delivered together
- **User Story 3 (Phase 5)**: Depends on Foundational only — independent of US1/US2, can be built in parallel with them
- **User Story 4 (Phase 6)**: Depends on Foundational only — independent of US1/US2/US3, can be built in parallel; largely a regression-test lock-in
- **Polish (Phase 7)**: Depends on all preceding phases

### Within Each User Story

- Tests are written before implementation and should fail first
- Model changes (Foundational) before provider changes before viewer changes

### Parallel Opportunities

- T002 and T003 (Foundational) in parallel
- T004, T005, T006 (US1 tests) in parallel
- T010, T011, T012 (US2 tests) in parallel
- T015, T016 (US3 tests) in parallel
- US3 (Phase 5) and US4 (Phase 6) can proceed in parallel with US1/US2 (Phases 3-4) once Foundational is done, since they touch the provider's filter/ordering logic rather than the `ReportViewer` grid
- T021 and T022 (Polish reviews) in parallel

---

## Implementation Strategy

### MVP First (User Stories 1 + 2)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (blocks everything)
3. Complete Phase 3: User Story 1 (collapsed view)
4. Complete Phase 4: User Story 2 (expand/collapse) — together these two P1 stories are the MVP
5. **STOP and VALIDATE**: Run quickstart.md steps 1-3

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. US1 + US2 (both P1) → core redesign working → validate independently
3. US3 (P2, archived filter) → validate independently
4. US4 (P3, ordering confirmation) → validate independently
5. Polish → full regression pass + manual quickstart validation

---

## Notes

- [P] tasks touch different files/tests with no ordering dependency
- [Story] label maps each task to its user story for traceability
- Verify new tests fail before implementing the corresponding change (TDD)
- Per CLAUDE.md: commit all changed/new files at the end of the task with `git add -A`
