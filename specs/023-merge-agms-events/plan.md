# Implementation Plan: AGMs on the All Events List

**Branch**: `023-merge-agms-events` | **Date**: 2026-08-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/023-merge-agms-events/spec.md`

## Summary

The All Events screen (`/events`) currently lists only `Event` rows; Annual General Meetings live solely on the separate `/events/agm` screen, so there is no single chronological record of everything the group has held. This plan adds a read-only merge layer: a new Core service combines the existing `IEventService.GetAllAsync()` and `IAgmService.GetAllAsync()` results into one date-sorted list of row view-models, and `EventList.razor`/`EventList.razor.cs` render that merged list in the existing `RadzenDataGrid`, branching per row kind so an AGM row shows AGM-appropriate status/actions and routes to the AGM's own detail screen instead of the generic event detail screen. No data model, storage, or existing dedicated-screen behavior changes — this is purely a new read/search projection over two already-existing, already-filtered data sources.

## Project Structure

### Documentation (this feature)

```text
specs/023-merge-agms-events/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── contracts/            # Phase 1 output
│   └── combined-event-list.md
└── tasks.md              # Phase 2 output (NOT created by this command)
```

### Source Code (repository root)

```text
src/StageFright.Core/
├── Contracts/
│   └── ICombinedEventListService.cs        # NEW — read-only merge contract
└── Modules/Events/
    ├── CombinedEventListItem.cs            # NEW — merged row view-model (one Event or AGM)
    ├── CombinedEventListItemKind.cs        # NEW — Event | Agm discriminant enum
    └── CombinedEventListService.cs         # NEW — merges IEventService + IAgmService results

src/StageFright.UI/Pages/Events/
├── EventList.razor                         # MODIFIED — grid TItem becomes CombinedEventListItem,
│                                            #   per-kind status/actions branches
└── EventList.razor.cs                      # MODIFIED — injects ICombinedEventListService plus
│                                            #   IAgmAttendanceSheetService/IAgmAttendanceSheetPdfRenderer
│                                            #   for the AGM Print action

src/StageFright.App/
└── MauiProgram.cs                          # MODIFIED — registers ICombinedEventListService (AddScoped)

tests/StageFright.Core.Tests/Modules/Events/
└── CombinedEventListServiceTests.cs        # NEW — merge/sort/route-mapping unit coverage

tests/StageFright.UI.Tests/Pages/Events/
└── EventListTests.cs                       # MODIFIED — combined rendering, AGM row status/actions,
                                             #   search-across-both-kinds, empty-state cases

tests/StageFright.Integration.Tests/Scenarios/
└── V20_CombinedEventsListTests.cs          # NEW — end-to-end journey: seed an Event + an AGM,
                                             #   confirm both appear, select the AGM row, confirm it
                                             #   opens the AGM detail screen (next free Vn slot after V19)
```

**Structure Decision**: All new code lives inside the existing `Events` module slice (`StageFright.Core/Modules/Events/`) and its existing UI page (`EventList.razor`/`.razor.cs`) — no new module, no new route. The merge service depends only on the already-published `IEventService` and `IAgmService` contracts (never on `AgmService`/`EventService` concretes), keeping the `Agm` and `Events` modules decoupled per §4.1 of the constitution. The dedicated `/events/agm` screen family (`AgmList`, `AgmDetail`, `RecordAgm`, `ScheduleAgm`) is untouched, satisfying FR-012.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS, no changes below.*

| Principle | Assessment |
|---|---|
| §3.2.1 One Class Per File | PASS — `CombinedEventListItem`, `CombinedEventListItemKind`, `ICombinedEventListService`, `CombinedEventListService` each get their own file. |
| §4.1 No Cross-Module Dependencies | PASS — `CombinedEventListService` (Events module) depends only on the published `IEventService`/`IAgmService` interfaces from `Contracts/`, never on the `Agm` module's concrete `AgmService`. |
| §4.1 Repositories Centralized, Not Module-Owned | PASS — no new repository; the merge reuses `IEventRepository`/`IAgmRepository` transitively via their existing services. |
| §4.5/§4.7 Blazor code-behind pattern | PASS — all new C# logic lives in `EventList.razor.cs`; `EventList.razor` gains only markup and `@if` branches, no `@code` block. |
| CLAUDE.md Data grid standards | PASS — the merged grid keeps `AllowSorting="true" AllowPaging="true" PageSize="15" class="rz-shadow-0"`, matching the Members reference grid and today's `EventList`. |
| §3.4 Soft-Delete / Query Filtering | PASS — reuses `IEventService.GetAllAsync()` and `IAgmService.GetAllAsync()`, both already scoped by the existing EF Core `!IsDeleted` query filters (`EventConfiguration`, `AnnualGeneralMeetingConfiguration`); no new filtering logic needed for FR-010. |
| §5 Custom Exceptions at Boundaries | PASS — `CombinedEventListService` is read-only and composes two already exception-safe service calls; it introduces no new DAL access and therefore no new exception-translation surface. |
| §11 Testing Standards | PLANNED — Phase 2 tasks add Core unit tests (merge/sort/route mapping), bUnit tests (per-kind rendering, search, actions), and a new integration scenario test; see Project Structure above. |

No constitution violations were identified — **Complexity Tracking is omitted**.
