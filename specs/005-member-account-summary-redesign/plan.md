# Implementation Plan: Member Account Summary Report Redesign

**Branch**: `005-member-account-summary-redesign` | **Date**: 2026-07-12 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-member-account-summary-redesign/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

The Member Account Summary report currently always renders full per-member transaction detail. This feature adds an archived-members filter (default off), collapses the on-screen view to one row per member (name + aging buckets), lets a committee member click a row to expand it into the existing full detail (opening balance, standard chronological transactions, closing balance, aging), and renders the collapsed member list using `RadzenDataGrid` with Radzen's built-in master-detail row-expand pattern. The capability is added as an optional, backward-compatible extension to the shared `ReportData`/`ReportSection`/`ReportViewer` pipeline so the other five existing reports are unaffected.

## Technical Context

**Language/Version**: C# 14 (.NET, MAUI Blazor Hybrid)

**Primary Dependencies**: `Radzen.Blazor` (RadzenDataGrid master-detail), existing `StageFright.Reports` pipeline (`IReportProvider`, `ReportData`, `PdfReportRenderer` [QuestPDF], `CsvReportExporter` [CsvHelper]), Bootstrap (existing hand-rolled detail table markup, unchanged)

**Storage**: SQLite via EF Core (`StageFrightDbContext`) — read-only for this feature; no schema/migration changes (FR-009)

**Testing**: xUnit (`StageFright.Reports.Tests`, `StageFright.Core.Tests`), bUnit (`StageFright.UI.Tests`), integration tests (`StageFright.Integration.Tests`)

**Target Platform**: Windows desktop and macOS desktop (MAUI Blazor Hybrid), per constitution §7.1

**Project Type**: Desktop app (existing solution; no new projects)

**Performance Goals**: No new performance requirement; report generation remains synchronous with the existing 5-second cancel-button threshold in `ReportViewer`

**Constraints**: No custom JavaScript (constitution §7.3); one class per file; custom exceptions at layer boundaries only where a boundary is actually crossed (this feature adds no new I/O, so no new exception types are needed); soft-delete conventions unaffected (archived members are read via existing repository methods, never hard-deleted)

**Scale/Scope**: Single report provider (`MemberAccountSummaryReportProvider`), the shared report model (3 files in `StageFright.Reports/Models`), `ReportViewer.razor`/`.razor.cs`, and their associated tests — roughly 8-10 files touched, no new projects or major subsystems

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **§3.4 Soft Delete Pattern**: PASS. Archived members are read via existing soft-delete-aware repository methods (`GetAllAsync`/archived query already used today); nothing is deleted or hard-removed. The new filter only changes which already-fetched members are included.
- **§3.5/§3.6 Financial Data Preservation / Corrections**: PASS. FR-009 explicitly forbids changing GL/aging calculations; this feature only changes filtering, ordering, and presentation of already-computed values.
- **§4.5 Code Organization**: PASS. New/changed types (`ReportSection.SummaryRow`, `ReportData.SummaryColumns`) are properties on existing classes, not new types, so no one-class-per-file violation. Any new render-fragment helper types introduced in `ReportViewer.razor.cs` will get their own file if they are more than a small private record.
- **§4.7 Blazor Component Patterns (MANDATORY)**: PASS. `ReportViewer.razor` already has a paired `.razor.cs`; the master-detail rendering logic (expand state, dynamic column generation) is added to the existing code-behind file, not inline in a `@code` block.
- **§7.2/§7.3 Architecture Requirements / Prohibited**: PASS. `RadzenDataGrid` is an approved Radzen component; no custom JavaScript is introduced.
- **§9/§10 Spec & Plan requirements**: PASS. Spec (this feature) documents purpose, scope, interfaces (`ReportData`/`ReportSection` contract), dependencies, constraints, and acceptance criteria; no hidden side effects or global state are introduced.
- **§11 Testing Standards**: PASS (tracked as task work, not yet written). Existing tests for the 5 unaffected reports must continue passing unchanged; new/updated tests are required for the filter, summary row/columns population, expand/collapse rendering, and confirming transaction order is unchanged — see `quickstart.md` for the validation checklist.

No violations requiring `Complexity Tracking` justification.

## Project Structure

### Documentation (this feature)

```text
specs/005-member-account-summary-redesign/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md         # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/StageFright.Reports/
├── Models/
│   ├── ReportSection.cs        # + SummaryRow property (optional, nullable)
│   └── ReportData.cs           # + SummaryColumns property (optional, nullable)
└── Providers/
    └── MemberAccountSummaryReportProvider.cs
        # + includeArchived filter, SummaryRow/SummaryColumns population,
        #   chronological (oldest-first) ordering confirmed unchanged

src/StageFright.UI/Shared/
├── ReportViewer.razor          # Conditional RadzenDataGrid master-detail rendering
└── ReportViewer.razor.cs       # Expand-state tracking, dynamic column generation

tests/StageFright.Reports.Tests/
└── MemberAccountSummaryReportProviderTests.cs   # Updated + new cases

tests/StageFright.UI.Tests/Shared/
└── ReportViewerTests.cs                          # Updated + new master-detail cases

tests/StageFright.Integration.Tests/Scenarios/
├── V6_AccountingReportsTests.cs                  # Updated for new default filter/behavior
└── V11_ReportsMenuTests.cs                       # Updated if it asserts report content
```

**Structure Decision**: This feature extends the existing single-solution desktop app (`StageFrightCommunity.slnx`); no new projects. Changes are confined to the `StageFright.Reports` model/provider layer and the `StageFright.UI` shared report-viewer component, following the existing module/project boundaries exactly as laid out in `CLAUDE.md`.

## Complexity Tracking

> No constitution violations identified — section intentionally left without entries.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
