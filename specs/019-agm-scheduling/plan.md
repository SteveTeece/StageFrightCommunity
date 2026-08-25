# Implementation Plan: Schedule Future AGMs

**Branch**: `019-agm-scheduling` | **Date**: 2026-08-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/019-agm-scheduling/spec.md`

## Summary

Split today's single-step "record an AGM" flow into a schedule-then-record flow, mirroring how
Rehearsals already separate `RehearsalService.ScheduleAsync` from
`AttendanceService.RecordBatchAsync`. A new `AnnualGeneralMeeting.IsRecorded` flag (tracked
directly on the row, never inferred from attendance rows) distinguishes a scheduled AGM (date +
notes only) from a recorded one (attendance + committee elections + a new committee term).
`IAgmService` gains `ScheduleAsync` and re-shapes `RecordAsync` to complete an existing scheduled
AGM by id instead of creating one from scratch; a new `IAgmRepository.ExistsForYearAsync` check
enforces the one-non-archived-AGM-per-calendar-year rule. The existing
`AgmAttendanceSheetService`/`IAgmAttendanceSheetPdfRenderer` pipeline from spec 018 is extended
(not replaced) to print a blank, all-unchecked report for a scheduled AGM from the currently-active
member roster, reusing the same renderer unchanged.

## Project Structure

### Documentation (this feature)

```text
specs/019-agm-scheduling/
├── plan.md              # This file (/speckit-companion-plan output)
├── research.md           # Phase 0 output
├── data-model.md          # Phase 1 output
├── contracts/
│   └── agm-scheduling-contract.md   # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-companion-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── StageFright.Core/
│   ├── Entities/
│   │   └── AnnualGeneralMeeting.cs           # + IsRecorded (bool)
│   ├── Contracts/
│   │   ├── IAgmRepository.cs                 # + ExistsForYearAsync(int year)
│   │   └── IAgmService.cs                    # + ScheduleAsync; RecordAsync signature changed
│   └── Modules/
│       ├── Agm/
│       │   ├── AgmService.cs                 # + ScheduleAsync; RecordAsync re-shaped
│       │   ├── ScheduleAgmRequest.cs          # NEW — Date, Notes
│       │   ├── RecordAgmRequest.cs            # CHANGED — Date/Notes removed
│       │   └── AgmAttendanceSheetService.cs   # + IsRecorded branch, + IMemberRepository dep
│       └── Settings/
│           └── BackupService.cs               # MapAgm/MapAgmFromDto + IsRecorded line each
├── StageFright.Data/
│   ├── Repositories/AgmRepository.cs          # + ExistsForYearAsync
│   └── Migrations/
│       └── <timestamp>_AddIsRecordedToAgm.cs  # NEW — IsRecorded column + backfill existing rows
└── StageFright.UI/Pages/Events/
    ├── AgmList.razor / .razor.cs      # + Status column, "Record" row action, heading/copy fixes
    ├── AgmDetail.razor / .razor.cs    # + IsRecorded branch (date/notes-only vs. full view)
    ├── ScheduleAgm.razor / .razor.cs  # NEW — replaces RecordAgm.razor at /events/agm/new
    └── RecordAgm.razor / .razor.cs    # CHANGED — moves to /events/agm/{Id}/record, loads existing AGM

tests/
├── StageFright.Core.Tests/Modules/Agm/
│   ├── AgmServiceTests.cs                    # extended — ScheduleAsync, re-shaped RecordAsync, year-uniqueness
│   └── AgmAttendanceSheetServiceTests.cs      # extended — scheduled-AGM branch, empty-active-roster case
├── StageFright.Data.Tests/Repositories/
│   └── AgmRepositoryTests.cs                  # NEW — ExistsForYearAsync (archived exclusion, cross-year)
├── StageFright.UI.Tests/Pages/Events/
│   ├── AgmListTests.cs        # extended — Status column, Record action
│   ├── AgmDetailTests.cs      # extended — scheduled-vs-recorded branch
│   ├── RecordAgmTests.cs      # rewritten — loads existing AGM, date/already-recorded guards
│   └── ScheduleAgmTests.cs    # NEW — mirrors RehearsalFormTests shape
└── StageFright.Integration.Tests/Scenarios/
    └── V19_AgmSchedulingTests.cs  # NEW — schedule → print blank → record → print recorded → year-reject → archive-frees-year, against real SQLite
