---

description: "Task list template for feature implementation"
---

# Tasks: Printable Member Attendance Roll

**Input**: Design documents from `/specs/012-printable-attendance-roll/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/attendance-roll-contract.md](./contracts/attendance-roll-contract.md), [quickstart.md](./quickstart.md)

**Tests**: Included and mandatory — CLAUDE.md's "Exhaustive code-path test coverage" rule is non-negotiable for this repository, and quickstart.md enumerates the exact four automated test layers (`StageFright.Core.Tests`, `StageFright.Reports.Tests`, `StageFright.UI.Tests`, `StageFright.Integration.Tests`) that must pass before manual validation.

**Organization**: Tasks are grouped by user story (US1/US2/US3, matching spec.md's P1/P2/P3 priorities). This is a purely additive feature (no existing type is changed shape, only two new files' worth of DTOs/contracts plus one new button on an existing page) — the solution stays green throughout; each story phase adds real, independently-verifiable behavior on top of the last, per research.md's explicit division of `AttendanceRollService`/`AttendanceRollPdfRenderer` responsibilities across stories.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Paths are relative to the repository root (`c:\SourceCode\StageFrightCommunity`)

---

## Phase 1: Setup

**Purpose**: Confirm a known-green baseline before adding new files.

- [X] T001 Run `dotnet restore`, `dotnet build`, and `dotnet test` from the repo root and confirm all five test projects are currently green, so any later failure is attributable to this feature's changes

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish the shared DTO/contract shapes every user story phase builds on (per data-model.md). No behavior yet — just the types.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 [P] Create `src/StageFright.Core/Modules/Rehearsals/AttendanceRollData.cs`: sealed class with `DateTime RehearsalDate { get; init; }`, `TimeSpan RehearsalTime { get; init; }`, `IReadOnlyList<AttendanceRollMember> Members { get; init; } = Array.Empty<AttendanceRollMember>();` — exactly as specified in data-model.md
- [X] T003 [P] Create `src/StageFright.Core/Modules/Rehearsals/AttendanceRollMember.cs`: sealed class with `string FirstName { get; init; } = string.Empty;`, `string LastName { get; init; } = string.Empty;`, `bool AnnualFeePaid { get; init; }` — no `MemberId`, no `Attended`/`RehearsalFeePaid` fields (those two checkboxes are always rendered blank, per data-model.md)
- [X] T004 [P] Create `src/StageFright.Core/Modules/Rehearsals/IAttendanceRollService.cs`: `Task<AttendanceRollData> GenerateAsync(Guid rehearsalId, CancellationToken ct = default)`, XML-doc noting it throws `EntityNotFoundException` for an unknown rehearsal id, per data-model.md/contracts/attendance-roll-contract.md
- [X] T005 [P] Create `src/StageFright.Reports/Rendering/IAttendanceRollPdfRenderer.cs`: `byte[] Render(AttendanceRollData data, string organizationName = "")`, XML-doc matching contracts/attendance-roll-contract.md's postconditions (non-empty output for any input including a zero-member roll; pure function, no I/O)

**Checkpoint**: DTOs and contracts exist. User story implementation can now begin.

---

## Phase 3: User Story 1 - Print a roll instead of maintaining a separate paper list (Priority: P1) 🎯 MVP

**Goal**: A "Print Roll" action on each scheduled rehearsal generates a print-ready PDF listing every currently-active member, sorted by surname then first name, each with blank "Attended" and "Rehearsal Fee Paid" checkboxes; an empty active-member list shows an inline message instead of a blank PDF.

**Independent Test**: Generate a roll for a scheduled rehearsal and confirm it lists every currently active member exactly once, sorted alphabetically by surname, each with blank "Attended" and "Rehearsal Fee Paid" checkboxes, ready to print; confirm archived/soft-deleted members never appear; confirm a zero-active-member rehearsal shows an empty-state message instead of a PDF.

### Tests for User Story 1

- [X] T006 [P] [US1] Create `tests/StageFright.Core.Tests/Modules/Rehearsals/AttendanceRollServiceTests.cs` (NSubstitute mocks for `IRehearsalRepository`/`IMemberService`, following `MemberServiceTests.cs`'s convention): `GenerateAsync` throws `EntityNotFoundException` for an unknown `rehearsalId`; returns exactly the members from `GetByStatusAsync(MemberStatus.Active)` (archived/inactive/soft-deleted members that the mock never returns don't appear); orders results by `LastName` then `FirstName`, including a same-surname pair to confirm first-name sub-sort; returns an empty `Members` list (not a throw) when no active members exist; copies `RehearsalDate`/`RehearsalTime` from the looked-up `Rehearsal`
- [X] T007 [P] [US1] Create `tests/StageFright.Reports.Tests/AttendanceRollPdfRendererTests.cs` (mirrors `PdfAndCsvRendererTests.cs`'s non-null/non-empty/no-throw convention — QuestPDF byte-array output can't be asserted on visual content): `Render` returns a non-empty `byte[]` for a populated roster; returns a non-empty `byte[]` (no throw) for a zero-member `AttendanceRollData`; does not throw when `organizationName` is empty
- [X] T008 [P] [US1] Extend `tests/StageFright.UI.Tests/Pages/Rehearsals/RehearsalListTests.cs`: a "Print Roll" button renders in the Actions column for every non-deleted rehearsal row; clicking it when `IAttendanceRollService.GenerateAsync` returns an empty `Members` list shows an inline alert message and `IAttendanceRollPdfRenderer.Render` is never called (`DidNotReceive()`); clicking it when `GenerateAsync` throws shows a generic error alert and `Render` is never called — do **not** add a test that clicks through to a successful render (no seam exists to intercept the real `File.WriteAllBytes`/`Process.Start` call, matching the existing precedent in `ReportViewerTests.cs`, which likewise never click-tests its own `PrintReport()`)
- [X] T009 [P] [US1] Extend `tests/StageFright.Integration.Tests/Scenarios/V3_RehearsalAttendanceTests.cs` with an `AttendanceRollService` built against the real SQLite in-memory `StageFrightDbContext`: `GenerateAsync` returns only active members sorted by surname/first name, excludes a soft-deleted member, returns an empty list for a rehearsal with zero active members, and throws `EntityNotFoundException` for a random unknown `rehearsalId`

### Implementation for User Story 1

- [X] T010 [US1] Create `src/StageFright.Core/Modules/Rehearsals/AttendanceRollService.cs` implementing `IAttendanceRollService`: constructor-inject `IRehearsalRepository`, `IMemberService`; `GenerateAsync` looks up the rehearsal via `_rehearsalRepo.GetByIdAsync(rehearsalId, ct) ?? throw new EntityNotFoundException("Rehearsal", rehearsalId, nameof(GenerateAsync))` (exact precedent: `RehearsalService.FreezeAttendanceRateAsync`), fetches `_memberService.GetByStatusAsync(MemberStatus.Active, ct)`, orders by `LastName` then `FirstName`, maps each to an `AttendanceRollMember` with `AnnualFeePaid = false` (placeholder — computed for real in US2), and returns an `AttendanceRollData` carrying the rehearsal's `Date`/`Time` — depends on T002-T004
- [X] T011 [US1] Create `src/StageFright.Reports/Rendering/AttendanceRollPdfRenderer.cs` implementing `IAttendanceRollPdfRenderer`: static constructor sets `QuestPDF.Settings.License = LicenseType.Community` (matching `PdfReportRenderer`); header block (organization name, "Attendance Roll" title, rehearsal date/time subtitle from `data.RehearsalDate`/`data.RehearsalTime`, generated-at timestamp) mirroring `PdfReportRenderer`'s header; content is a single flowing QuestPDF `Table` (one row per member: Name column showing `$"{m.LastName}, {m.FirstName}"`, an "Attended" checkbox cell, a "Rehearsal Fee Paid" checkbox cell — both always empty); render each checkbox as a small bordered `Container` (`.Border(1).Width(Xpt).Height(Xpt)`), **not** a Unicode glyph, per research.md Decision 4; footer with `CurrentPageNumber()`/`TotalPages()` matching `PdfReportRenderer`; a zero-member `data.Members` renders a header-only page without throwing — depends on T005
- [X] T012 [US1] Add a "Print Roll" button to the existing Actions column `<Template>` in `src/StageFright.UI/Pages/Rehearsals/RehearsalList.razor`, available for every row alongside/underneath the existing "Record Attendance"/"Recorded" content, with an `aria-label` following the existing "for @r.Date..." convention; add an inline alert element below the grid (separate from the existing `_errorMessage` load-failure alert) bound to a new roll-specific message field
- [X] T013 [US1] Implement the `PrintRoll(Guid rehearsalId)` handler in `src/StageFright.UI/Pages/Rehearsals/RehearsalList.razor.cs`: inject `IAttendanceRollService`, `IAttendanceRollPdfRenderer`, `ISettingsService`; call `GenerateAsync(rehearsalId)`; if `Members.Count == 0`, set the roll alert message (e.g. "No active members found — nothing to print.") and return without rendering, matching `AttendanceGrid.razor.cs`'s empty-state precedent (research.md Decision 6); otherwise fetch `SettingsService.GetAsync()` for `OrganizationName`, call `PdfRenderer.Render(rollData, orgName)`, write the bytes to a temp file (`Path.Combine(Path.GetTempPath(), $"attendance-roll_{Guid.NewGuid():N}.pdf")`), and launch it via `Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true })`, exactly matching `ReportViewer.razor.cs`'s `PrintReport()` pattern; wrap in try/catch setting the roll alert message on any exception — depends on T010, T011, T012
- [X] T014 [US1] Register `IAttendanceRollService`/`AttendanceRollService` and `IAttendanceRollPdfRenderer`/`AttendanceRollPdfRenderer` as `AddScoped` in `src/StageFright.App/MauiProgram.cs`'s `RegisterCoreServices`, alongside `IRehearsalService`/`IPdfReportRenderer` respectively — depends on T010, T011

**Checkpoint**: User Story 1 is fully functional and testable independently — a roll can be printed for any scheduled rehearsal with correct active-member listing, blank Attended/Rehearsal Fee Paid checkboxes, and a proper empty-state message.

---

## Phase 4: User Story 2 - See current-year annual fee status at a glance (Priority: P2)

**Goal**: Each row's "Annual Fee Paid" checkbox is checked only when the member's current-calendar-year Annual fee has no outstanding GL balance.

**Independent Test**: Generate the roll for a mix of members — fully-paid current-year annual fee, unpaid/partial, and no annual fee recorded yet for the current year — and confirm the "Annual Fee Paid" checkbox is checked only for the fully-paid member.

### Tests for User Story 2

- [X] T015 [P] [US2] Extend `tests/StageFright.Core.Tests/Modules/Rehearsals/AttendanceRollServiceTests.cs` with mocked `IMemberBalanceService`/`IFeeRepository`: a member with a fully-paid current-year Annual fee (`AnnualFeeExistsAsync` true, no matching outstanding entry) → `AnnualFeePaid == true`; a member with an unpaid/partial current-year Annual fee (matching outstanding entry) → `false`; a member with no current-year Annual fee record at all (`AnnualFeeExistsAsync` false) → `false`; a member with an overpaid/credit balance (`AnnualFeeExistsAsync` true, no matching outstanding entry since `GetOutstandingFeesAsync` filters `RemainingAmount <= 0`) → `true`
- [X] T016 [P] [US2] Extend `tests/StageFright.Reports.Tests/AttendanceRollPdfRendererTests.cs`: rendering a roster with a mix of `AnnualFeePaid = true`/`false` members returns a non-empty `byte[]` without throwing, and the renderer emits a third "Annual Fee Paid" checkbox column alongside the existing two
- [X] T017 [P] [US2] Extend `tests/StageFright.Integration.Tests/Scenarios/V3_RehearsalAttendanceTests.cs`: seed a real fully-settled current-year Annual `Fee` + matching GL debit/credit pair for one member (→ `AnnualFeePaid == true`), an unpaid current-year Annual `Fee` for another (→ `false`), and a member with no `Fee` record for the current year at all (→ `false`), then assert `AttendanceRollService.GenerateAsync` reflects all three correctly

### Implementation for User Story 2

- [X] T018 [US2] Update `src/StageFright.Core/Modules/Rehearsals/AttendanceRollService.cs`: constructor-inject `IMemberBalanceService`, `IFeeRepository`; add `private async Task<bool> IsCurrentYearAnnualFeePaidAsync(Guid memberId, CancellationToken ct)` implementing exactly data-model.md's rule — `return await _feeRepo.AnnualFeeExistsAsync(memberId, currentYear, ct) && !(await _memberBalanceService.GetOutstandingFeesAsync(memberId, ct)).Any(f => f.FeeType == FeeType.Annual && f.FeeDate.Year == currentYear);` — and call it per member in `GenerateAsync`, replacing the `AnnualFeePaid = false` placeholder from T010 — depends on T010
- [X] T019 [US2] Update `src/StageFright.Reports/Rendering/AttendanceRollPdfRenderer.cs`: add a third "Annual Fee Paid" checkbox column cell per row — the same bordered `Container` style as the other two checkbox cells, but filled/marked (e.g. centered bold "X" or solid background) when `m.AnnualFeePaid` is `true`, left empty otherwise — depends on T011

**Checkpoint**: User Stories 1 AND 2 both work independently — the roll's Annual Fee Paid checkbox accurately reflects live GL-derived fee status.

---

## Phase 5: User Story 3 - Compact, print-friendly layout (Priority: P3)

**Goal**: The roll is laid out in a two-column, print-optimized format: minimal-width checkbox columns, wrapping column headings, surnames in capitals, and same-page overflow into a second column (then additional pages) for larger rosters.

**Independent Test**: Generate a roll with enough active members to overflow a single column and confirm the list continues into a second column on the same page, checkbox columns are visibly narrower than the name column, column headings wrap rather than widen/truncate, and every surname is displayed in capital letters.

### Tests for User Story 3

- [X] T020 [P] [US3] Extend `tests/StageFright.Reports.Tests/AttendanceRollPdfRendererTests.cs` with pagination-boundary cases around the renderer's `RowsPerColumn` constant — exactly `RowsPerColumn` members (fills column one, no column two), `RowsPerColumn + 1` (spills into column two, same page), and `2 * RowsPerColumn + 1` (spills onto a second page) — each asserted as a non-empty `byte[]` with no thrown exception (content/visual correctness is not assertable from raw PDF bytes; see T022's manual check)

### Implementation for User Story 3

- [X] T021 [US3] Refactor `src/StageFright.Reports/Rendering/AttendanceRollPdfRenderer.cs` per research.md Decision 3: introduce `private const int RowsPerColumn = <tuned value>;`; replace the single flowing `Table` from T011/T019 with `data.Members.Chunk(RowsPerColumn * 2)` → one QuestPDF `container.Page(...)` per chunk, each page containing a `Row` of two side-by-side `Table` columns (left = first `RowsPerColumn` of the chunk, right = the remainder); each column `Table`'s `ColumnsDefinition` uses one wide `RelativeColumn(4)` Name column and three `RelativeColumn(1)` checkbox columns (FR-010 minimal width); render the Name cell as `$"{m.LastName.ToUpperInvariant()}, {m.FirstName}"` (FR-003 capitals); rely on QuestPDF's default text wrapping for the narrow checkbox column headers (FR-011, no explicit line breaks needed); keep the document-wide footer (`CurrentPageNumber()`/`TotalPages()`) working unchanged across the multiple `Page()` blocks; a zero-member `data.Members` still yields exactly one header-only page (an empty `Chunk` produces no chunks) without throwing — depends on T019
- [X] T022 [US3] Manually verify (per research.md's Outstanding Risks — QuestPDF's rendered row height isn't fully reproducible from unit tests alone) that the `RowsPerColumn` constant and checkbox `RelativeColumn` ratio from T021 produce a visually correct printed page: run the app, generate a roll for a rehearsal with enough active members to overflow one column, and open the resulting PDF to confirm (a) column one fills completely before column two continues, (b) the roll continues onto additional pages with repeated headers if the roster is larger still, (c) the three checkbox columns are visibly narrower than the Name column, (d) column headings wrap onto multiple lines rather than being cut off or widening the column, and (e) every surname renders in capital letters; adjust `RowsPerColumn`/the column ratio and re-run T020 if any check fails

**Checkpoint**: All three user stories are independently functional — the roll is a full-featured, print-ready two-column attendance sheet.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final whole-solution verification once all three stories are in place.

- [X] T023 Walk through every manual validation scenario in [quickstart.md](./quickstart.md) (Scenarios 1-5: basic roll generation, empty active-member list, annual-fee-paid accuracy, compact two-column layout, re-generation reflects live data) against a running `dotnet run --project src/StageFright.App/` instance, plus the "Rollback / no-op safety check" confirming no `Member`, `Rehearsal`, `Fee`, `Payment`, `Transaction`, or GL record was created, changed, or removed by generating a roll
- [X] T024 Run `dotnet build` and the full `dotnet test` suite (all five projects) from the repo root and confirm everything is green, per CLAUDE.md's build/test verification rule
- [X] T025 [P] Tick off the success-criteria checklist (SC-001 through SC-005) in [spec.md](./spec.md) based on the verification in T023/T024

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — run first to confirm a green baseline
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories; T002-T005 are four independent new files and can run in parallel
- **User Story 1 (Phase 3)**: Depends on Foundational completion — no dependency on US2/US3
- **User Story 2 (Phase 4)**: Depends on Foundational completion; its implementation tasks (T018, T019) modify files US1 created (T010, T011) — sequence after US1 rather than in parallel with it
- **User Story 3 (Phase 5)**: Depends on Foundational completion; its implementation task (T021) refactors the renderer file US2 last touched (T019) — sequence after US2
- **Polish (Phase 6)**: Depends on all three user stories being complete

### Within Each User Story

- Tests are written first per task list order and should fail (or fail to compile) before the paired implementation task lands
- Within US1: T010 (service) and T011 (renderer) before T013 (UI handler, which calls both); T012 (button markup) before T013 (handler wired to the button); T014 (DI registration) after T010/T011 exist as concrete classes
- Within US2: T018 (service) and T019 (renderer) are independent of each other but both depend on their US1 predecessors (T010, T011/T019)
- Within US3: T021 (renderer refactor) before T022 (manual visual check of what T021 produced)

### Parallel Opportunities

- All of Phase 2 (T002-T005) can run in parallel — four independent new files
- Within US1, all four test tasks (T006-T009) can run in parallel — different test files/projects
- Within US2, all three test tasks (T015-T017) can run in parallel
- T015 (service tests) and T016 (renderer tests) within US2 touch different files and can run in parallel with each other even though both eventually depend on the same US1 predecessor files

---

## Parallel Example: Phase 2 (Foundational)

```bash
Task: "Create AttendanceRollData.cs"
Task: "Create AttendanceRollMember.cs"
Task: "Create IAttendanceRollService.cs"
Task: "Create IAttendanceRollPdfRenderer.cs"
```

## Parallel Example: User Story 1 tests

```bash
Task: "Create AttendanceRollServiceTests.cs — not-found, active-only, sort, empty-list cases"
Task: "Create AttendanceRollPdfRendererTests.cs — non-empty byte[], zero-member no-throw"
Task: "Extend RehearsalListTests.cs — button renders, empty-state alert, error alert"
Task: "Extend V3_RehearsalAttendanceTests.cs — real-SQLite active-member listing"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (DTOs + contracts)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Print Roll works end-to-end for a scheduled rehearsal with correct active-member listing and blank checkboxes; empty-state message shows for a zero-active-member rehearsal
5. Deploy/demo if ready — this alone replaces the manually maintained paper roll

