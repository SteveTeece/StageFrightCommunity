# Tasks: AGMs on the All Events List

**Input**: Design documents from `specs/023-merge-agms-events/` (plan.md, spec.md, research.md, data-model.md, contracts/combined-event-list.md)

**Tests**: Constitution/CLAUDE.md's "Exhaustive code-path test coverage" rule is project-wide and non-negotiable. Each phase's `### Tests` block is written first, deliberately failing until its `### Implementation` block lands, matching this repo's spec 019/022 precedent.

**Organization**: Grouped by user story per spec.md's own priority order (US1 P1 → US2 P2 → US3 P3). All three stories read and write `EventList.razor`/`EventList.razor.cs` — those files are edited once per phase, sequentially across phases (never in the same wave).

## Format: `[ID] [P?] [Story] Description · file`

- **[P]**: Independent of the other tasks in its wave — different file, no incomplete dependency — buildable in any order (or in parallel).
- **[US#]**: Maps to spec.md's US1–US3.
- A **wave** groups tasks that can be built in any order; **⟶** join lines mark a hard wait for the previous wave.

---

## Phase 1: Setup

- [x] **T001** Confirm baseline: `dotnet build` and the full `dotnet test` suite (no `--no-build`) are green on branch `023-merge-agms-events` before any change, per CLAUDE.md's Build & Test Verification rule.

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: No user story can begin until this phase is complete — `ICombinedEventListService` is the single merge/sort/route-mapping seam every story's UI work builds on, and FR-006's AGM-vs-event routing must be unit-tested at the Core level before any Razor page depends on it.

### Tests

**Wave 1 — single task:**

- [x] **T002** New `CombinedEventListServiceTests`: merges `IEventService.GetAllAsync()` + `IAgmService.GetPastAsync()` results into one list (FR-001); sorts the merged list by `Date` descending (FR-002); maps an `Event` row with `TypeName = EventType.Name`, `ParticipationRate = StoredParticipationRate`, `IsAgmRecorded = null`, `DetailUrl = "/events/{id}"`; maps an `AnnualGeneralMeeting` row with the fixed literal `TypeName = "Annual General Meeting"` (FR-004), `ParticipationRate = null`, `IsAgmRecorded = IsRecorded`, `DetailUrl = "/events/agm/{id}"` — asserted literally, and asserted **never** equal to `/events/{id}` for an AGM row (FR-006's safety-critical routing); returns an empty list when both sources are empty · `tests/StageFright.Core.Tests/Modules/Events/CombinedEventListServiceTests.cs` (NEW)

### Implementation

**Wave 1 — independent (different files):**

- [x] **T003** [P] Create `CombinedEventListItemKind` enum (`Event`, `Agm`) per data-model.md · `src/StageFright.Core/Modules/Events/CombinedEventListItemKind.cs` (NEW)
- [x] **T004** [P] Create `CombinedEventListItem` DTO with fields `Id`, `Date`, `Notes`, `TypeName`, `Kind`, `ParticipationRate`, `IsAgmRecorded`, `DetailUrl` per data-model.md's field table · `src/StageFright.Core/Modules/Events/CombinedEventListItem.cs` (NEW)
- [x] **T005** [P] Create `ICombinedEventListService` interface: `Task<IReadOnlyList<CombinedEventListItem>> GetAllAsync(CancellationToken ct = default)` per contracts/combined-event-list.md · `src/StageFright.Core/Contracts/ICombinedEventListService.cs` (NEW)

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — single task:**

- [x] **T006** Implement `CombinedEventListService`: constructor takes `IEventService` + `IAgmService` only (never the concrete `AgmService`/`EventService`, per constitution §4.1); `GetAllAsync` calls both, maps each source record per T002's rules, concatenates, and orders by `Date` descending · `src/StageFright.Core/Modules/Events/CombinedEventListService.cs` (NEW). Depends on T003, T004, T005.

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — single task:**

- [x] **T007** Register `services.AddScoped<ICombinedEventListService, CombinedEventListService>();` in the "Events module" section, alongside the existing `IEventService`/`IEventAttendanceSheetService` registrations · `src/StageFright.App/MauiProgram.cs`. Depends on T005, T006.

**Checkpoint**: `ICombinedEventListService` merges, sorts, and routes correctly (FR-001, FR-002, FR-004, FR-006), fully unit-tested and DI-registered. No UI change yet — `EventList` still reads `IEventService` directly.

---

## Phase 3: User Story 1 - See AGMs in the All Events list (Priority: P1) 🎯 MVP

**Goal**: The All Events screen renders both AGM and Event rows in one date-sorted grid, using the same Date/Notes/Type columns and the existing empty state.

**Independent Test**: Seed one recorded AGM and one Event with different dates, open the All Events screen, and confirm the AGM appears as a row in the same list as the Event, ordered by date. Verifiable without Stories 2 or 3.

### Tests

**Wave 1 — single task:**

- [x] **T008** [US1] Rewrite `EventListTests`: replace the `IEventService` substitute/DI registration with an `ICombinedEventListService` substitute seeded with `CombinedEventListItem` rows of both `Kind.Event` and `Kind.Agm`; update every existing test's setup call to the new seam; add cases asserting an AGM row and an Event row on different dates both render in the same grid ordered by `Date` descending (Acceptance Scenario 1), a scheduled-but-unrecorded AGM still appears (Acceptance Scenario 2), and the existing "No events scheduled yet" message still shows when the combined list is empty (Acceptance Scenario 3) · `tests/StageFright.UI.Tests/Pages/Events/EventListTests.cs`

### Implementation

**Wave 1 — single task:**

- [x] **T009** [US1] Rewrite `EventList.razor` + `EventList.razor.cs`: inject `ICombinedEventListService` in place of `IEventService`; change the grid's backing list/property from `List<Event>`/`DisplayEvents` to `List<CombinedEventListItem>`/`DisplayItems` (dropping the component's own `.OrderByDescending` — the service already returns sorted); `RadzenDataGrid TItem` becomes `CombinedEventListItem`; Date column's link href becomes `@item.DetailUrl` (drops the hardcoded `/events/@e.Id`); Event Type column reads `@item.TypeName` directly (drops the `EventType?.Name ?? "—"` null-coalesce, since `TypeName` is always populated) · `src/StageFright.UI/Pages/Events/EventList.razor` + `.razor.cs`. Depends on T003–T007.

**Checkpoint**: User Story 1 is independently functional and testable — AGM and Event rows both render in the combined, date-sorted grid with the correct empty state, and each row's Date link already routes correctly (FR-006 comes for free from `DetailUrl`). The Status/Actions columns still show Event-style content for AGM rows (fixed in Story 2) but nothing crashes or misroutes.

---

## Phase 4: User Story 2 - Tell AGM rows apart and open the right screen (Priority: P2)

**Goal**: An AGM row's Status column shows a Recorded/Scheduled badge instead of a participation rate, and its Actions column offers "Record"/"Print" (the AGM's own attendance pipeline) instead of "Record Participation".

