# Implementation Plan: Committee Report Year Summary

**Branch**: `010-committee-report-year-summary` | **Date**: 2026-07-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/010-committee-report-year-summary/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

The Committee Report currently renders one flat row per (Member, Year, Position) record. This feature regroups it into one section per year (most recent first), each collapsed to a summary row showing the year and the count of committee positions recorded that year, expandable to a role breakdown: President/Secretary/Treasurer (or "Vacant"), every other distinct non-blank position label as its own alphabetical line, and a "General Committee Members" catch-all for blank positions — all matched case-insensitively and trimmed, with multiple members on the same position line listed together alphabetically. This reuses the master-detail `ReportData.SummaryColumns` / `ReportSection.SummaryRow` extension already built for spec 005 (Member Account Summary) — `ReportViewer`, `PdfReportRenderer`, and `CsvReportExporter` need no changes. The change is confined to `CommitteeReportProvider.GenerateAsync` plus its new/updated tests.

## Technical Context

**Language/Version**: C# 14 (.NET, MAUI Blazor Hybrid)

**Primary Dependencies**: Existing `StageFright.Reports` pipeline only — `IReportProvider`, the already-built `ReportData.SummaryColumns` / `ReportSection.SummaryRow` master-detail model (added in spec 005), `Radzen.Blazor` (`RadzenDataGrid` master-detail rendering in `ReportViewer`, unchanged), `PdfReportRenderer` (QuestPDF), `CsvReportExporter` (CsvHelper). No new packages.

**Storage**: SQLite via EF Core (`StageFrightDbContext`) — read-only for this feature; no schema/migration changes. Uses only the existing `ICommitteeMembershipRepository.GetByMemberAsync` and `IMemberRepository` methods already called by the current provider.

**Testing**: xUnit (`StageFright.Reports.Tests`) for the provider's grouping/role-breakdown logic; existing integration test (`StageFright.Integration.Tests`) updated for the new row shape. No bUnit changes needed — `ReportViewer` already renders any `SummaryColumns`-bearing report generically.

**Target Platform**: Windows desktop and macOS desktop (MAUI Blazor Hybrid), per constitution §7.1

**Project Type**: Desktop app (existing solution; no new projects)

**Performance Goals**: No new performance requirement; report generation remains synchronous with the existing 5-second cancel-button threshold in `ReportViewer`

**Constraints**: No custom JavaScript (constitution §7.3); one class per file; no new custom exception types (no new I/O — same repository calls as today, only the in-memory aggregation changes); soft-delete conventions unaffected (feature is entirely read-only over already soft-delete-aware repository methods)

**Scale/Scope**: One provider file (`CommitteeReportProvider.cs`), one new test file (`CommitteeReportProviderTests.cs` — did not previously exist), one existing integration test updated (`V11_ReportsMenuTests.cs`, which asserts `Cells[0]` against the old flat row shape). No changes to `StageFright.Reports.Models`, `ReportViewer.razor(.cs)`, `PdfReportRenderer`, or `CsvReportExporter`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **§3.4/§3.5/§3.6 Soft Delete / Financial Data**: PASS. Feature is entirely read-only reporting over `ICommitteeMembershipRepository.GetByMemberAsync` (already non-deleted-only) and existing `IMemberRepository` status queries; no writes, no financial entities touched.
- **§4.1 Vertical Slice Module Architecture**: PASS (no change). Report providers live centrally in `StageFright.Reports/Providers/` per the CLAUDE.md-documented deviation for the reports pipeline; this feature does not alter that boundary.
- **§4.5/§4.7 Code Organization / Blazor Component Patterns**: PASS. No new or modified `.razor`/`.razor.cs` files — `ReportViewer` already renders master-detail reports generically via `ReportData.SummaryColumns`. The one changed C# type (`CommitteeReportProvider`) keeps its existing one-class-per-file placement.
- **§7.2/§7.3 Architecture Requirements / Prohibited**: PASS. Reuses the existing approved `RadzenDataGrid` master-detail rendering; no custom JavaScript introduced.
- **§9/§10 Spec & Plan Requirements**: PASS. Spec documents purpose, scope, the reused `ReportData`/`ReportSection` contract, dependencies, constraints, and acceptance criteria; no hidden side effects or global state.
- **§11 Testing Standards**: PASS (tracked as task work, not yet written). New `CommitteeReportProviderTests.cs` must cover: year grouping/ordering (FR-001), summary count (FR-002), named-role display and vacancy (FR-003–005), other-position lines and ordering (FR-006), General Committee Members grouping (FR-006a), case/whitespace-insensitive matching (FR-007), the existing Member Status filter (FR-008), year omission when empty (FR-009), duplicate-role members (FR-010), empty-state (FR-012), and CSV/PDF row content preserving year+role info (FR-011, US3). `V11_ReportsMenuTests.cs`'s `CommitteeReport_DefaultFilter_ReturnsActiveOnly` test must be updated for the new row shape (see Project Structure below).

No violations requiring `Complexity Tracking` justification.

## Project Structure

### Documentation (this feature)

```text
specs/010-committee-report-year-summary/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/StageFright.Reports/Providers/
└── CommitteeReportProvider.cs
    # Rewritten GenerateAsync: aggregate filtered members' committee memberships,
    # group by Year (desc), build a role-breakdown detail table per year
    # (Year/Position/Member(s) rows) plus a SummaryRow (Year, Positions Recorded),
    # and populate ReportData.SummaryColumns — same pattern already used by
    # MemberAccountSummaryReportProvider (spec 005).

tests/StageFright.Reports.Tests/
└── CommitteeReportProviderTests.cs   # New file — full FR/edge-case coverage

tests/StageFright.Integration.Tests/Scenarios/
└── V11_ReportsMenuTests.cs
    # CommitteeReport_DefaultFilter_ReturnsActiveOnly currently asserts
    # Cells[0] == member name (old [Member, Year, Position] row shape).
    # Updated to assert against the new [Year, Position, Member(s)] row shape.
```

No changes to `src/StageFright.Reports/Models/*` (`SummaryColumns`/`SummaryRow` already exist), `src/StageFright.UI/Shared/ReportViewer.razor(.cs)`, `PdfReportRenderer.cs`, or `CsvReportExporter.cs` — all four already handle any `IReportProvider` that populates `SummaryColumns`/`SummaryRow`/`Rows` generically, and neither renderer reads `SummaryColumns`/`SummaryRow` (confirmed by reading both files), so PDF/CSV export continues to work unmodified as long as each detail row carries its own Year value (see `data-model.md`).

**Structure Decision**: This feature extends the existing single-solution desktop app (`StageFrightCommunity.slnx`); no new projects. The change is confined entirely to one existing provider class in `StageFright.Reports` plus its tests, following the existing module/project boundaries laid out in `CLAUDE.md`.

## Complexity Tracking

> No constitution violations identified — section intentionally left without entries.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