### Incremental Delivery

1. Complete Setup + Foundational → foundation ready
2. Add User Story 1 → verify independently → deploy/demo (MVP!)
3. Add User Story 2 → verify Annual Fee Paid accuracy independently → deploy/demo
4. Add User Story 3 → verify two-column print layout independently → deploy/demo
5. Each story adds real value without breaking the previous one — the solution stays green throughout since no story removes or reshapes what an earlier story built

### Parallel Team Strategy

With multiple developers, once Phase 2 lands:

- Developer A: User Story 1 (service + renderer skeleton + UI button/handler + DI)
- Developer B: waits for US1's files to exist, then takes User Story 2 (fee-paid computation + column)
- Developer C: waits for US2's renderer changes, then takes User Story 3 (two-column layout refactor)

Because US2 and US3 each modify the same two core files (`AttendanceRollService.cs`, `AttendanceRollPdfRenderer.cs`) that the prior story just created/touched, true parallel staffing across stories is limited — sequential handoff (A → B → C) is more realistic here than the fully-parallel case described in the template.

---

---

## Phase 7: Correction — 2026-07-28 (Annual Fee Paid removed; real attendance/fee-paid state; point-in-time membership; fee-amount header)

**Purpose**: The Phase 3-6 implementation above did not match the actual requirement — see spec.md's "Correction" clarification session, research.md Decisions 5, 8-10, and the corresponding rewrites of data-model.md and contracts/attendance-roll-contract.md. This phase corrects it.