```

**Structure Decision**: No new projects. The feature reshapes one existing entity field, one
existing repository, one existing service, and four existing UI pages inside the already-owned
`Agm` module — no new module folder, no new plugin contract, no new `StageFright.Reports`
renderer (the spec-018 renderer is reused unchanged). See [data-model.md](./data-model.md) for the
full type-level design and [research.md](./research.md) for the decisions and rejected
alternatives behind this structure.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design below.*

| Gate | Status | Notes |
|---|---|---|
| §3.2.1 / §4.5 One Class Per File | **PASS** | Every new type gets its own file (`ScheduleAgmRequest`, new migration class). `RecordAgmRequest`, `AnnualGeneralMeeting`, `IAgmRepository`, `IAgmService`, `AgmService`, `AgmRepository`, `AgmAttendanceSheetService` are modified in place, not merged with another type. |
| §3.3 Separation of Concerns | **PASS** | The year-uniqueness rule and the recorded/scheduled state machine live entirely in `AgmService` (Core); `AgmRepository` only adds a plain existence query; UI pages only orchestrate navigation and call the service, exactly mirroring the existing `RehearsalForm`/`AttendanceGrid` split. |
| §3.4 / §3.5 Soft-Delete & Member/Financial Data Preservation | **PASS** | `IsRecorded` is additive to the existing soft-delete-capable entity; `ArchiveAsync` (unchanged) continues to soft-delete both scheduled and recorded AGMs (FR-013). No `Member`, `Fee`, `Payment`, or `Transaction` record is touched by this feature — AGM scheduling/recording carries no financial dimension, same as today. |
| §3.6 Financial Corrections Pattern | **N/A** | No `Fee`/`Payment`/`Transaction`/GL record is created, read, or touched anywhere in this feature. |
| §4.1 Layered Architecture with Module Slices | **PASS** | All new/changed service and DTO code stays inside `StageFright.Core/Modules/Agm/`; the new repository method is added to the existing centralized `StageFright.Data/Repositories/AgmRepository.cs`; the new/changed contracts are added to the existing centralized `StageFright.Core/Contracts/`. No cross-module import is introduced — `AgmAttendanceSheetService`'s new `IMemberRepository` dependency is a published `Contracts/` interface, the same relationship `EventAttendanceSheetService` already has to it (spec 018 precedent). |
| §4.7 Blazor Component Patterns (code-behind, no inline `@code`) | **PASS** | `ScheduleAgm.razor`/`.razor.cs` follows the exact `RehearsalForm.razor`/`.razor.cs` pairing; `RecordAgm.razor.cs`'s changed logic (load-by-id, guard states) stays in its code-behind file, mirroring `AttendanceGrid.razor.cs`'s existing `_alreadyRecorded`/`_isFutureDate` guard shape. No `@code` blocks, no new `.razor.css` (reuses existing alert/button styling). |
| §5 Custom Exceptions at Boundaries | **PASS** | `ScheduleAsync` throws `ValidationException` for the year-uniqueness rule; `RecordAsync` throws `EntityNotFoundException` for an unknown id and `ValidationException` for the already-recorded/not-yet-due guards — all following the exact constructor shape and precedent `RecordAsync`'s existing duplicate-assignment check and `RecordBatchAsync`'s existing date guard already use. No raw framework exception crosses a boundary. |
| §11 Exhaustive Test Coverage | **PASS (planned)** | Project Structure above enumerates every new/extended test file across all four test projects: service-level (schedule, record, both guards, year-uniqueness, archived-year-freed), repository-level (`ExistsForYearAsync`), UI-level (status column, per-state branching, new/changed pages), and one new end-to-end integration scenario (`V19_AgmSchedulingTests.cs`) covering the full schedule → print-blank → record → print-recorded → reject-duplicate-year → archive-frees-year journey against real SQLite. Tasks.md (Phase 2) enumerates every case. |
| §7.1/§7.3 Tech Stack / No Custom JS | **PASS** | Pure C#/Blazor/EF Core change; one new EF Core migration; no JavaScript; no new NuGet dependency. |

No violations requiring Complexity Tracking.

**Post-Phase-1 re-check** (after research.md/data-model.md/contracts/ were written): All gates
above still **PASS**. Design introduced exactly the surface anticipated above — one entity field,
one repository method, one new + one re-shaped service method, two request DTOs, one extended
read-model branch, and four touched UI pages — with no scope creep, no new plugin surface, and no
financial-record involvement. No Complexity Tracking entries required.
