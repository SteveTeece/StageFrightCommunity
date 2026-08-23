# Implementation Plan: Print Reports for Event and AGM Attendance

**Branch**: `018-event-agm-attendance-reports` | **Date**: 2026-08-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/018-event-agm-attendance-reports/spec.md`

## Summary

Add a "Print" action to the Events list/detail pages and the Past AGMs list/detail pages that
generates a printable PDF attendance sheet: for an event, every member active as of the event's
date with a "Participated" checkbox that prints blank until participation is recorded and matches
the real value afterward; for a past AGM, the fixed attendance roster captured when the AGM was
saved, each member's checkbox matching their real recorded attended/absent status. Both sheets use
the same two-column, minimal-width checkbox-box, capitalized-surname print format already
established by the rehearsal attendance roll (spec 012). Because that layout (multi-column,
bordered checkbox glyphs) cannot be expressed by the generic `ReportData`/`IReportProvider`/
`PdfReportRenderer` pipeline (a flat single table of pre-formatted string cells), this feature
follows spec 012's own precedent exactly: one new read-only Core service + DTO pair per owning
module (`Events`, `Agm`) that assembles the printable data, and one new QuestPDF renderer per
module in `StageFright.Reports`. The two new renderers share one internal two-column page-layout
helper so the event sheet and the AGM sheet are guaranteed to render identically (per User Story
3) without hand-duplicating the pagination/checkbox-cell code twice.

## Project Structure

### Documentation (this feature)

```text
specs/018-event-agm-attendance-reports/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── contracts/
│   └── event-agm-attendance-sheet-contract.md   # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit-tasks command — NOT created here)
```

### Source Code (repository root)

```text
src/
├── StageFright.Core/
│   ├── Contracts/
│   │   ├── IEventAttendanceSheetService.cs    # NEW — GenerateAsync(eventId) -> EventAttendanceSheetData
│   │   └── IAgmAttendanceSheetService.cs      # NEW — GenerateAsync(agmId) -> AgmAttendanceSheetData
│   └── Modules/
│       ├── Events/
│       │   ├── EventAttendanceSheetService.cs    # NEW — active-as-of-date roster + real/blank Participated flag
│       │   ├── EventAttendanceSheetData.cs       # NEW — event date/type + ordered member rows
│       │   └── EventAttendanceSheetMember.cs     # NEW — FirstName, LastName, Participated
│       └── Agm/
│           ├── AgmAttendanceSheetService.cs      # NEW — reads the AGM's fixed, already-persisted roster
│           ├── AgmAttendanceSheetData.cs         # NEW — AGM date + ordered member rows
│           └── AgmAttendanceSheetMember.cs       # NEW — FirstName, LastName, Attended
├── StageFright.Reports/Rendering/
│   ├── IEventAttendanceSheetPdfRenderer.cs    # NEW — Render(EventAttendanceSheetData, orgName) -> byte[]
│   ├── EventAttendanceSheetPdfRenderer.cs     # NEW
│   ├── IAgmAttendanceSheetPdfRenderer.cs      # NEW — Render(AgmAttendanceSheetData, orgName) -> byte[]
│   ├── AgmAttendanceSheetPdfRenderer.cs       # NEW
│   └── CheckboxSheetPdfBuilder.cs             # NEW — internal; shared two-column/checkbox-cell QuestPDF
│                                               #        layout used by both renderers above (AttendanceRollPdfRenderer
│                                               #        is untouched — it keeps its own two-checkbox-column layout)
├── StageFright.UI/Pages/Events/
│   ├── EventList.razor / .razor.cs      # + "Print" action per row (Actions column)
│   ├── EventDetail.razor / .razor.cs    # + "Print Attendance Sheet" button
│   ├── AgmList.razor / .razor.cs        # + "Print" action per row
│   └── AgmDetail.razor / .razor.cs      # + "Print Attendance Report" button
└── StageFright.App/MauiProgram.cs       # 4 new DI registrations (2 services, 2 renderers)

tests/
├── StageFright.Core.Tests/Modules/Events/
│   └── EventAttendanceSheetServiceTests.cs     # NEW — roster/sorting, real vs blank Participated, not-found, empty roster
├── StageFright.Core.Tests/Modules/Agm/
│   └── AgmAttendanceSheetServiceTests.cs       # NEW — roster from AgmAttendanceRecord, not-found, empty roster
├── StageFright.Reports.Tests/
│   ├── EventAttendanceSheetPdfRendererTests.cs # NEW — non-empty PDF, pagination boundaries, no-throw on empty roster
│   └── AgmAttendanceSheetPdfRendererTests.cs   # NEW — same shape as above, AGM-specific header text
├── StageFright.UI.Tests/Pages/Events/
│   ├── EventListTests.cs      # extended — Print action renders per row
│   ├── EventDetailTests.cs    # NEW — Print button renders; page currently has no test file
│   ├── AgmListTests.cs        # extended — Print action renders per row
│   └── AgmDetailTests.cs      # extended — Print button renders
└── StageFright.Integration.Tests/Scenarios/
    ├── V5_EventsParticipationTests.cs   # extended — print end-to-end against real SQLite
    └── V18_AgmWorkflowTests.cs          # extended — print end-to-end against real SQLite
