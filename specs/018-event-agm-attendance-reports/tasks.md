# Tasks: Print Reports for Event and AGM Attendance

**Input**: Design documents from `/specs/018-event-agm-attendance-reports/` (plan.md, spec.md, research.md, data-model.md, contracts/event-agm-attendance-sheet-contract.md)

**Tests**: Included throughout — CLAUDE.md's "Exhaustive code-path test coverage" rule is project-wide and non-negotiable. Each story's `### Tests` block is written first, deliberately failing to compile/pass until its `### Implementation` block lands, matching this repo's spec 012 precedent.

**Organization**: Grouped by user story per spec.md's own priority order (US1 P1 → US2 P2 → US3 P3). Unlike spec 012 (which built a single-column layout in US1 and only introduced two-column rendering later, in US3), this feature's design (research.md Decision 3) puts the shared two-column/checkbox-cell layout engine (`CheckboxSheetPdfBuilder`) in the Foundational phase, since it is a pure function of primitive parameters with no dependency on either domain's DTOs — both US1's and US2's renderers call it directly from their first implementation, so both sheets are two-column-correct from the moment each story ships. US3 therefore adds no new production code path; it verifies (via pagination-boundary tests and a manual visual check) that the shared implementation actually delivers the identical layout FR-007/FR-008 requires of both sheets — exactly the framing spec.md's own US3 "Why this priority" text gives ("both sheets are already functional and printable without it").

## Format: `[ID] [P?] [Story] Description · file`