**Independent Test**: With an AGM present in the combined list (from Story 1), select its row and confirm it opens the AGM's dedicated screen; confirm the row's type and status text read distinctly from an ordinary event's.

### Tests

**Wave 1 — single task:**

- [x] **T010** [US2] Extend `EventListTests`: an AGM row with `IsAgmRecorded = true` renders a `"Recorded"` badge (`bg-success`), never a participation percentage (Acceptance Scenario 2); `IsAgmRecorded = false` renders a `"Scheduled"` badge (`bg-warning text-dark`) and a `"Record"` action linking to `/events/agm/{id}/record` (Acceptance Scenario 3); clicking an AGM row's Print button calls `IAgmAttendanceSheetService.GenerateAsync` + `IAgmAttendanceSheetPdfRenderer.Render` (not the Event pipeline); an explicit regression guard asserting an Event row's Date link href is `/events/{id}` and an AGM row's is `/events/agm/{id}` — never `/events/{id}` (FR-006, Acceptance Scenario 1) · `tests/StageFright.UI.Tests/Pages/Events/EventListTests.cs`

### Implementation

**Wave 1 — single task:**

- [x] **T011** [US2] Extend `EventList.razor.cs`: inject `IAgmAttendanceSheetService` + `IAgmAttendanceSheetPdfRenderer`; add `PrintAgmAttendanceReport(Guid agmId)` mirroring `AgmList.razor.cs`'s `PrintAttendanceReport` (temp-file render + `Process.Start`), keeping the existing `PrintAttendanceSheet(eventId)` for Event rows. Extend `EventList.razor`'s Status and Actions columns with `@if (item.Kind == CombinedEventListItemKind.Agm) { ... } else { ... }` branches: Status renders the Recorded/Scheduled badge markup carried over from `AgmList.razor` for AGM rows, the existing `%`/"Not recorded" markup for Event rows; Actions renders `"Record"` → `/events/agm/{id}/record` + Print (via `PrintAgmAttendanceReport`) for AGM rows, the existing `"Record Participation"`/`"Recorded"` + Print (via `PrintAttendanceSheet`) for Event rows · `src/StageFright.UI/Pages/Events/EventList.razor` + `.razor.cs`. Depends on T009.

**Checkpoint**: User Story 2 is independently functional and testable — an AGM row's status and actions read distinctly from an event row's, its Print button uses the AGM attendance pipeline, and selecting it opens the AGM's own detail screen, never the generic event detail screen.

---

## Phase 5: User Story 3 - Search the combined list (Priority: P3)

**Goal**: The existing search box matches AGM rows on the same date/type/notes fields it already searches for events.

**Independent Test**: With an AGM and a non-AGM event both present, type a search term that matches only the AGM's date or notes and confirm only the AGM row remains; type a term matching only the other event and confirm the AGM row is filtered out.

### Tests

**Wave 1 — single task:**

