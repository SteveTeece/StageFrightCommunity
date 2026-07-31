# Implementation Plan: AGM Workflow

**Branch**: `013-agm-workflow` | **Date**: 2026-07-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/013-agm-workflow/spec.md`

## Summary

Coordinators today record an Annual General Meeting as a generic `Event`, then edit committee members one at a time to record election outcomes, and manually click a "reset committee" button each year — three disconnected steps built around a bare `(MemberId, Year)` committee-position row. This feature replaces all three with a single "Record AGM" screen (attendance + President/Secretary/Treasurer + configurable office-holder titles + general-committee seats, saved atomically) and a real AGM-to-AGM term model: a new `AnnualGeneralMeeting`/`AgmAttendanceRecord`/`CommitteeOfficeHolderType`/`CommitteeTerm` set of entities, plus the existing `CommitteeMembership` renamed and extended to `CommitteePositionRecord` with start/end dates so mid-term replacements (special elections) can be represented without losing history. Saving a new AGM automatically closes the previously-open term and opens a new one — this single mechanism is what makes the old manual "reset" button obsolete, so it and its banner are removed outright rather than kept alongside the new flow. The Committee Report, the Settings page (new Committee tab), and the first-run setup wizard (new step, plus reuse of the existing `CommitteeRenewalMonth` field as "AGM month") are all updated to match.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — no changes since first pass.*

| Principle | Assessment |
|---|---|
| §3.2.1 / §4.5 One class per file | PASS — every new entity, request record, service, exception usage, and Blazor component gets its own file, matching the module layout in [contracts/agm-workflow-contract.md](./contracts/agm-workflow-contract.md). |
| §3.4 / §3.5 Soft-delete everywhere | PASS — `AnnualGeneralMeeting`, `AgmAttendanceRecord`, `CommitteeOfficeHolderType`, `CommitteePositionRecord` all carry `IsDeleted`/`DeletedAt`/`DeletedBy` (FR-017). `CommitteeTerm` has no independent archive action — it's closed (not deleted) only as a side effect of the next AGM, consistent with the entity being a derived boundary marker, not user-facing archivable data. |
| §4.1 Vertical slice modules | PASS — new `Agm` module for AGM/attendance orchestration; the committee-position-record rework stays in the existing `Members` module where `CommitteeService` already lives (research D2), avoiding needless churn while still respecting slice ownership. |
| §4.6 Navigation menu system | PASS — "Record AGM"/"Past AGMs" added as `SubItems` under the existing Events menu entry (FR-001's "from the Events menu"); no new top-level module menu needed. |
| §4.7 Blazor code-behind + CSS isolation | PASS — every new `.razor` gets a paired `.razor.cs`; the one new `.razor.css` (the AGM attendance grid's independent-scroll container, FR-005) is justified per §4.7.2 since no existing global-stylesheet pattern covers non-paged, internally-scrolling grids (research D5). |
| §5 Custom exceptions at boundaries | PASS — reuses existing `ValidationException`/`EntityNotFoundException`/`DataIntegrityException`; no new exception types needed (research D10). |
| §7.2 RadzenDataGrid only, no plain tables | PASS — attendance grid, past-AGM list, and office-holder-title management grid are all `RadzenDataGrid`. |
| §11 Exhaustive test-path coverage | PASS — plan carries unit (Core.Tests), bUnit (UI.Tests), and integration (`V18_AgmWorkflowTests.cs`) coverage for every new service/component, following the codebase's actual naming practice (research D12) rather than the constitution's literal template where the two already diverge. |
| Finance/GL integrity (§ CLAUDE.md) | N/A — this feature is non-financial; no GL transactions are touched. |

No violations — Complexity Tracking table omitted.

## Project Structure

### Documentation (this feature)

```text
specs/013-agm-workflow/
├── plan.md              # This file
├── research.md           # Phase 0 — 12 decisions resolving every open design question
├── data-model.md         # Phase 1 — 5 entities (4 new, 1 extended), 2 removed services
├── contracts/
│   └── agm-workflow-contract.md   # Phase 1 — routes, menu, service signatures
└── checklists/
    └── requirements.md