- **[P]**: Independent of the other tasks in its wave — different file, no incomplete dependency — buildable in any order (or in parallel).
- **[US#]**: Maps to spec.md's US1–US3.
- A **wave** groups tasks that can be built in any order; **⟶** join lines mark a hard wait for the previous wave.

---

## Phase 1: Setup

- [x] **T001** Confirm baseline: `dotnet build` and `dotnet test` (no `--no-build`) are green on branch `018-event-agm-attendance-reports` before any change, per CLAUDE.md's Build & Test Verification rule.

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: No user story can begin until this phase is complete — both new renderers (US1, US2) delegate their entire page-composition to this one shared helper, so it must exist first.

**Wave 1 (single task):**

- [x] **T002** Create `CheckboxSheetPdfBuilder` (internal static) · `src/StageFright.Reports/Rendering/CheckboxSheetPdfBuilder.cs` — `internal static byte[] Build(string organizationName, string title, string dateLine, string checkboxColumnHeader, IReadOnlyList<(string LastName, string FirstName, bool Checked)> rows)` per data-model.md: A4/18pt-margin page, `RowsPerColumn = 32` (research.md Decision 6, same tuning as `AttendanceRollPdfRenderer`), two side-by-side single-checkbox-column tables per page (wide Name column + one minimal-width checkbox column headed by `checkboxColumnHeader`, FR-007), bordered-box-plus-"✓"-glyph checkbox cells (never a solid fill, per CLAUDE.md), surnames rendered in capitals (FR-006), wrapping column headings, "Page X of Y" footer, header showing `organizationName`/`title`/`dateLine` with **no** "Generated: <timestamp>" line (FR-008). `AttendanceRollPdfRenderer` is untouched. Not directly unit-tested (it's `internal`, not published) — exercised via `EventAttendanceSheetPdfRendererTests`/`AgmAttendanceSheetPdfRendererTests` in US1/US2/US3 below.

**Checkpoint**: The shared layout engine exists and compiles. Both attendance-sheet stories can now build their renderer on top of it.

---

## Phase 3: User Story 1 - Print an event's attendance sheet (Priority: P1) 🎯 MVP

**Goal**: A "Print" action on the Events list and detail page generates a two-column PDF listing every member active as of the event's date, sorted by surname then first name, each with a "Participated" checkbox that prints blank until participation is recorded and matches the real value afterward; an empty active-member list shows an inline message instead of a blank PDF.

**Independent Test**: Print the sheet for an event whose participation hasn't been recorded yet and confirm every eligible member appears with a blank checkbox; record participation and reprint the same event to confirm the checkboxes now show the real recorded result.

### Tests for User Story 1

**Wave 1 — independent (different files):**

- [x] **T003** [P] [US1] New `tests/StageFright.Core.Tests/Modules/Events/EventAttendanceSheetServiceTests.cs` (NSubstitute mocks for `IEventRepository`/`IMemberRepository`, following `AttendanceRollServiceTests.cs`'s convention): `GenerateAsync` throws `EntityNotFoundException("Event", ...)` for an unknown `eventId`; returns exactly the members `IMemberRepository.GetActiveAsOfAsync(evt.Date)` returns (archived/soft-deleted members the mock never returns don't appear, FR-002/Acceptance Scenario 3); orders by `LastName` then `FirstName`, including a same-surname pair for the first-name sub-sort (FR-006); `Participated` is `false` for a member with no matching `ParticipationRecord` and matches the record's `Participated` value when one exists (FR-003, Edge Cases); returns an empty `Members` list (not a throw) when no active members exist (FR-009's precondition, research.md Decision 7); copies `EventDate`/`EventTypeName` from the looked-up `Event`.
- [x] **T004** [P] [US1] New `tests/StageFright.Reports.Tests/EventAttendanceSheetPdfRendererTests.cs` (mirrors `AttendanceRollPdfRendererTests.cs`'s non-null/non-empty/no-throw convention): `Render` returns a non-empty `byte[]` for a populated roster; returns a non-empty `byte[]` (no throw) for a zero-member `EventAttendanceSheetData`; does not throw when `organizationName` is empty.
- [x] **T005** [P] [US1] Extend `tests/StageFright.UI.Tests/Pages/Events/EventListTests.cs`: a "Print" button renders in the Actions column for every event row; clicking it when `IEventAttendanceSheetService.GenerateAsync` returns an empty `Members` list shows an inline message and `IEventAttendanceSheetPdfRenderer.Render` is never called (`DidNotReceive()`); clicking it when `GenerateAsync` throws shows a generic error message and `Render` is never called — no click-through-to-success test (no seam to intercept `File.WriteAllBytes`/`Process.Start`, matching `RehearsalListTests.cs`'s existing precedent for `PrintRoll`).
- [x] **T006** [P] [US1] New `tests/StageFright.UI.Tests/Pages/Events/EventDetailTests.cs` (page currently has no test file): renders event details for a valid `Id`; shows "Event not found." for an unknown `Id`; a "Print Attendance Sheet" button renders once the event loads; clicking it when `GenerateAsync` returns an empty roster shows an inline message and `Render` is never called, mirroring T005.
- [x] **T007** [P] [US1] Extend `tests/StageFright.Integration.Tests/Scenarios/V5_EventsParticipationTests.cs` with an `EventAttendanceSheetService` built against the real SQLite in-memory `StageFrightDbContext`: `GenerateAsync` returns only members active as of the event's date sorted by surname/first name, excludes a soft-deleted member, reflects `true` after participation is recorded for a member and `false` before, returns an empty list for an event with zero active members, and throws `EntityNotFoundException` for an unknown `eventId`.

### Implementation for User Story 1

**Wave 1 — independent (different files):**

- [x] **T008** [P] [US1] Create `EventAttendanceSheetData.cs` · `src/StageFright.Core/Modules/Events/EventAttendanceSheetData.cs` — sealed class, `DateTime EventDate`, `string EventTypeName = string.Empty`, `IReadOnlyList<EventAttendanceSheetMember> Members = Array.Empty<...>()`, per data-model.md.
- [x] **T009** [P] [US1] Create `EventAttendanceSheetMember.cs` · `src/StageFright.Core/Modules/Events/EventAttendanceSheetMember.cs` — sealed class, `string FirstName`, `string LastName`, `bool Participated` — no `MemberId`, per data-model.md.
- [x] **T010** [P] [US1] Create `IEventAttendanceSheetService.cs` · `src/StageFright.Core/Contracts/IEventAttendanceSheetService.cs` — `Task<EventAttendanceSheetData> GenerateAsync(Guid eventId, CancellationToken ct = default)`, XML doc noting the `EntityNotFoundException("Event", ...)` failure mode, per contracts/event-agm-attendance-sheet-contract.md.
- [x] **T011** [P] [US1] Create `IEventAttendanceSheetPdfRenderer.cs` · `src/StageFright.Reports/Rendering/IEventAttendanceSheetPdfRenderer.cs` — `byte[] Render(EventAttendanceSheetData data, string organizationName = "")`, XML doc matching the contract's postconditions (non-empty output even for a zero-member sheet; pure function).

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — independent (different files):**

- [x] **T012** [P] [US1] Create `EventAttendanceSheetService.cs` · `src/StageFright.Core/Modules/Events/EventAttendanceSheetService.cs` implementing `IEventAttendanceSheetService`: constructor-inject `IEventRepository`, `IMemberRepository`; `GenerateAsync` looks up the event via `_eventRepo.GetByIdWithDetailsAsync(eventId, ct) ?? throw new EntityNotFoundException("Event", eventId, nameof(GenerateAsync))`, fetches `_memberRepo.GetActiveAsOfAsync(evt.Date, ct)` ordered by `LastName` then `FirstName`, builds a `MemberId → Participated` lookup from `evt.ParticipationRecords`, maps each active member to an `EventAttendanceSheetMember`, and returns the `EventAttendanceSheetData` — exact shape per data-model.md's implementation sketch. Depends on T008, T009, T010.
- [x] **T013** [P] [US1] Create `EventAttendanceSheetPdfRenderer.cs` · `src/StageFright.Reports/Rendering/EventAttendanceSheetPdfRenderer.cs` implementing `IEventAttendanceSheetPdfRenderer`: `Render` calls `CheckboxSheetPdfBuilder.Build` with `title = "Event Attendance Sheet"`, `dateLine = $"{data.EventTypeName}: {data.EventDate:d MMMM yyyy}"`, `checkboxColumnHeader = "Participated"`, rows mapped from `data.Members` as `(LastName, FirstName, Participated)`, per data-model.md. Depends on T002, T008, T011.

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — independent (different files):**

- [x] **T014** [P] [US1] Add a "Print" button to the Actions column `<Template>` in `src/StageFright.UI/Pages/Events/EventList.razor` (alongside the existing "Record Participation"/"Recorded" content, `aria-label` following the existing "for @e.Date..." convention) + an inline message element below the grid; implement `PrintAttendanceSheet(Guid eventId)` in `src/StageFright.UI/Pages/Events/EventList.razor.cs`: inject `IEventAttendanceSheetService`, `IEventAttendanceSheetPdfRenderer`, `ISettingsService`; call `GenerateAsync`; if `Members.Count == 0` set the message and return without rendering (FR-009); otherwise fetch `SettingsService.GetAsync()` for `OrganizationName`, call `Render`, write to a temp file, and launch it via `Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })` — exact pattern from `RehearsalList.razor(.cs)`'s `PrintRoll`. Depends on T012, T013.
- [x] **T015** [P] [US1] Add a "Print Attendance Sheet" button to `src/StageFright.UI/Pages/Events/EventDetail.razor` (near the existing "Back to Events" button) + an inline message element; implement `PrintAttendanceSheet()` in `src/StageFright.UI/Pages/Events/EventDetail.razor.cs`, same generate → empty-state check → render → temp-file → `Process.Start` shape as T014, operating on `Id`. Depends on T012, T013.
- [x] **T016** [P] [US1] Register `IEventAttendanceSheetService`/`EventAttendanceSheetService` (near `IEventService`) and `IEventAttendanceSheetPdfRenderer`/`EventAttendanceSheetPdfRenderer` (near `IAttendanceRollPdfRenderer`) as `AddScoped` in `src/StageFright.App/MauiProgram.cs`'s `RegisterCoreServices`. Depends on T012, T013.

**Checkpoint**: User Story 1 is fully functional and independently testable — an event's attendance sheet can be printed from either the Events list or its detail page, with correct active-as-of-date membership, real/blank Participated checkboxes, and a proper empty-state message.

---

## Phase 4: User Story 2 - Print a past AGM's attendance report (Priority: P2)

**Goal**: A "Print" action on the Past AGMs list and detail page generates a two-column PDF listing every member on the AGM's fixed, already-persisted attendance roster, sorted by surname then first name, with a checkbox matching each member's real recorded attended/absent status; an AGM recorded with an empty roster shows an inline message instead of a blank PDF.

**Independent Test**: Record an AGM with a mix of attended and absent members, print its attendance report, and confirm the checkboxes match exactly what was recorded.

### Tests for User Story 2

**Wave 1 — independent (different files):**

- [x] **T017** [P] [US2] New `tests/StageFright.Core.Tests/Modules/Agm/AgmAttendanceSheetServiceTests.cs` (NSubstitute mocks for `IAgmRepository`/`IAgmAttendanceRepository`): `GenerateAsync` throws `EntityNotFoundException("AnnualGeneralMeeting", ...)` for an unknown `agmId`; returns exactly the roster `IAgmAttendanceRepository.GetByAgmAsync` returns, with each `Attended` value copied unchanged (FR-005); returns an empty `Members` list (not a throw) when the AGM's roster is empty (FR-009's precondition); copies `AgmDate` from the looked-up AGM.
- [x] **T018** [P] [US2] New `tests/StageFright.Reports.Tests/AgmAttendanceSheetPdfRendererTests.cs`: `Render` returns a non-empty `byte[]` for a populated roster (mixed attended/absent); returns a non-empty `byte[]` (no throw) for a zero-member `AgmAttendanceSheetData`; does not throw when `organizationName` is empty.
- [x] **T019** [P] [US2] Extend `tests/StageFright.UI.Tests/Pages/Events/AgmListTests.cs`: a "Print" button renders per AGM row; clicking it when `IAgmAttendanceSheetService.GenerateAsync` returns an empty `Members` list shows an inline message and `IAgmAttendanceSheetPdfRenderer.Render` is never called; clicking it when `GenerateAsync` throws shows a generic error message and `Render` is never called.
- [x] **T020** [P] [US2] Extend `tests/StageFright.UI.Tests/Pages/Events/AgmDetailTests.cs`: a "Print Attendance Report" button renders once the AGM loads; same empty-state/error-message coverage as T019.
- [x] **T021** [P] [US2] Extend `tests/StageFright.Integration.Tests/Scenarios/V18_AgmWorkflowTests.cs` with an `AgmAttendanceSheetService` built against the real SQLite in-memory `StageFrightDbContext`: `GenerateAsync` returns the AGM's real persisted roster sorted by surname/first name with correct attended/absent values, returns an empty list for an AGM recorded with zero attendance records, and throws `EntityNotFoundException` for an unknown `agmId`.

### Implementation for User Story 2

**Wave 1 — independent (different files):**

- [x] **T022** [P] [US2] Create `AgmAttendanceSheetData.cs` · `src/StageFright.Core/Modules/Agm/AgmAttendanceSheetData.cs` — sealed class, `DateTime AgmDate`, `IReadOnlyList<AgmAttendanceSheetMember> Members = Array.Empty<...>()`, per data-model.md.
- [x] **T023** [P] [US2] Create `AgmAttendanceSheetMember.cs` · `src/StageFright.Core/Modules/Agm/AgmAttendanceSheetMember.cs` — sealed class, `string FirstName`, `string LastName`, `bool Attended`, per data-model.md.
- [x] **T024** [P] [US2] Create `IAgmAttendanceSheetService.cs` · `src/StageFright.Core/Contracts/IAgmAttendanceSheetService.cs` — `Task<AgmAttendanceSheetData> GenerateAsync(Guid agmId, CancellationToken ct = default)`, XML doc noting the `EntityNotFoundException("AnnualGeneralMeeting", ...)` failure mode, per contracts/event-agm-attendance-sheet-contract.md.
- [x] **T025** [P] [US2] Create `IAgmAttendanceSheetPdfRenderer.cs` · `src/StageFright.Reports/Rendering/IAgmAttendanceSheetPdfRenderer.cs` — `byte[] Render(AgmAttendanceSheetData data, string organizationName = "")`, XML doc matching the contract's postconditions.

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — independent (different files):**

- [x] **T026** [P] [US2] Create `AgmAttendanceSheetService.cs` · `src/StageFright.Core/Modules/Agm/AgmAttendanceSheetService.cs` implementing `IAgmAttendanceSheetService`: constructor-inject `IAgmRepository`, `IAgmAttendanceRepository`; `GenerateAsync` looks up the AGM via `_agmRepo.GetByIdAsync(agmId, ct) ?? throw new EntityNotFoundException("AnnualGeneralMeeting", agmId, nameof(GenerateAsync))`, fetches `_attendanceRepo.GetByAgmAsync(agmId, ct)` (already `Member`-eager-loaded and surname/first-name ordered), maps each record to an `AgmAttendanceSheetMember`, and returns the `AgmAttendanceSheetData` — per data-model.md's implementation sketch. Depends on T022, T023, T024.
- [x] **T027** [P] [US2] Create `AgmAttendanceSheetPdfRenderer.cs` · `src/StageFright.Reports/Rendering/AgmAttendanceSheetPdfRenderer.cs` implementing `IAgmAttendanceSheetPdfRenderer`: `Render` calls `CheckboxSheetPdfBuilder.Build` with `title = "AGM Attendance Report"`, `dateLine = $"Annual General Meeting: {data.AgmDate:d MMMM yyyy}"`, `checkboxColumnHeader = "Attended"`, rows mapped from `data.Members` as `(LastName, FirstName, Attended)`, per data-model.md. Depends on T002, T022, T025.

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — independent (different files):**

- [x] **T028** [P] [US2] Add a "Print" button to the Actions column in `src/StageFright.UI/Pages/Events/AgmList.razor` (a new `Sortable="false"` Actions column, since the grid currently has none) + an inline message element; implement `PrintAttendanceReport(Guid agmId)` in `src/StageFright.UI/Pages/Events/AgmList.razor.cs`: inject `IAgmAttendanceSheetService`, `IAgmAttendanceSheetPdfRenderer`, `ISettingsService`; same generate → empty-state check → render → temp-file → `Process.Start` shape as T014. Depends on T026, T027.
- [x] **T029** [P] [US2] Add a "Print Attendance Report" button to `src/StageFright.UI/Pages/Events/AgmDetail.razor` (near the existing "Archive"/"Back to Past AGMs" buttons) + an inline message element; implement `PrintAttendanceReport()` in `src/StageFright.UI/Pages/Events/AgmDetail.razor.cs`, same shape as T015, operating on `Id`. Depends on T026, T027.
- [x] **T030** [P] [US2] Register `IAgmAttendanceSheetService`/`AgmAttendanceSheetService` (near `IAgmService`) and `IAgmAttendanceSheetPdfRenderer`/`AgmAttendanceSheetPdfRenderer` (near `IAttendanceRollPdfRenderer`) as `AddScoped` in `src/StageFright.App/MauiProgram.cs`'s `RegisterCoreServices`. Depends on T026, T027.

**Checkpoint**: User Stories 1 AND 2 both work independently — a past AGM's attendance report can be printed from either the Past AGMs list or its detail page, reflecting the fixed roster's real attended/absent status.

---

## Phase 5: User Story 3 - Consistent, print-ready layout for both sheets (Priority: P3)

**Goal**: Confirm — not build anew — that the event sheet and AGM sheet render with genuinely identical two-column behavior, checkbox-column width, wrapping headings, and surname capitalization, since both already delegate to the one shared `CheckboxSheetPdfBuilder` from T002.

**Independent Test**: Generate an event sheet and an AGM sheet each with enough members to overflow a single column, and confirm both use the two-column layout, narrow checkbox columns, wrapping column headings, and capitalized surnames.

### Tests for User Story 3

**Wave 1 — independent (different files):**

- [x] **T031** [P] [US3] Extend `tests/StageFright.Reports.Tests/EventAttendanceSheetPdfRendererTests.cs` with pagination-boundary cases around `CheckboxSheetPdfBuilder`'s `RowsPerColumn` constant — exactly `RowsPerColumn` members (fills column one, no column two), `RowsPerColumn + 1` (spills into column two, same page), and `2 * RowsPerColumn + 1` (spills onto a second page) — each asserted as a non-empty `byte[]` with no thrown exception.
- [x] **T032** [P] [US3] Extend `tests/StageFright.Reports.Tests/AgmAttendanceSheetPdfRendererTests.cs` with the identical three pagination-boundary cases as T031, confirming the AGM renderer hits the same boundaries at the same member counts (proof the two renderers share one layout engine, not two that could drift).

### Implementation for User Story 3

**Wave 1 (single task):**

- [x] **T033** [US3] Manually verify (per research.md Decision 6's outstanding risk — QuestPDF's rendered row height isn't fully reproducible from unit tests alone) that `CheckboxSheetPdfBuilder`'s `RowsPerColumn`/column-width ratio produce a visually correct printed page for **both** sheets: run the app, generate an event sheet and an AGM sheet each with enough members to overflow one column, and open both PDFs to confirm (a) column one fills completely before column two continues on both, (b) each continues onto additional pages with repeated headers if the roster is larger still, (c) the checkbox column is visibly narrower than the Name column on both, (d) the column heading wraps rather than being cut off or widening the column on both, (e) every surname renders in capital letters on both, and (f) each header shows organization name + title + date with no "Generated:" line (FR-008). Adjust `CheckboxSheetPdfBuilder` (T002) and re-run T031/T032 if any check fails.

**Checkpoint**: All three user stories are independently functional — both sheets are full-featured, print-ready, and visually identical in layout.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Wave 1 — independent:**

- [x] **T034** [P] Run `dotnet build` and the full `dotnet test` suite (all five test projects, no `--no-build`) from the repo root and confirm everything is green, per CLAUDE.md's Build & Test Verification rule.
- [x] **T035** [P] Walk every Acceptance Scenario in spec.md (US1's 4, US2's 2, US3's 3) plus the Edge Cases against a running `dotnet run --project src/StageFright.App/` instance, including the future-dated "AGM"-type Event printed through the event sheet (spec Assumptions); confirm FR-010's read-only guarantee — no `Event`, `AnnualGeneralMeeting`, `ParticipationRecord`, `AgmAttendanceRecord`, `Member`, `Fee`, `Payment`, or `Transaction` record is created, changed, or removed by printing either sheet.
- [x] **T036** [P] Extend CLAUDE.md's "Reports pipeline" paragraph to name `EventAttendanceSheetPdfRenderer` and `AgmAttendanceSheetPdfRenderer` alongside `AttendanceRollPdfRenderer` as the QuestPDF-rendered checkbox-style-cell renderers that share the bordered-box-plus-"✓"-glyph convention, per this repo's CLAUDE.md "Spec & Docs Workflow" rule (keep project documentation from going stale).

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2:**

- [x] **T037** Tick off the Success Criteria checklist (SC-001 through SC-006) in spec.md based on the verification performed in T034/T035.

---

## Dependencies & Execution Order

- **Setup (Phase 1, T001)** → **Foundational (Phase 2, T002)**: `CheckboxSheetPdfBuilder` is a single blocking task — both US1's and US2's renderers call it directly.
- **Foundational → US1 (Phase 3, T003–T016)**: Tests wave (T003–T007) is 5 independent new/extended test files, written first and expected to fail to compile until Implementation lands. Implementation Wave 1 (T008–T011) is 4 independent new files (2 DTOs + 2 interfaces); Wave 2 (T012–T013) is the service + renderer, each depending on Wave 1's files plus (for the renderer) T002; Wave 3 (T014–T016) is UI wiring on two different pages plus DI registration, each depending on Wave 2's concrete classes.
- **Foundational → US2 (Phase 4, T017–T030)**: Same shape as US1, independent of it — different modules (`Agm` vs `Events`), different pages (`AgmList`/`AgmDetail` vs `EventList`/`EventDetail`). Its only shared-file touch is `MauiProgram.cs` (T030 edits the same file T016 did, in the following phase — sequential, not parallel, across phases).
- **US1 + US2 → US3 (Phase 5, T031–T033)**: Needs both renderers to exist to prove they share pagination behavior; Tests wave (T031–T032) is 2 independent test-file extensions; Implementation Wave 1 (T033) is a single manual verification task depending on both.
- **All stories → Polish (Phase 6, T034–T037)**: Wave 1 (T034–T036) is 3 independent activities (automated verification, manual walkthrough, doc update); Wave 2 (T037) depends on Wave 1's results.
