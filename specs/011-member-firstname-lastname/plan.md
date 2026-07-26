# Implementation Plan: Split Member Name into First Name and Last Name

**Branch**: `011-member-firstname-lastname` | **Date**: 2026-07-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/011-member-firstname-lastname/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Replace the single `Member.Name` string with two required fields, `FirstName` and `LastName` (100 chars max each), used everywhere a member's name is captured, searched, sorted, or displayed. A hand-written EF Core migration adds the two columns, backfills them from the existing `Name` value using a trim/collapse-whitespace-then-split-on-first-space rule (truncating to 100 chars if needed, leaving `LastName` blank for single-word names), and drops the old `Name` column. A derived, read-only `Member.FullName` computed property (`"{FirstName} {LastName}"` for entry contexts, formatted as `"{LastName}, {FirstName}"` for sorted lists/reports per FR-005) replaces every direct `member.Name` read across `StageFright.Core`, `StageFright.Data`, `StageFright.UI`, and `StageFright.Reports`. `MemberService.UpdateAsync` is extended to capture old/new FirstName/LastName values for the audit trail (FR-011), following the existing `AccountService.UpdateAsync` pattern.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`)

**Primary Dependencies**: .NET MAUI Blazor Hybrid, EF Core (SQLite provider), Radzen.Blazor (`RadzenDataGrid`), BlazorBootstrap, QuestPDF (PDF reports), CsvHelper (CSV export), Serilog

**Storage**: SQLite (`TestData/stagefright.db`), EF Core Code-First migrations (`src/StageFright.Data/Migrations/`)

**Testing**: xUnit (unit + integration), bUnit (Blazor component tests), EF Core against SQLite in-memory/file connections for `StageFright.Data.Tests` and `StageFright.Integration.Tests`

**Target Platform**: Windows desktop and macOS desktop (MAUI), Blazor Hybrid hosting model

**Project Type**: Desktop app — single .NET solution (`StageFrightCommunity.slnx`) with layered projects (App/Core/Data/Reports/UI/Plugins.Contracts) and five parallel test projects; no separate frontend/backend split

**Performance Goals**: No new performance targets; SC-004 requires the two-field Add/Edit Member flow to take no longer (time-on-task) than the current single-field flow — purely a UX/tab-order concern, not a throughput concern

**Constraints**: Zero data loss/corruption during the one-time conversion (FR-007, SC-001); migration must run safely against SQLite (no unsupported `ALTER COLUMN`, so old-column drop uses the table-rebuild EF Core already generates for SQLite); soft-deleted/inactive members must be converted identically to active ones; must satisfy the constitution's one-class-per-file, code-behind-only Blazor, and custom-exception-boundary rules