```

### Source code (repository root)

```text
src/StageFright.Core/
├── Entities/
│   ├── AnnualGeneralMeeting.cs          # new
│   ├── AgmAttendanceRecord.cs           # new
│   ├── CommitteeOfficeHolderType.cs     # new
│   ├── CommitteeTerm.cs                 # new
│   └── CommitteePositionRecord.cs       # renamed + extended from CommitteeMembership.cs
├── Modules/
│   ├── Agm/                             # new module
│   │   ├── AgmService.cs
│   │   ├── RecordAgmRequest.cs
│   │   └── RecordSpecialElectionRequest.cs
│   ├── Members/
│   │   ├── CommitteeService.cs          # extended (GetCurrentAsync/GetByTermAsync/GetByAgmAsync; SoftDeleteCurrentYearAsync removed)
│   │   ├── CommitteeOfficeHolderTypeService.cs   # new
│   │   └── CommitteeAnnualResetService.cs        # DELETED
│   └── Events/
│       ├── EventsMenuItemProvider.cs    # extended (SubItems)
│       └── EventTypeService.cs          # extended (GetSelectableForNewEventsAsync)
└── Contracts/
    ├── IAgmService.cs                   # new
    ├── ICommitteeOfficeHolderTypeService.cs      # new
    ├── ICommitteeService.cs             # extended
    └── ICommitteeAnnualResetService.cs  # DELETED

src/StageFright.Data/
├── Configurations/
│   ├── AnnualGeneralMeetingConfiguration.cs      # new
│   ├── AgmAttendanceRecordConfiguration.cs       # new
│   ├── CommitteeOfficeHolderTypeConfiguration.cs # new
│   ├── CommitteeTermConfiguration.cs             # new
│   └── CommitteePositionRecordConfiguration.cs   # renamed + extended
├── Repositories/
│   ├── AgmRepository.cs, AgmAttendanceRepository.cs, CommitteeOfficeHolderTypeRepository.cs, CommitteeTermRepository.cs  # new
│   └── CommitteeMembershipRepository.cs → CommitteePositionRecordRepository.cs  # renamed + extended
└── Migrations/
    └── <timestamp>_AddAgmWorkflow.cs    # new tables + CommitteeMembership rename/extend + Settings.LastCommitteeResetYear drop

src/StageFright.UI/Pages/
├── Events/
│   ├── RecordAgm.razor(.cs)             # new
│   ├── AgmList.razor(.cs)               # new
│   ├── AgmDetail.razor(.cs)             # new
│   ├── AgmAttendanceGrid.razor(.cs)(.css) # new — the one .razor.css this feature needs
│   └── RecordSpecialElection.razor(.cs) # new
├── Settings/
│   ├── SettingsPage.razor(.cs)          # extended (5th tab)
│   ├── CommitteeSettingsTab.razor(.cs)  # new
│   └── GeneralSettingsTab.razor(.cs)    # extended (AGM-month label; banner code removed)
└── Setup/
    ├── SetupWizard.razor(.cs)           # extended (step 4→5)
    └── SetupFormModel.cs                # extended (committee title list, seat count, AGM month)

src/StageFright.Reports/Providers/
└── CommitteeReportProvider.cs           # reworked in place (term-keyed grouping, multi-holder date display)

tests/StageFright.Core.Tests/Modules/
├── Agm/AgmServiceTests.cs                                # new
├── Members/CommitteeOfficeHolderTypeServiceTests.cs      # new
├── Members/CommitteeServiceTests.cs                      # extended
└── Members/CommitteeAnnualResetServiceTests.cs           # DELETED

tests/StageFright.UI.Tests/Pages/Events/
├── AgmAttendanceGridTests.cs, RecordAgmTests.cs, AgmListTests.cs, AgmDetailTests.cs  # new

tests/StageFright.Integration.Tests/Scenarios/
├── V18_AgmWorkflowTests.cs              # new
└── V13_CommitteeResetAgmBannerTests.cs  # DELETED

tests/StageFright.Reports.Tests/
└── CommitteeReportProviderTests.cs      # extended
```

**Structure Decision**: New `Agm` vertical-slice module (per constitution §4.1) for the AGM/attendance orchestration that has no existing home; the committee-position-record model rework stays in the existing `Members` module where it already lives, extended rather than relocated. Menu integration happens through the existing `EventsMenuItemProvider` (route strings only — no cross-module C# dependency). Settings and Setup wizard are extended in place, matching every existing sibling tab/step. See [contracts/agm-workflow-contract.md](./contracts/agm-workflow-contract.md) for the full route/interface list and [research.md](./research.md) for why each of these placements was chosen over the alternatives considered.
