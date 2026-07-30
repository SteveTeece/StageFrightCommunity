# Implementation Plan: Printable Member Attendance Roll

**Branch**: `012-printable-attendance-roll` | **Date**: 2026-07-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/012-printable-attendance-roll/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

**Note**: This Summary and the Technical Context/Constraints/Scale sections below already
describe the corrected behavior — point-in-time active membership, real "Present"/fee-paid
checkboxes, and the static "Pd" fee-column heading — following the "Correction — 2026-07-28"
clarification session in spec.md. The full history of what changed and why (including the
now-removed `AnnualFeePaid` field) is recorded in research.md Decisions 5 and 8-10 and
data-model.md; this plan's Constitution Check gates below remain valid unchanged (no new module,
no schema change, same layering).

Add a "Print Roll" action to each scheduled rehearsal (surfaced from `RehearsalList.razor`'s
Actions column) that generates a printable PDF attendance roll: every member active as of the
rehearsal's date, sorted by surname then first name, surname in capitals, with "Present" and
attendance-fee checkboxes that print blank before attendance is recorded and reflect real
recorded attendance/fee-payment state once it has been. The roll is laid out in a
print-optimized two-column format (second column as same-page overflow, minimal-width checkbox
columns, wrapping headers) and paginates automatically for larger rosters. Because this layout
(two columns, checkbox glyphs) cannot be expressed by the existing generic `ReportData`/
`IReportProvider`/`PdfReportRenderer` pipeline (flat single table of string cells, no multi-column
support), the feature adds one new module-owned data-assembly service
(`IAttendanceRollService` in `StageFright.Core/Modules/Rehearsals/`) and one new bespoke QuestPDF
renderer (`IAttendanceRollPdfRenderer` in `StageFright.Reports/Rendering/`) that reuses the same
QuestPDF technology and "render → temp file → `Process.Start`" print UX already established by
`ReportViewer.razor.cs`, without going through the generic reports menu.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`)

**Primary Dependencies**: .NET MAUI Blazor Hybrid, Radzen.Blazor (`RadzenDataGrid` on the
Rehearsals list), QuestPDF 2026.6.0 (Community license, PDF rendering — same package already used
by `PdfReportRenderer`), Serilog (via `ILogger<T>`)

**Storage**: SQLite (`TestData/stagefright.db`), read-only for this feature — no new tables,
columns, or migrations; reads existing `Rehearsal`, `Member`, `Fee`, and GL (`Transaction`) data
through existing repositories

**Testing**: xUnit (unit + integration), NSubstitute (service/repository mocks), bUnit (Blazor
component tests for `RehearsalList.razor`), direct QuestPDF-output assertions (byte array
non-null/non-empty, no-throw) following `PdfAndCsvRendererTests.cs`'s convention

**Target Platform**: Windows desktop and macOS desktop (MAUI), Blazor Hybrid hosting model

**Project Type**: Desktop app — single .NET solution (`StageFrightCommunity.slnx`), layered
projects (App/Core/Data/Reports/UI/Plugins.Contracts); no separate frontend/backend split

**Performance Goals**: No new performance targets; a typical rehearsal roster (tens to low
hundreds of active members) must render to PDF well within the existing report-generation UX
(synchronous, "Generating…" pattern not required here since there is no interactive filter step —
the print action is a single click that assembles data and opens the PDF)

**Constraints**: Read-only operation — MUST NOT create, update, or delete any `Member`,
`Rehearsal`, `Fee`, `Payment`, `Transaction`, or GL record (spec Assumptions); must reuse the
exact "active member" definition already used by `AttendanceGrid.razor.cs`
(`IMemberService.GetByStatusAsync(MemberStatus.Active)`) so the paper roll and the later digital
attendance entry always list the same members (FR-002); must reuse the existing GL-derived
outstanding-fee logic (`IMemberBalanceService.GetOutstandingFeesAsync`) rather than a new
paid/unpaid flag, since fees carry no per-record paid flag in this system; two-column layout with
minimal-width checkbox columns and wrapping headers, continuing onto additional physical pages for
larger rosters (FR-009, FR-010, FR-011)

**Scale/Scope**: One new Core service + DTOs (`StageFright.Core/Modules/Rehearsals/`), one new
renderer (`StageFright.Reports/Rendering/`), one new button/handler on an existing page
(`RehearsalList.razor`/`.razor.cs`), two new DI registrations (`MauiProgram.cs`); no changes to
`Member`, `Rehearsal`, `Fee`, or GL entities/schema; sized for a community group's rehearsal
roster (tens to low hundreds of members), not a high-volume dataset

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Status | Notes |
|---|---|---|
| §3.2.1 / §4.5 One Class Per File | **PASS** | Every new type (`IAttendanceRollService`, `AttendanceRollService`, `AttendanceRollData`, `AttendanceRollMember`, `IAttendanceRollPdfRenderer`, `AttendanceRollPdfRenderer`) gets its own file; no multi-type files introduced. |
| §3.3 Separation of Concerns | **PASS** | Data assembly (active members, annual-fee-paid computation) lives in Core (`StageFright.Core/Modules/Rehearsals`); PDF rendering/layout lives in Reports (`StageFright.Reports/Rendering`), matching the existing split between report providers and `PdfReportRenderer`. UI (`RehearsalList.razor.cs`) only orchestrates the two calls and the temp-file/launch step, mirroring `ReportViewer.razor.cs`. |
| §3.4 / §3.5 Soft-Delete & Member/Financial Data Preservation | **PASS** | Read-only feature — no entity is created, updated, or deleted. Archived/inactive/soft-deleted members are excluded via the existing `GetByStatusAsync(Active)` repository filter, not a new query. |
| §3.6 Financial Corrections Pattern | **N/A** | No `Fee`/`Payment`/`Transaction`/GL record is created, edited, or reversed; the feature only reads `IMemberBalanceService.GetOutstandingFeesAsync` for a paid/unpaid signal. |
| §4.1 Vertical Slice Module Architecture | **PASS** | New service and DTOs are added under `StageFright.Core/Modules/Rehearsals/` (the module that already owns rehearsal/attendance logic); the renderer stays in `StageFright.Reports` alongside the existing `PdfReportRenderer`, consistent with that project's stated role ("Report infrastructure ... PdfReportRenderer (QuestPDF)"). No cross-module reach-through — the Rehearsals module depends on the already-published `IMemberService`/`IMemberBalanceService` interfaces from Members/Finance, not their internals. |
| §4.7 Blazor Component Patterns (code-behind, no inline `@code`) | **PASS** | `RehearsalList.razor` gets one new button in the existing `Template` block; all new logic (the `PrintRoll` handler, temp-file/launch call, empty-state message) goes in `RehearsalList.razor.cs`. No `.razor.css` needed (reuses existing button/alert styling). |
| §5 Custom Exceptions at Boundaries | **PASS** | `AttendanceRollService.GenerateAsync` throws the existing `EntityNotFoundException("Rehearsal", rehearsalId, ...)` for an unknown rehearsal id, following the exact precedent already used by `RehearsalService.FreezeAttendanceRateAsync`. No raw framework exception crosses a boundary; QuestPDF rendering exceptions are caught in the UI layer exactly as `ReportViewer.razor.cs` already does for `PrintReport()`. |
| §11 Exhaustive Test Coverage | **PASS (planned)** | research.md and quickstart.md enumerate the required test additions across `StageFright.Core.Tests` (service logic: sorting, active-member filter, annual-fee-paid computation, not-found), `StageFright.Reports.Tests` (renderer: pagination boundaries, non-empty PDF, no-throw), `StageFright.UI.Tests` (button rendering, empty-state message), and `StageFright.Integration.Tests` (end-to-end against a real SQLite in-memory DB, extending `V3_RehearsalAttendanceTests.cs`). Tasks.md (Phase 2) will enumerate every test file and case. |
| §7.1/§7.3 Tech Stack / No Custom JS | **PASS** | Pure C#/Blazor/QuestPDF change; no JavaScript involved; QuestPDF already an approved dependency (Community license, already in use). |

No violations requiring Complexity Tracking justification.

**Post-Phase-1 re-check** (after research.md/data-model.md/contracts/quickstart.md were written):
All gates above still **PASS**. Design added exactly the types anticipated above (no scope
creep), keeps the new PDF-rendering path fully separate from the generic `IReportProvider`
pipeline (a deliberate, documented deviation — see research.md Decision 1 — not a pipeline
change that could affect the six existing MVP reports), and introduces no new cross-module
coupling, plug-in surface, or financial-record mutation. No Complexity Tracking entries required.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
├── StageFright.Core/
│   └── Modules/Rehearsals/
│       ├── IAttendanceRollService.cs      # Contract: GenerateAsync(rehearsalId) -> AttendanceRollData
│       ├── AttendanceRollService.cs       # Assembles active members + annual-fee-paid flag for a rehearsal
│       ├── AttendanceRollData.cs          # Rehearsal date/time + ordered member rows
│       └── AttendanceRollMember.cs        # FirstName, LastName, Attended, RehearsalFeePaid
├── StageFright.Reports/Rendering/
│   ├── IAttendanceRollPdfRenderer.cs      # Contract: Render(AttendanceRollData, organizationName) -> byte[]
│   └── AttendanceRollPdfRenderer.cs       # QuestPDF two-column, checkbox-box, wrapping-header layout
├── StageFright.UI/Pages/Rehearsals/
│   ├── RehearsalList.razor                # New "Print Roll" button in the Actions column template
│   └── RehearsalList.razor.cs             # PrintRoll(rehearsalId) handler: generate -> empty-state check -> render -> temp file -> Process.Start
└── StageFright.App/MauiProgram.cs         # Two new DI registrations (IAttendanceRollService, IAttendanceRollPdfRenderer)

tests/
├── StageFright.Core.Tests/Modules/Rehearsals/
│   └── AttendanceRollServiceTests.cs      # Sorting, active-member filter, annual-fee-paid computation, not-found, empty roster
├── StageFright.Reports.Tests/
│   └── AttendanceRollPdfRendererTests.cs  # Non-null/non-empty PDF, pagination boundaries (1 col, 2 cols, 2nd page), no-throw on empty roster
├── StageFright.UI.Tests/Pages/Rehearsals/
│   └── RehearsalListTests.cs              # Button renders per row; success path; empty-state message path (existing file, extended)
└── StageFright.Integration.Tests/Scenarios/
    └── V3_RehearsalAttendanceTests.cs     # End-to-end against real SQLite in-memory DB (existing file, extended)
```

**Structure Decision**: Existing single-solution, layered-project structure (per CLAUDE.md's
"Project layout") is unchanged — no new projects are introduced. The feature adds one new
module-owned service slice inside the existing `StageFright.Core/Modules/Rehearsals/` folder, one
new renderer inside the existing `StageFright.Reports/Rendering/` folder (sibling to
`PdfReportRenderer`, sharing the QuestPDF dependency already referenced by that project), and
extends one existing UI page and its test file. See [data-model.md](./data-model.md) for the
full type-level design and [research.md](./research.md) for the decisions and rejected
alternatives behind this structure.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