- [x] **T012** [US3] Extend `EventListTests`: typing a term matching only the AGM's formatted date leaves only the AGM row (Acceptance Scenario 1); typing `"annual general meeting"` or a partial match leaves AGM rows and filters out non-matching Event rows (Acceptance Scenario 2); typing a term matching only the AGM's notes leaves only that row (Acceptance Scenario 3); typing a term matching neither an AGM nor an Event shows the existing `No events match "<term>"` message applied to the combined set (Edge Case, FR-009) · `tests/StageFright.UI.Tests/Pages/Events/EventListTests.cs`

### Implementation

**Wave 1 — single task:**

- [x] **T013** [US3] Verify `DisplayItems`' search predicate — already generalized to `item.Date`/`item.TypeName`/`item.Notes` by T009's type swap — satisfies every T012 case with no further production change; extend only if T012 finds a genuine gap (none expected, since `TypeName` already carries the literal `"Annual General Meeting"` for AGM rows) · `src/StageFright.UI/Pages/Events/EventList.razor.cs`. Depends on T009, T012.

**Checkpoint**: User Story 3 is independently functional and testable — the search box finds AGM rows by date, type, or notes exactly as it already does for events, and the "no matches" message covers the combined set.

---

## Final Phase: Polish

**Wave 1 — single task:**

- [x] **T014** New `V20_CombinedEventsListTests` integration test against a real SQLite in-memory database with full EF migrations (next free `Vn` slot after `V19_AgmSchedulingTests`): seed one `Event` and one `AnnualGeneralMeeting`, confirm `ICombinedEventListService.GetAllAsync()` returns both; assert the AGM row's `DetailUrl` is `/events/agm/{id}`, not `/events/{id}` (FR-006); archive the AGM (soft-delete) and confirm it disappears from the combined result (FR-010) · `tests/StageFright.Integration.Tests/Scenarios/V20_CombinedEventsListTests.cs` (NEW)

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — single task:**

- [x] **T015** Full rebuild and full test suite run (`dotnet build -t:Rebuild`, then `dotnet test` without `--no-build`) per CLAUDE.md's Build & Test Verification rule; confirm no new warnings and every test green (treat the two documented "fee"-substring flaky tests — `ParticipationGridTests`/`EventFormTests`, unrelated files — per the known-flake note, not as regressions).

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — single task:**

- [x] **T016** Walk every Acceptance Scenario in spec.md (US1's 3, US2's 3, US3's 3) plus the Edge Cases against a running `dotnet run --project src/StageFright.App/` instance; confirm SC-001–SC-004; confirm FR-011 (AGM/Event scheduling, recording, archiving, and committee elections behave exactly as before), FR-012 (`/events/agm`, `/events/agm/new`, AGM detail/record screens remain reachable and unchanged), and FR-013 (the All Events screen's "Schedule Event" button still creates only a generic `Event`, never an AGM).

---

## Dependencies & Execution Order

- **Setup (Phase 1, T001)** → **Foundational (Phase 2, T002–T007)**: the Core merge service's Tests wave (T002) is written first against the not-yet-existing enum/DTO/interface/service; Implementation Wave 1 (T003–T005) is 3 independent new files; Wave 2 (T006) implements the service against Wave 1's contracts; Wave 3 (T007) registers it in DI once the concrete type exists.
- **Foundational → US1 (Phase 3, T008–T009)**: Tests (T008) rewrites `EventListTests`' fixture to the new `ICombinedEventListService` seam and is written to fail until T009 lands. Implementation (T009) rewires `EventList.razor`/`.razor.cs` to the merged DTO — this single edit also incidentally satisfies FR-006's routing and (mechanically) generalizes the search predicate, though those aren't asserted until Stories 2 and 3.
- **Foundational → US2 (Phase 4, T010–T011)**: Depends on US1's `EventList.razor`/`.razor.cs` (T009) already using `CombinedEventListItem` and `item.Kind`. `EventList.razor`/`.razor.cs` (T011) is the *second* edit to files US1 already touched (T009) — sequential, never parallel with it.
- **Foundational → US3 (Phase 5, T012–T013)**: Depends on US1's `DisplayItems` predicate (T009) existing. T013 is a verification step, not new production logic, since the predicate was already generalized by T009.
- **US1 + US2 + US3 → Polish (Phase 6, T014–T016)**: T014 (new end-to-end integration test) needs the full merge/routing/soft-delete behavior to exist; T015 (full build/test) needs T014's new test file written first to include it in the run; T016 (manual walkthrough) needs T015 green before it's a meaningful check.

---

## Requirement Coverage

| Requirement | Tasks |
|---|---|
| FR-001 | T002, T006, T008, T009 |
| FR-002 | T002, T006, T009 |
| FR-003 | T008, T009 |
| FR-004 | T002, T004, T006, T008, T009 |
| FR-005 | T010, T011 |
| FR-006 | T002, T004, T006, T009, T010, T011, T014 |
| FR-007 | T010, T011 |
| FR-008 | T009, T012, T013 |
| FR-009 | T008, T009, T012 |
| FR-010 | T006, T014 |
| FR-011 | T016 |
| FR-012 | T016 |
| FR-013 | T016 |