**Scale/Scope**: Single entity change (`Member`) rippling through ~1 EF configuration, ~1 migration, 1 repository (no `Name`-specific methods to change), 1 service + 2 request DTOs, ~7 Razor UI pages/components (`MemberForm`, `MemberList`, `MemberDetail`, `EventDetail`, `ParticipationGrid`, `AttendanceGrid`, `MemberBalanceList`, `PaymentForm`), 3 report providers (`MemberList`, `MemberAccountSummary`, `Committee`), and ~29 existing test files across 5 test projects (per research.md §9) — sized for a community group's member roster (tens to low hundreds of records), not a high-volume dataset

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Status | Notes |
|---|---|---|
| §3.2.1 / §4.5 One Class Per File | **PASS** | No new types beyond what's needed; `Member.FullName` is a computed property on the existing `Member.cs`, not a new file. Any new custom exception (if introduced) gets its own file under `StageFright.Core/Exceptions/`. |
| §3.4 / §3.5 Soft-Delete & Member Data Preservation | **PASS** | Migration converts `Name` in place for every row (active, inactive, and soft-deleted) via SQL `UPDATE`, never deletes/recreates member rows; FR-007/FR-008 explicitly require this. No hard deletes introduced. |
| §3.6 Financial Corrections Pattern | **N/A** | Feature touches display/search of member names only; no `Fee`/`Payment`/`Transaction` record is created, edited, or deleted. `PaymentService`'s use of `member.Name` in GL transaction `Description` strings switches to `member.FullName` — text only, not a financial-value change. |
| §4.1 Vertical Slice Module Architecture | **PASS** | All service/DTO changes stay inside `StageFright.Core/Modules/Members/`; repository stays in `StageFright.Data/Repositories/` per the existing spec-mandated deviation (FR-042) already documented in CLAUDE.md. |
| §4.7 Blazor Component Patterns (code-behind, no inline `@code`) | **PASS** | `MemberForm.razor`/`.razor.cs` gets a second input + label; no inline `@code` introduced; no new `.razor.css` needed (reuses existing form field styling). |
| §5 Custom Exceptions at Boundaries | **PASS** | Existing `ValidationException` covers the new required-field/max-length checks for `FirstName`/`LastName` (same shape as today's `Name` check in `MemberValidationService`); no new raw framework exception crosses a boundary. |
| §11 Exhaustive Test Coverage | **PASS (planned)** | research.md §9 enumerates every existing test file touching `Member.Name`; tasks.md (Phase 2) must update each plus add new tests for: split-on-conversion edge cases (FR-006/FR-008), per-field max-length validation (FR-009), audit trail capture (FR-011), and search/sort by first/last/full name (FR-004/FR-005). |
| §7.1/§7.3 Tech Stack / No Custom JS | **PASS** | Pure Razor/C# change; no JavaScript involved. |

No violations requiring Complexity Tracking justification.

**Post-Phase-1 re-check** (after research.md/data-model.md/contracts/quickstart.md were written):
All gates above still **PASS** — the design added one new file (`MemberNameSplitter.cs`, one
class per §3.2.1/§4.5), two computed properties on the existing `Member` entity (no new file),
and a hand-written SQL migration following the `ConvertCategoriesToAccounts` precedent; no new
cross-module coupling, no new plug-in surface, no financial-record mutation, and the audit-trail
gap closed for `MemberService.UpdateAsync` (research.md Decision 8) strengthens rather than
weakens §5/§11 compliance. No Complexity Tracking entries required.

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
├── StageFright.App/                          # MAUI Blazor Hybrid host (composition root — untouched by this feature)
├── StageFright.Core/
│   ├── Entities/
│   │   └── Member.cs                         # Name -> FirstName + LastName + FullName (computed)
│   └── Modules/Members/
│       ├── MemberService.cs                  # Create/Update mapping, audit old/new value capture
│       ├── MemberValidationService.cs        # Required + max-length checks per field
│       ├── CreateMemberRequest.cs
│       └── UpdateMemberRequest.cs
├── StageFright.Data/
│   ├── Configurations/MemberConfiguration.cs # FirstName/LastName HasMaxLength(100), IsRequired
│   ├── Migrations/
│   │   └── <timestamp>_SplitMemberNameIntoFirstLastName.cs
│   └── Repositories/MemberRepository.cs      # No Name-specific query methods to change
├── StageFright.Reports/Providers/
│   ├── MemberListReportProvider.cs           # Sort + FullName ("Last, First") column
│   ├── MemberAccountSummaryReportProvider.cs # Sort + FullName section/summary label
│   └── CommitteeReportProvider.cs            # Sort + FullName in per-position member lists
└── StageFright.UI/
    ├── Pages/Members/
    │   ├── MemberForm.razor(.cs)             # Two inputs: First Name, Last Name
    │   ├── MemberList.razor(.cs)             # Grid columns, search-by-first/last/full
    │   └── MemberDetail.razor                # Header uses FullName
    ├── Pages/Events/
    │   ├── EventDetail.razor                 # Participation grid FullName
    │   └── ParticipationGrid.razor.cs
    ├── Pages/Rehearsals/AttendanceGrid.razor(.cs)
    └── Pages/Finance/
        ├── MemberBalanceList.razor           # via MemberBalanceService.MemberBalance.Name -> FullName
        └── PaymentForm.razor.cs

tests/
├── StageFright.Core.Tests/Modules/{Members,Finance}/    # Service, validation, MemberBalance tests
├── StageFright.Data.Tests/{Repositories,BackupImportTests.cs,...}
├── StageFright.Reports.Tests/{MemberListReportProviderTests.cs,MemberAccountSummaryReportProviderTests.cs,CommitteeReportProviderTests.cs}
├── StageFright.UI.Tests/Pages/{Members,Events,Rehearsals,Finance}/
└── StageFright.Integration.Tests/Scenarios/{V2_MemberManagementTests.cs,V3_RehearsalAttendanceTests.cs,V4_AnnualFeeApplicationTests.cs,V5_PaymentsTests.cs,V5_EventsParticipationTests.cs,V6_AccountingReportsTests.cs,V9_BackupRestoreTests.cs,V12_ReactivationForgivenessTests.cs,V13_CommitteeResetAgmBannerTests.cs}
```

**Structure Decision**: Existing single-solution, layered-project structure (per CLAUDE.md's "Project layout") is unchanged — this feature modifies one entity and its ripple effects within the existing `StageFright.Core` / `StageFright.Data` / `StageFright.Reports` / `StageFright.UI` projects and their matching test projects. No new projects, modules, or directories are introduced; see [data-model.md](./data-model.md) for the full field-level design and [research.md](./research.md) for the file-by-file inventory backing this tree.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
