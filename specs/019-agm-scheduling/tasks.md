# Tasks: Schedule Future AGMs

**Input**: Design documents from `/specs/019-agm-scheduling/` (plan.md, spec.md, research.md, data-model.md, contracts/agm-scheduling-contract.md)

**Tests**: Included throughout — CLAUDE.md's "Exhaustive code-path test coverage" rule is project-wide and non-negotiable. Each story's `### Tests` block is written first, deliberately failing to compile/pass until its `### Implementation` block lands, matching this repo's spec 012/018 precedent.

**Organization**: Grouped by user story per spec.md's own order (US1 P1 → US2 P1 → US3 P2). US1 and US2 are tied at P1 — spec.md, plan.md, and research.md all consistently frame US1 ("schedule ahead") as the prerequisite build step and US2 ("record against an existing scheduled AGM") as the second, so that listed order is kept as the build sequence. Both stories touch `IAgmService`/`AgmService.cs`, `AgmList.razor(.cs)`, and `AgmDetail.razor(.cs)` — those files are edited once in each phase, sequentially across phases (never in the same wave), per the Dependencies section below.

## Format: `[ID] [P?] [Story] Description · file`

- **[P]**: Independent of the other tasks in its wave — different file, no incomplete dependency — buildable in any order (or in parallel).
- **[US#]**: Maps to spec.md's US1–US3.
- A **wave** groups tasks that can be built in any order; **⟶** join lines mark a hard wait for the previous wave.

---

## Phase 1: Setup

- [ ] **T001** Confirm baseline: `dotnet build` and the full `dotnet test` suite (no `--no-build`) are green on branch `019-agm-scheduling` before any change, per CLAUDE.md's Build & Test Verification rule.

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: No user story can begin until this phase is complete — `IsRecorded` is the one field all three stories read or write (US1 sets it `false`, US2 sets it `true` and guards on it, US3 branches `GenerateAsync` on it).

**Wave 1 — independent (different files):**

- [ ] **T002** [P] Add `public bool IsRecorded { get; set; }` to `AnnualGeneralMeeting`, with the XML doc comment from data-model.md explaining it is set once inside `RecordAsync` and never cleared, tracked directly on the row rather than inferred from attendance rows (Edge Case 5) · `src/StageFright.Core/Entities/AnnualGeneralMeeting.cs`
- [ ] **T003** [P] Add `[ProtoMember(10)] public bool IsRecorded { get; set; }` to `AnnualGeneralMeetingBackupDto` · `src/StageFright.Core/Modules/Settings/Backup/AnnualGeneralMeetingBackupDto.cs`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — independent (different files):**

- [ ] **T004** [P] Generate the EF Core migration: `dotnet ef migrations add AddIsRecordedToAgm --project src/StageFright.Data/ --startup-project src/StageFright.App/`, then hand-edit its `Up()` to add a one-line backfill (`migrationBuilder.Sql("UPDATE AnnualGeneralMeetings SET IsRecorded = 1")`) immediately after the `AddColumn` call, so every pre-existing AGM row — all created under today's always-complete `RecordAsync` — correctly migrates to `IsRecorded = 1` · `src/StageFright.Data/Migrations/<timestamp>_AddIsRecordedToAgm.cs` (+ `.Designer.cs` + `StageFrightDbContextModelSnapshot.cs`, auto-generated). Depends on T002.
- [ ] **T005** [P] Map `IsRecorded` in both directions: `Id = a.Id, ... IsRecorded = a.IsRecorded, ...` in `MapAgm`, and the matching `IsRecorded = d.IsRecorded` in `MapAgmFromDto` · `src/StageFright.Core/Modules/Settings/BackupService.cs`. Depends on T002, T003.

**Checkpoint**: `AnnualGeneralMeeting.IsRecorded` exists, persists, and round-trips through backup/restore. Every story can now build on it.

---

## Phase 3: User Story 1 - Schedule a future AGM ahead of time (Priority: P1) 🎯 MVP

**Goal**: A committee administrator can save an AGM's meeting date and optional notes immediately, without entering attendance or elections, and it appears on the AGM list and its own detail page clearly marked "Scheduled" — with a second AGM in the same calendar year rejected.

**Independent Test**: Schedule an AGM for a future date with no attendance or elections entered, confirm it saves immediately and appears in the AGM list as not-yet-recorded, confirm scheduling alone creates no attendance records, elected positions, or committee term, and confirm attempting to schedule a second AGM with a meeting date in that same calendar year is rejected.

### Tests

**Wave 1 — independent (different files):**

- [ ] **T006** [P] [US1] Add `ScheduleAsync` cases to `AgmServiceTests`: persists `Date`/`Notes`/`IsRecorded = false`; creates zero `AgmAttendanceRecord`/`CommitteePositionRecord`/`CommitteeTerm` rows; wraps the write in one transaction; logs `AuditAction.Create` against the new AGM's id; throws `ValidationException(nameof(AnnualGeneralMeeting), nameof(ScheduleAsync))` and persists nothing when `IAgmRepository.ExistsForYearAsync(request.Date.Year)` returns `true` · `tests/StageFright.Core.Tests/Modules/Agm/AgmServiceTests.cs`
- [ ] **T007** [P] [US1] New `AgmRepositoryTests`: `ExistsForYearAsync` returns `true` when a non-archived AGM's `Date` falls in the given year, `false` when none does, `false` for an archived AGM in that year (proving the global `!IsDeleted` query filter excludes it — this is the mechanism behind FR-015's "archiving frees the year"), and `false` for a different calendar year · `tests/StageFright.Data.Tests/Repositories/AgmRepositoryTests.cs` (NEW)
- [ ] **T008** [P] [US1] New `ScheduleAgmTests` bUnit suite (mirrors `RecordAgmTests.cs`'s NSubstitute/`RadzenGridTestContext` shape): renders a date field and a notes field; clicking Save calls `IAgmService.ScheduleAsync` with the entered `Date`/`Notes`; a thrown `ValidationException` renders its message in an inline `.alert-danger`; a successful save navigates to `/events/agm/{agm.Id}` (research.md Decision 7 — not back to the list) · `tests/StageFright.UI.Tests/Pages/Events/ScheduleAgmTests.cs` (NEW)
- [ ] **T009** [P] [US1] Extend `AgmListTests`: a Status column renders "Scheduled" for `!agm.IsRecorded` and "Recorded" for `agm.IsRecorded`; the page heading assertion becomes "AGMs" (was "Past AGMs"); the empty-state and toolbar button text becomes "Schedule AGM" routing to `/events/agm/new`; the Attendance column shows "—" instead of "0 of 0" for a still-scheduled row · `tests/StageFright.UI.Tests/Pages/Events/AgmListTests.cs`
- [ ] **T010** [P] [US1] Extend `AgmDetailTests`: a scheduled AGM (`IsRecorded = false`) renders only the meeting date and notes plus a "Scheduled" status badge — no attendance-count line and no Elected Positions section; a recorded AGM (`IsRecorded = true`) keeps every existing assertion (attendance count, position lines) unchanged · `tests/StageFright.UI.Tests/Pages/Events/AgmDetailTests.cs`

### Implementation

**Wave 1 — independent (different files):**

- [ ] **T011** [P] [US1] Add `Task<bool> ExistsForYearAsync(int year, CancellationToken ct = default)` to `IAgmRepository`, with the data-model.md XML doc noting archived AGMs are excluded via the entity's existing `!IsDeleted` query filter; also add the one-sentence doc-comment correction to `GetPastOrderedAsync` from research.md Decision 6 ("despite the name, returns every non-deleted AGM including future-dated ones") · `src/StageFright.Core/Contracts/IAgmRepository.cs`
- [ ] **T012** [P] [US1] New `ScheduleAgmRequest` record: `public record ScheduleAgmRequest(DateTime Date, string? Notes);` · `src/StageFright.Core/Modules/Agm/ScheduleAgmRequest.cs` (NEW)
- [ ] **T013** [P] [US1] Add `Task<AnnualGeneralMeeting> ScheduleAsync(ScheduleAgmRequest request, CancellationToken ct = default)` to `IAgmService`, with the XML doc's `ValidationException` failure mode from the contract (a non-archived AGM already exists for `request.Date`'s calendar year) · `src/StageFright.Core/Contracts/IAgmService.cs`
- [ ] **T014** [P] [US1] `AgmDetail.razor`/`.razor.cs`: branch on `_agm.IsRecorded` — `false` renders only `Date`/`Notes` plus a status badge (`<span class="badge bg-warning text-dark">Scheduled</span>`, matching this app's existing not-yet-done badge convention), no attendance-count line, no Elected Positions section; `true` keeps today's view (badge `bg-success` "Recorded") unchanged. Also update the "Back to Past AGMs" link text (both the not-found state and the page-bottom link) to "Back to AGMs", matching T015's heading change · `src/StageFright.UI/Pages/Events/AgmDetail.razor`, `AgmDetail.razor.cs`. Depends on T002 (Foundational) only.
- [ ] **T015** [P] [US1] `AgmList.razor`/`.razor.cs`: add a Status column ("Scheduled"/"Recorded" from `agm.IsRecorded`); change the page heading `<h1>` from "Past AGMs" to "AGMs"; change the empty-state message/link and toolbar button text from "Record AGM" to "Schedule AGM" (still routing to `/events/agm/new`); in the Attendance column, render "—" instead of `AttendedCount(agm) of agm.AttendanceRecords.Count` when `!agm.IsRecorded` · `src/StageFright.UI/Pages/Events/AgmList.razor`, `AgmList.razor.cs`. Depends on T002 (Foundational) only.

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — independent (different files):**

- [ ] **T016** [P] [US1] Implement `AgmRepository.ExistsForYearAsync`: `await _db.AnnualGeneralMeetings.AnyAsync(a => a.Date.Year == year, ct)` · `src/StageFright.Data/Repositories/AgmRepository.cs`. Depends on T011.
- [ ] **T017** [P] [US1] Implement `AgmService.ScheduleAsync`: call `_agmRepo.ExistsForYearAsync(request.Date.Year, ct)` before opening the write transaction and throw `ValidationException` if it returns `true` (same "check outside, write inside" shape `RecordAsync`'s existing duplicate-assignment check uses); inside one `IUnitOfWork` transaction, insert a new `AnnualGeneralMeeting` with `Date`, `Notes`, `IsRecorded = false`, `CreatedAt`/`UpdatedAt = DateTime.UtcNow`, and log `AuditAction.Create` · `src/StageFright.Core/Modules/Agm/AgmService.cs`. Depends on T011, T012, T013.

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 (single task):**

- [ ] **T018** [US1] New `ScheduleAgm.razor`/`.razor.cs` at `@page "/events/agm/new"` (replaces today's all-in-one page at this route): a date input and a notes input only; `SaveAsync` calls `AgmService.ScheduleAsync(new ScheduleAgmRequest(date, notes))`, shows a `ValidationException`'s message inline on failure, and navigates to `/events/agm/{agm.Id}` on success (Decision 7) · `src/StageFright.UI/Pages/Events/ScheduleAgm.razor`, `ScheduleAgm.razor.cs` (NEW). Depends on T012, T013, T017.

**Checkpoint**: US1 is independently functional and testable — an AGM can be scheduled with just a date and notes, shows as "Scheduled" on the list and detail page, creates no attendance/election/term data, and a second AGM in the same calendar year is rejected with nothing saved.

---

## Phase 4: User Story 2 - Record attendance and elections once the AGM has happened (Priority: P1)

**Goal**: A committee administrator can record attendance and committee elections against a previously scheduled AGM once its date has arrived, updating that same row rather than creating a new one — with recording before the date, or a second time, rejected.

**Independent Test**: Schedule an AGM, then record its attendance and elections once its date has arrived; confirm the same AGM record now shows attendance counts and elected positions and that a new committee term has started; confirm recording it again, or recording it before its date, is rejected.

### Tests

**Wave 1 — independent (different files):**

- [ ] **T019** [P] [US2] Rewrite `AgmServiceTests`' existing `RecordAsync` suite to the new `RecordAsync(Guid agmId, RecordAgmRequest)` shape: each test now seeds a scheduled AGM first (e.g. via `_agmRepo.AddAsync` with `IsRecorded = false`) and calls `RecordAsync(agmId, request)` with `Date`/`Notes` dropped from `RecordAgmRequest`; every existing case (persist/transaction/duplicate-assignment/rollback/term-rollover/month-labeling) keeps its assertions but reads `agm.Date` instead of `request.Date`. Add new cases: throws `EntityNotFoundException(nameof(AnnualGeneralMeeting), agmId, nameof(RecordAsync))` for an unknown `agmId`; throws `ValidationException` when the seeded AGM's `Date` is in the future; throws `ValidationException` when the seeded AGM already has `IsRecorded = true`; on success sets `IsRecorded = true` and logs `AuditAction.Update` (not `Create`, since `ScheduleAsync` now owns that event) · `tests/StageFright.Core.Tests/Modules/Agm/AgmServiceTests.cs`. Note: this same file was extended by T006 in the previous phase — merge into the existing test class, don't duplicate it.
- [ ] **T020** [P] [US2] Rewrite `RecordAgmTests` for the new `/events/agm/{Id}/record` route: remove the `#agmDate`/`#agmNotes` field assertions; add cases for "AGM not found", "this AGM has already been recorded", and "this AGM's date has not yet arrived" guard messages; update the save-call assertion to `AgmService.Received(1).RecordAsync(SavedAgmId, Arg.Is<RecordAgmRequest>(r => ...), ...)` without `Date`/`Notes` · `tests/StageFright.UI.Tests/Pages/Events/RecordAgmTests.cs`
- [ ] **T021** [P] [US2] Extend `AgmListTests`: a per-row "Record" action renders only when `!agm.IsRecorded` and navigates to `/events/agm/{id}/record`; a recorded row shows no such action (mirroring `RehearsalListTests`' Record/Recorded coverage) · `tests/StageFright.UI.Tests/Pages/Events/AgmListTests.cs`
- [ ] **T022** [P] [US2] Extend `AgmDetailTests`: the scheduled-AGM branch (from T010) renders a "Record Attendance & Elections" button that navigates to `/events/agm/{id}/record` · `tests/StageFright.UI.Tests/Pages/Events/AgmDetailTests.cs`

### Implementation

**Wave 1 — independent (different files):**

- [ ] **T023** [P] [US2] Drop `Date`/`Notes` from `RecordAgmRequest`, leaving `AttendedMemberIds`, `AllActiveMemberIds`, `OfficeHolderAssignments`, `GeneralCommitteeMemberIds` — they are already fixed on the AGM row from scheduling and (per spec Assumptions) can never be edited afterward · `src/StageFright.Core/Modules/Agm/RecordAgmRequest.cs`
- [ ] **T024** [P] [US2] Change `IAgmService.RecordAsync` to `Task<AnnualGeneralMeeting> RecordAsync(Guid agmId, RecordAgmRequest request, CancellationToken ct = default)`, with the contract's three failure-mode doc comments (`EntityNotFoundException` for an unknown `agmId`; `ValidationException` for a still-future date, an already-recorded AGM, or a duplicate committee assignment) · `src/StageFright.Core/Contracts/IAgmService.cs`
- [ ] **T025** [P] [US2] `AgmList.razor`/`.razor.cs`: add a per-row "Record" action (button, matching `RehearsalList`'s "Record Attendance" styling) when `!agm.IsRecorded`, navigating to `/events/agm/{agm.Id}/record` — offered unconditionally, matching `RehearsalList`'s precedent that the date/already-recorded guard lives on the target page, not the list row (research.md Decision 5) · `src/StageFright.UI/Pages/Events/AgmList.razor`, `AgmList.razor.cs`. Same files T015 (US1) already touched — sequential edit, not parallel with it.
- [ ] **T026** [P] [US2] `AgmDetail.razor`/`.razor.cs`: in the scheduled (`!_agm.IsRecorded`) branch from T014, add a "Record Attendance & Elections" button navigating to `/events/agm/{Id}/record` · `src/StageFright.UI/Pages/Events/AgmDetail.razor`, `AgmDetail.razor.cs`. Same files T014 (US1) already touched — sequential edit, not parallel with it.

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 (single task):**

- [ ] **T027** [US2] Re-shape `AgmService.RecordAsync`: load `var agm = await _agmRepo.GetByIdAsync(agmId, ct) ?? throw new EntityNotFoundException(...)`; guard `if (agm.IsRecorded) throw new ValidationException(...)` (FR-006); guard `if (agm.Date.Date > DateTime.Today) throw new ValidationException(...)` (FR-005); keep the unchanged duplicate-assignment check, attendance-record batch, open-term rollover, new-term creation, and position-record creation, replacing every `request.Date` read with `agm.Date`; inside the transaction set `agm.IsRecorded = true`, snapshot `GeneralCommitteeSeatCountTarget` from Settings (unchanged, still happens here per research.md Decision 8), `UpdateAsync` the AGM row (not `AddAsync`), and log `AuditAction.Update` instead of `Create` · `src/StageFright.Core/Modules/Agm/AgmService.cs`. Depends on T023, T024. Same file T017 (US1) already touched — sequential edit, not parallel with it.

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 (single task):**

- [ ] **T028** [US2] `RecordAgm.razor`/`.razor.cs`: move the route to `@page "/events/agm/{Id:guid}/record"`; in `OnParametersSetAsync`, load `_agm = await AgmService.GetByIdAsync(Id)`, guarding "AGM not found" (null), "This AGM has already been recorded" (`_agm.IsRecorded`), and "This AGM's date has not yet arrived" (`_agm.Date.Date > DateTime.Today`) — mirroring `AttendanceGrid.razor.cs`'s `_alreadyRecorded`/`_isFutureDate` guard shape — only loading active members/office-holder types/settings and building the attendance grid when none of the guards trip; remove the `#agmDate`/`#agmNotes` input fields from the markup entirely; on save, call `AgmService.RecordAsync(Id, new RecordAgmRequest(attendedIds, allActiveIds, officeHolderAssignments, generalCommitteeIds))` (no `Date`/`Notes`) and navigate to `/events/agm/{Id}` on success · `src/StageFright.UI/Pages/Events/RecordAgm.razor`, `RecordAgm.razor.cs`. Depends on T023, T024, T027.

**Checkpoint**: US2 is independently functional and testable — attendance and elections can be recorded against a previously scheduled AGM on or after its date, updating that same row; recording before the date or a second time is rejected with nothing changed; duplicate-assignment rejection and term rollover still work exactly as before.

---

## Phase 5: User Story 3 - Print a blank attendance report for a scheduled AGM (Priority: P2)

**Goal**: Printing an AGM's attendance report — already reachable from the list and detail Print buttons US1/US2 left unchanged — lists every currently active member with an unchecked box when the AGM is still scheduled, and keeps showing the fixed recorded roster once it isn't.

**Independent Test**: Schedule an AGM and print its attendance report before recording anything, confirming every currently active member appears with an unchecked box; then record attendance and reprint the same AGM to confirm the report now shows the real recorded roster and checkmarks.

### Tests

**Wave 1 (single task):**

- [ ] **T029** [US3] Extend `AgmAttendanceSheetServiceTests`: add an `IMemberRepository` NSubstitute to the fixture's constructor wiring. New cases — a scheduled AGM (`IsRecorded = false`) returns every member `IMemberRepository.GetByStatusAsync(MemberStatus.Active)` returns, ordered by `LastName` then `FirstName`, each with `Attended = false`, and never calls `IAgmAttendanceRepository.GetByAgmAsync` (`DidNotReceive()`); an empty active-member roster returns an empty (not thrown) `Members` list (FR-012); a recorded AGM (`IsRecorded = true`) keeps every existing fixed-roster assertion unchanged and never calls `IMemberRepository.GetByStatusAsync` · `tests/StageFright.Core.Tests/Modules/Agm/AgmAttendanceSheetServiceTests.cs`

### Implementation

**Wave 1 (single task):**

- [ ] **T030** [US3] Add `IMemberRepository` as a third constructor dependency of `AgmAttendanceSheetService`; branch `GenerateAsync` on `agm.IsRecorded` — `true` keeps the existing `_attendanceRepo.GetByAgmAsync` fixed-roster path unchanged; `false` instead builds `Members` from `(await _memberRepo.GetByStatusAsync(MemberStatus.Active, ct)).OrderBy(m => m.LastName).ThenBy(m => m.FirstName)`, each mapped with `Attended = false` — deliberately "active right now", not `GetActiveAsOfAsync(agm.Date)`, per the spec's Edge Cases (research.md Decision 4). Also add the one-sentence doc-comment addition to `IAgmAttendanceSheetService.GenerateAsync` describing the new branch · `src/StageFright.Core/Modules/Agm/AgmAttendanceSheetService.cs`, `src/StageFright.Core/Contracts/IAgmAttendanceSheetService.cs`. No `MauiProgram.cs` change needed — `IMemberRepository`/`MemberRepository` are already registered; only the constructor parameter is new. Depends on T029.

**Checkpoint**: US3 is independently functional and testable — printing a scheduled AGM's report (via the unchanged Print buttons US1/US2 already wired up) lists every active member unchecked, an empty active roster shows the existing empty-state message, and a recorded AGM's report is untouched.

---

## Phase 6: Polish

**Wave 1 (single task):**

- [ ] **T031** [P] New `V19_AgmSchedulingTests` integration test against a real SQLite in-memory database with full EF migrations (mirroring `V18_AgmWorkflowTests`'s `IAsyncLifetime` shape, no DI container): schedule an AGM → print its blank report (assert every active member appears, `Attended = false` for all) → record it → print again (assert the fixed recorded roster with real checkmarks) → attempt to schedule a second AGM in the same calendar year (assert `ValidationException`, nothing persisted) → archive the recorded AGM → schedule a replacement AGM in that same now-freed calendar year (assert it succeeds) · `tests/StageFright.Integration.Tests/Scenarios/V19_AgmSchedulingTests.cs` (NEW)

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 (single task):**

- [ ] **T032** Run `dotnet build` and the full `dotnet test` suite (all five test projects, no `--no-build`) from the repo root and confirm everything is green, per CLAUDE.md's Build & Test Verification rule.

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 (single task):**

- [ ] **T033** Walk every Acceptance Scenario in spec.md (US1's 4, US2's 3, US3's 3) plus the Edge Cases against a running `dotnet run --project src/StageFright.App/` instance, including scheduling a past-dated catch-up AGM (spec Assumptions) and the December-31/January-1 different-calendar-year case (Edge Cases); confirm FR-014's read-only guarantee — printing either a scheduled or recorded AGM's report creates, changes, or removes no `AnnualGeneralMeeting`, `AgmAttendanceRecord`, `CommitteePositionRecord`, `CommitteeTerm`, or `Member` record.

---

## Dependencies & Execution Order

- **Setup (Phase 1, T001)** → **Foundational (Phase 2, T002–T005)**: `IsRecorded` (T002) must exist before the migration (T004, depends on T002) and the backup DTO field (T003) must exist before `BackupService` mapping (T005, depends on T002+T003).
- **Foundational → US1 (Phase 3, T006–T018)**: Tests wave (T006–T010) is 5 independent new/extended test files, written first and expected to fail until Implementation lands. Implementation Wave 1 (T011–T015) is 5 independent files — 2 contract signatures, 1 new DTO, and 2 UI pages that need nothing beyond the Foundational `IsRecorded` field. Wave 2 (T016–T017) is the repository method and the service method, each depending only on Wave 1's contracts (not on each other). Wave 3 (T018) is the new `ScheduleAgm` page, depending on the real `ScheduleAsync` behavior from Wave 2.
- **Foundational → US2 (Phase 4, T019–T028)**: Same shape as US1. Implementation Wave 1 (T023–T026) is 4 independent files — the DTO change, the interface signature change, and two pure-navigation UI edits (`AgmList`'s Record action, `AgmDetail`'s Record button) that need only `agm.IsRecorded`, not the reshaped service. Wave 2 (T027) reshapes `AgmService.RecordAsync`, depending on Wave 1's DTO/interface changes. Wave 3 (T028) rewrites `RecordAgm.razor(.cs)`, depending on Wave 2's real guard behavior. `AgmService.cs` (T027), `AgmList.razor(.cs)` (T025), and `AgmDetail.razor(.cs)` (T026) are each the *second* edit to a file US1 already touched (T017, T015, T014 respectively) — sequential across phases, never parallel with US1's edit.
- **Foundational → US3 (Phase 5, T029–T030)**: Independent of US1/US2's service changes — only needs the Foundational `IsRecorded` field. Could in principle build alongside US1/US2, but is sequenced last per spec.md's P2 priority.
- **US1 + US2 + US3 → Polish (Phase 6, T031–T033)**: T031 (new end-to-end integration test) needs every story's behavior to exist; T032 (full build/test) needs T031's new test file written first to include it in the run; T033 (manual walkthrough) needs T032 green before it's a meaningful check.

---

## Requirement Coverage

| Requirement | Tasks |
|---|---|
| FR-001 | T006, T011, T012, T013, T017, T018 |
| FR-002 | T006, T017 |
| FR-003 | T009, T015 |
| FR-004 | T019, T023, T024, T027, T028 |
| FR-005 | T019, T027, T028 |
| FR-006 | T019, T027, T028 |
| FR-007 | T019, T027 |
| FR-008 | T010, T014 |
| FR-009 | T029, T030 |
| FR-010 | T029, T030 |
| FR-011 | T029, T030 |
| FR-012 | T029, T030 |
| FR-013 | T031 (existing `ArchiveAsync` already tolerates a scheduled AGM's empty `AttendanceRecords` — no production change needed, only test coverage) |
| FR-014 | T029, T030, T033 |
| FR-015 | T006, T007, T011, T016, T017, T018, T031 |