```

**Structure Decision**: No new projects. The feature adds two new module-owned service slices
(`StageFright.Core/Modules/Events/`, `StageFright.Core/Modules/Agm/`), two new renderers plus one
new shared internal layout helper in the existing `StageFright.Reports/Rendering/` folder
(sibling to `AttendanceRollPdfRenderer`, reusing the QuestPDF dependency already referenced by
that project), and extends four existing UI pages plus two existing integration-test scenario
files. See [data-model.md](./data-model.md) for the full type-level design and
[research.md](./research.md) for the decisions and rejected alternatives behind this structure.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design below.*

| Gate | Status | Notes |
|---|---|---|
| §3.2.1 / §4.5 One Class Per File | **PASS** | Every new type gets its own file (`EventAttendanceSheetService`, `EventAttendanceSheetData`, `EventAttendanceSheetMember`, `AgmAttendanceSheetService`, `AgmAttendanceSheetData`, `AgmAttendanceSheetMember`, both renderer interfaces/implementations, `CheckboxSheetPdfBuilder`). No multi-type files. |
| §3.3 Separation of Concerns | **PASS** | Data assembly (active-as-of-date roster, real/blank checkbox values) lives in Core, per owning module; PDF layout lives in `StageFright.Reports`; UI pages only orchestrate the generate → empty-state check → render → temp-file → `Process.Start` sequence, exactly mirroring `RehearsalList.razor.cs`. |
| §3.4 / §3.5 Soft-Delete & Member/Financial Data Preservation | **PASS** | Read-only feature (FR-010) — no `Event`, `AnnualGeneralMeeting`, `ParticipationRecord`, `AgmAttendanceRecord`, `Member`, `Fee`, `Payment`, or `Transaction` record is created, updated, or deleted. The event sheet excludes archived/soft-deleted members via the existing `IMemberRepository.GetActiveAsOfAsync` filter (`IsDeleted=false`), satisfying FR-002/Acceptance Scenario 3 with no new query. |
| §3.6 Financial Corrections Pattern | **N/A** | No `Fee`/`Payment`/`Transaction`/GL record is touched — Events never generate financial records (events living spec), and AGM attendance carries no financial dimension. |
| §4.1 Layered Architecture with Module Slices | **PASS** | New services/DTOs live under each feature's owning module (`Modules/Events/`, `Modules/Agm/`) per §4.1; new interfaces are added to the centralized `Contracts/` folder alongside every other published contract. No module imports another module's concrete class — `EventAttendanceSheetService` depends only on the already-published `IEventRepository`/`IMemberRepository`, and `AgmAttendanceSheetService` only on `IAgmRepository`/`IAgmAttendanceRepository`. The two new renderers and their shared internal layout helper stay in `StageFright.Reports`, which is a downstream layer, not a Core module — sharing layout code there does not create a Core-to-Core cross-module dependency. |
| §4.7 Blazor Component Patterns (code-behind, no inline `@code`) | **PASS** | Each touched page gets one new button in its existing markup; all new logic (the `PrintXxx` handler, empty-state message field, temp-file/launch call) goes in the paired `.razor.cs` file, mirroring `RehearsalList.razor`/`.razor.cs` exactly. No `.razor.css` needed — reuses existing button/alert styling. |
| §5 Custom Exceptions at Boundaries | **PASS** | Both new services throw the existing `EntityNotFoundException` (`"Event"`/`"AnnualGeneralMeeting"`, id, `nameof(GenerateAsync)`) for an unknown id, following the exact precedent `AttendanceRollService.GenerateAsync` already uses. No raw framework exception crosses a boundary; QuestPDF rendering exceptions are caught in the UI layer exactly as `RehearsalList.razor.cs`'s `PrintRoll` already does. |
| §11 Exhaustive Test Coverage | **PASS (planned)** | research.md and the Project Structure above enumerate the required test additions across `StageFright.Core.Tests` (service: sorting, real/blank checkbox, not-found, empty roster), `StageFright.Reports.Tests` (renderer: pagination boundaries, non-empty PDF, no-throw on empty roster, per-domain header text), `StageFright.UI.Tests` (button rendering, empty-state message, success path), and `StageFright.Integration.Tests` (end-to-end against real SQLite, extending the existing V5/V18 scenario files). Tasks.md (Phase 2) enumerates every test file and case. |
| §7.1/§7.3 Tech Stack / No Custom JS | **PASS** | Pure C#/Blazor/QuestPDF change; no JavaScript; QuestPDF is already an approved, in-use dependency (Community license). |

No violations requiring Complexity Tracking.

**Post-Phase-1 re-check** (after research.md/data-model.md/contracts/ were written): All gates
above still **PASS**. Design introduced exactly the types anticipated above — no scope creep — and
the one addition beyond spec 012's literal precedent (the shared internal `CheckboxSheetPdfBuilder`)
stays entirely inside `StageFright.Reports`, so it introduces no new cross-module coupling, no new
plugin surface, and no financial-record mutation. No Complexity Tracking entries required.
