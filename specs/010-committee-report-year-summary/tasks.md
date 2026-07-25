---

description: "Task list for Committee Report Year Summary"
---

# Tasks: Committee Report Year Summary

**Input**: Design documents from `/specs/010-committee-report-year-summary/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/committee-report-row-shape-contract.md, quickstart.md

**Tests**: Included — CLAUDE.md mandates exhaustive code-path test coverage before merge, and plan.md's Constitution Check (§11) lists the specific FR/edge-case tests required for this feature.

**Organization**: Tasks are grouped by user story (spec.md priorities P1–P3) to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, or same new file but non-overlapping test methods with no ordering dependency)
- **[Story]**: Which user story this task belongs to (US1–US3)

## Path Conventions

Single existing solution (`StageFrightCommunity.slnx`), no new projects. All paths are relative to the repo root. Per plan.md's Scale/Scope, this feature touches exactly one provider file, one new test file, and one existing integration test file.

---

## Phase 1: Setup

**Purpose**: Confirm a clean baseline before making changes

- [x] T001 Run `dotnet build` and `dotnet test` (full suite) from the repo root and confirm everything is green before starting, per CLAUDE.md's Build & Test Verification requirement

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared infrastructure every user story would depend on

No foundational tasks are required for this feature. `ReportData.SummaryColumns` and `ReportSection.SummaryRow` already exist (added in spec 005's master-detail extension) and this feature introduces no repository, entity, or migration changes (see data-model.md and research.md). User Story 1 can begin immediately after Phase 1.

---

## Phase 3: User Story 1 - Year-grouped committee overview (Priority: P1) 🎯 MVP

**Goal**: The Committee Report groups committee membership records by year (most recent first), with one summary row per year showing the year and the total count of committee positions recorded that year, respecting the existing Member Status filter and omitting years with no matching records.

**Independent Test**: Generate the Committee Report against seed data spanning multiple years and confirm exactly one summary row appears per year that has committee records, each showing the year and the count of committee positions recorded that year, most-recent-year-first.

### Tests for User Story 1

> **NOTE: Write these tests FIRST in the new file, ensure they FAIL before implementation**

- [x] T002 [P] [US1] Create `tests/StageFright.Reports.Tests/CommitteeReportProviderTests.cs` with a test asserting `result.Sections` contains exactly one section per year that has matching records, ordered most-recent-year-first (FR-001)
- [x] T003 [P] [US1] In the same file, add a test asserting `result.SummaryColumns` equals `[Year, Positions Recorded]` and every `section.SummaryRow!.Cells` equals `[year.ToString(), recordCount.ToString()]`, where `recordCount` is the raw count of committee membership records for that year (not the number of position lines) — FR-002, contract rule 5
- [x] T004 [P] [US1] In the same file, add a test asserting the existing `memberFilter` values (`Active Only` / `Archived Only` / `All`) continue to determine which members' committee records are included in each year's grouping and count, matching the current filter behavior (FR-008)
- [x] T005 [P] [US1] In the same file, add a test asserting a year with zero matching committee records under the active filter does not appear as a `ReportSection` at all — no "vacant year" placeholder (FR-009)
- [x] T006 [P] [US1] In the same file, add a test asserting that when no committee records match the active filter at all, `result.Sections` is empty (the existing `RadzenDataGrid` empty-state handles rendering; no new empty-state code is introduced) (FR-012)

### Implementation for User Story 1

- [x] T007 [US1] Rewrite `GenerateAsync` in `src/StageFright.Reports/Providers/CommitteeReportProvider.cs`: for each filtered member (existing `GetByStatusAsync`/`GetArchivedAsync`/`GetAllAsync` calls per `memberFilter`, unchanged), fetch their committee memberships via the existing `GetByMemberAsync` and flatten into a single list of `(Member, CommitteeMembership)` pairs; group the flattened list by `Year` descending, omitting years with no records; build one `ReportSection` per year with `Heading` set to the year string and `SummaryRow = [year.ToString(), recordCount.ToString()]`; set `ReportData.Columns = [Year, Position, Member(s)]` and `ReportData.SummaryColumns = [Year, Positions Recorded]`. For this task, populate each section's `Rows` with one interim row per raw record (`Cells = [year.ToString(), membership.Position.Trim(), member.Name]`, no case-insensitive consolidation yet) — User Story 2 replaces this with the full position-line aggregation. (Depends on T002–T006 existing as failing tests first.)

**Checkpoint**: The report shows one row per year with correct year ordering, accurate position counts, correct filter behavior, and correct omission of empty years/empty-filter results. Detail-row content within a year is still raw/unconsolidated pending User Story 2.

---

## Phase 4: User Story 2 - Role breakdown within each year (Priority: P2)

**Goal**: Within each year, President/Secretary/Treasurer each appear as their own line (showing the member or "Vacant"), every other distinct non-blank position label appears as its own alphabetically-ordered line, and blank-position members are grouped under a single "General Committee Members" line — all matched case-insensitively and trimmed, with multiple members on the same line listed together alphabetically rather than any being dropped.

**Independent Test**: Generate the report for a year with a full committee (President, Secretary, Treasurer, a "Welfare Officer", and several unlabeled general members) and confirm every position is shown correctly labeled under that year.

### Tests for User Story 2

- [x] T008 [P] [US2] In `tests/StageFright.Reports.Tests/CommitteeReportProviderTests.cs`, add a test asserting a year's `Rows` always include a `President`, `Secretary`, and `Treasurer` line, with `Cells[2] == "Vacant"` for any of the three not recorded that year (FR-003, FR-004, FR-005)
- [x] T009 [P] [US2] In the same file, add a test asserting a non-named position value (e.g., `"Welfare Officer"`) appears as its own line, positioned after the President/Secretary/Treasurer lines and ordered alphabetically among other non-named position lines (FR-006)
- [x] T010 [P] [US2] In the same file, add a test asserting members with a blank or whitespace-only `Position` are grouped into a single `"General Committee Members"` line, members sorted alphabetically by name, and that this line is ordered last (after all named-role and other-position lines) (FR-006a)
- [x] T011 [P] [US2] In the same file, add a test asserting case-insensitive/trimmed matching collapses variants such as `" president"`, `"President "`, and `"PRESIDENT"` into a single `President` line, and separately that two other-position values differing only by case/whitespace (e.g., `"welfare officer"` vs `"Welfare Officer "`) collapse into one line displayed using the first-encountered trimmed text (FR-007)
- [x] T012 [P] [US2] In the same file, add a test asserting that when two members are recorded against the same position label in the same year, both are listed together on that one line (e.g., `"Alice, Bob"`) rather than either being dropped or overwritten (FR-010)
- [x] T013 [P] [US2] In the same file, add a test asserting members within any multi-member line (named role, other position, or General Committee Members) are always ordered alphabetically by name (FR-006, FR-006a, FR-010)

### Implementation for User Story 2

- [x] T014 [US2] In `src/StageFright.Reports/Providers/CommitteeReportProvider.cs`, replace the interim per-record row logic from T007 with full position-line aggregation per year: normalize each record's `Position` (trim + lowercase) as the grouping key; always emit `President`, `Secretary`, and `Treasurer` lines first (canonical display labels regardless of source casing), using `"Vacant"` when no record matches that key for the year; then emit every other distinct non-blank-key group as its own line, ordered alphabetically (case-insensitive) by the first-encountered trimmed display text; then, if any blank/whitespace-only-position records exist that year, exactly one `"General Committee Members"` line last; within every line, list member names alphabetically, comma-separated. (Depends on T008–T013 existing as failing tests first, and on T007.)

**Checkpoint**: User Stories 1 and 2 together deliver the full year-summary/expand-to-role-breakdown redesign described in spec.md, matching `quickstart.md` steps 2–3.

---

## Phase 5: User Story 3 - Exportable, consistent output (Priority: P3)

**Goal**: The redesigned report continues to export cleanly to PDF and CSV through the existing, unmodified rendering pipeline, with the year and role/member information fully legible and present on every row.

**Independent Test**: Export a generated report to both PDF and CSV and confirm the year/role structure is legible and correctly represented in both formats.

### Tests for User Story 3

- [x] T015 [P] [US3] In `tests/StageFright.Reports.Tests/CommitteeReportProviderTests.cs`, add a test asserting `Cells[0]` of every `ReportRow` across all sections equals that section's year string — the year must never be carried only in `Heading` (contract rule 1, prerequisite for CSV correctness)
- [x] T016 [P] [US3] In the same file, add a test that pipes `CommitteeReportProvider.GenerateAsync`'s `ReportData` through the existing `CsvReportExporter.Export` (unchanged) and asserts the exported CSV contains the year value and the position/member(s) text on every data row, for a report spanning multiple years and position lines (FR-011, US3 Acceptance Scenario 2)
- [x] T017 [P] [US3] In the same file, add a test that pipes the same `ReportData` through the existing `PdfReportRenderer.Render` (unchanged) and asserts it returns a non-empty byte array without throwing, for a multi-year report with named roles, an other-position line, a General Committee Members line, and a vacant role (FR-011, US3 Acceptance Scenario 1)

### Implementation for User Story 3

- [x] T018 [US3] No source changes expected: confirm `PdfReportRenderer` and `CsvReportExporter` require no modification, since both already read only `Columns`/`Sections[].Rows`/`Subtotal`/`GrandTotal` and are oblivious to `SummaryColumns`/`SummaryRow` (per data-model.md/research.md). This task exists to lock that behavior in with the T015–T017 tests passing against the unmodified renderers.

**Checkpoint**: All three user stories are independently implemented and testable; PDF/CSV exports are verified against the new year/role row shape with no information loss.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Fix the one known regression and confirm no other regressions before merge

- [x] T019 [P] Update `CommitteeReport_DefaultFilter_ReturnsActiveOnly` in `tests/StageFright.Integration.Tests/Scenarios/V11_ReportsMenuTests.cs`: it currently asserts `Cells[0]` against the old `[Member, Year, Position]` row shape, but `Cells[0]` is now the year. Update the assertions to check member names via the new `[Year, Position, Member(s)]` row shape (e.g. assert against `Cells[2]`, or against the relevant `Rows`/`SummaryRow` content for the active/archived members seeded in that test)
- [x] T020 Run `dotnet build` and the full `dotnet test` suite (no `--no-build`) from the repo root and confirm everything is green, including all new/updated tests from T002–T019, per CLAUDE.md's Build & Test Verification requirement
- [x] T021 Execute the manual validation checklist in `quickstart.md` against the running app (`dotnet run --project src/StageFright.App/`), covering User Stories 1–3 and the empty-filter edge case

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Empty — no tasks block User Story 1
- **User Story 1 (Phase 3)**: Depends on Phase 1 only
- **User Story 2 (Phase 4)**: Depends on User Story 1's scaffold (T007) — both are needed together for the full redesign, but US1's independent test criteria can be validated before US2 is implemented
- **User Story 3 (Phase 5)**: Depends on User Story 1 (T007, for the year-in-every-row contract rule) and benefits from User Story 2 being complete for a fully representative export test, but its own tests (T015–T017) only require the row shape, not the finished role-breakdown content
- **Polish (Phase 6)**: Depends on all preceding phases (T019 specifically requires the final row shape from T014)

### Within Each User Story

- Tests are written before implementation and should fail first
- User Story 1's scaffold (T007) precedes User Story 2's refinement (T014) since T014 edits the same method

### Parallel Opportunities

- T002–T006 (US1 tests) in parallel
- T008–T013 (US2 tests) in parallel
- T015–T017 (US3 tests) in parallel
- US3's test-writing (T015–T017) can proceed in parallel with US2 (Phase 4), since both add tests to the same new file without editing provider code themselves — only implementation tasks T014 and T018 are sequential within `CommitteeReportProvider.cs`

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Test asserting one ReportSection per year, ordered most-recent-first, in tests/StageFright.Reports.Tests/CommitteeReportProviderTests.cs"
Task: "Test asserting SummaryColumns/SummaryRow shape and record count, in tests/StageFright.Reports.Tests/CommitteeReportProviderTests.cs"
Task: "Test asserting Member Status filter continues to scope year groupings, in tests/StageFright.Reports.Tests/CommitteeReportProviderTests.cs"
Task: "Test asserting years with no matching records are omitted, in tests/StageFright.Reports.Tests/CommitteeReportProviderTests.cs"
Task: "Test asserting empty-filter result yields empty Sections, in tests/StageFright.Reports.Tests/CommitteeReportProviderTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 3: User Story 1 (year grouping, summary row, count, filter, omission)
3. **STOP and VALIDATE**: Confirm US1's independent test criteria against seed data spanning multiple years
4. Deploy/demo if ready — a scannable year-by-year view is already useful on its own

### Incremental Delivery

1. Setup → foundation confirmed clean
2. US1 (P1) → year-grouped summary rows working → validate independently (MVP!)
3. US2 (P2) → role breakdown within each year → validate independently
4. US3 (P3) → PDF/CSV export verified against the new shape → validate independently
5. Polish → fix the one known regression (T019) + full regression pass + manual quickstart validation

---

## Notes

- [P] tasks touch different test methods or files with no ordering dependency
- [Story] label maps each task to its user story for traceability
- Verify new tests fail before implementing the corresponding change (TDD)
- T007 and T014 both edit `src/StageFright.Reports/Providers/CommitteeReportProvider.cs` — they are sequential, not parallel, despite both being "[US*]" tagged
- Per CLAUDE.md: commit all changed/new files at the end of the task with `git add -A`