- [X] T026 Rewrite spec.md, research.md, data-model.md, contracts/attendance-roll-contract.md, and quickstart.md to describe the corrected behavior (point-in-time active membership, real "Present"/fee-paid state, removed Annual Fee Paid column, fee-amount column heading)
- [X] T027 [P] Update `src/StageFright.Core/Modules/Rehearsals/AttendanceRollMember.cs`: remove `AnnualFeePaid`; add `bool Attended { get; init; }` and `bool RehearsalFeePaid { get; init; }`
- [X] T028 [P] Update `src/StageFright.Core/Modules/Rehearsals/AttendanceRollData.cs`: add `decimal AttendanceFeeAmount { get; init; }`
- [X] T029 [P] Update `src/StageFright.Core/Modules/Rehearsals/IAttendanceRollService.cs` XML doc to describe point-in-time membership and real Present/RehearsalFeePaid computation
- [X] T030 Rewrite `src/StageFright.Core/Modules/Rehearsals/AttendanceRollService.cs`: replace `IMemberService` dependency with `IMemberRepository` (call `GetActiveAsOfAsync(rehearsal.Date, ct)` instead of `GetByStatusAsync(MemberStatus.Active, ct)`); add `IAttendanceRepository` dependency (`GetByRehearsalAsync(rehearsalId, ct)`, indexed by `MemberId`, to populate `Attended`); add `ISettingsRepository` dependency (`GetAsync(ct)` for `AttendanceFeeAmount`); replace `IsCurrentYearAnnualFeePaidAsync` with `IsRehearsalFeePaidAsync(memberId, rehearsalId, ct)` per data-model.md/research.md Decision 5 — depends on T027, T028
- [X] T031 Update `src/StageFright.Reports/Rendering/AttendanceRollPdfRenderer.cs`: drop the fourth "Annual Fee Paid" column; rename "Attended" header to "Present" (checked per `m.Attended`); replace "Rehearsal Fee Paid" header text with `data.AttendanceFeeAmount.ToString("C0")` (checked per `m.RehearsalFeePaid`) — depends on T027, T028
- [X] T032 [P] Rework `tests/StageFright.Core.Tests/Modules/Rehearsals/AttendanceRollServiceTests.cs`: replace Annual-Fee-Paid cases with Present/RehearsalFeePaid cases (not-yet-recorded → both blank; attended+paid → both checked; attended+marked-unpaid → Present checked/fee unchecked; absent/no-record → both unchecked); replace active-member mocking with `IMemberRepository.GetActiveAsOfAsync` mocking, including a case where a member's status changed after the rehearsal date
- [X] T033 [P] Update `tests/StageFright.Reports.Tests/AttendanceRollPdfRendererTests.cs` test data construction for the new `AttendanceRollMember`/`AttendanceRollData` shape (no `AnnualFeePaid`, add `Attended`/`RehearsalFeePaid`/`AttendanceFeeAmount`)
- [X] T034 [P] Rework the `V3_RehearsalAttendanceTests.cs` integration test that seeded Annual-Fee GL data into one that seeds a real `AttendanceRecord` and a real per-rehearsal `Attendance`-type `Fee` (paid and marked-unpaid cases) and asserts `Attended`/`RehearsalFeePaid` end-to-end
- [X] T035 Run `dotnet build` and the full `dotnet test` suite from the repo root; fix any failures surfaced by T027-T034
- [X] T036 Update the success-criteria list in spec.md (already rewritten in T026) to match verification performed in T035
- [X] T037 Fix `src/StageFright.Data/Repositories/MemberRepository.cs`'s `GetActiveAsOfAsync`: a real integration-test failure (T035) surfaced that the existing query only matched members whose *current* status is Active, silently excluding a member who was active as of the given date but has since gone inactive — corrected to `(Status=Active AND ActivateDate <= date) OR (Status=Inactive AND ActivateDate <= date AND InactivateDate > date)`; see research.md Decision 8's correction note
- [X] T038 [P] Add `GetActiveAsOfAsync_ReturnsMember_WhenInactivatedAfterDate` to `tests/StageFright.Data.Tests/Repositories/MemberRepositoryIntegrationTests.cs` covering the branch fixed in T037; confirmed all four pre-existing `GetActiveAsOfAsync` tests still pass unchanged

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- This feature is read-only end-to-end (spec Assumptions) — no migration, no entity/schema change, no test needs to verify rollback of a write, only that no write occurred (T023)
- T013's temp-file-write-then-`Process.Start` pattern is deliberately **not** unit/component-tested beyond the empty-state and error paths, matching the existing precedent in `ReportViewerTests.cs` for `PrintReport()`/`ExportCsv()` — the happy path is covered by T006/T007/T009 (data/render correctness) plus T023 (manual end-to-end run)
- Commit after each task or logical group, per this repository's CLAUDE.md workflow (stage everything, commit with a descriptive message)
- Run `dotnet build` and `dotnet test` before considering any phase checkpoint met
