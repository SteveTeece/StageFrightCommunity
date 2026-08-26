# Phase 0 Research: AGMs on the All Events List

All decisions below were resolved by reading the existing `Events`/`Agm` module code (`EventList.razor(.cs)`, `AgmList.razor(.cs)`, `EventService`, `AgmService`, their repositories and EF Core configurations) rather than by introducing anything new to the stack — this feature has no unknown technology, only a design choice about where the merge logic lives.

## Decision: Build the merge as a new Core read-model service, not inline in the component

**Decision**: Introduce `ICombinedEventListService` / `CombinedEventListService` in the `Events` module (`StageFright.Core/Modules/Events/`), depending only on the already-published `IEventService` and `IAgmService` contracts.

**Rationale**: This mirrors the existing `IEventAttendanceSheetService` pattern — a read-only application service in the module folder that composes data from repositories/other services into a display DTO. It keeps the merge, type-label, status, and (safety-critical) routing rules unit-testable in `StageFright.Core.Tests`, and satisfies constitution §4.1 ("No Cross-Module Dependencies") by depending on interfaces, never on the sibling `Agm` module's concrete `AgmService`.

**Alternatives considered**: Merging `IEventService.GetAllAsync()` and `IAgmService.GetAllAsync()` directly inside `EventList.razor.cs` and zipping the two lists in the component. Rejected — it would duplicate FR-004/FR-006 mapping logic (AGM type label, detail-route selection) in presentation code with no Core-level test seam, and FR-006's "must never route an AGM row to the generic event detail screen" requirement deserves a direct unit-test assertion rather than relying solely on bUnit's rendered-markup inspection.

## Decision: Reuse `IAgmService.GetAllAsync()` and `IEventService.GetAllAsync()` as-is; no new repository methods

**Decision**: The merge service calls the two existing methods unchanged. No new `IAgmRepository`/`IEventRepository` methods are needed.

**Rationale**: Reading `AgmRepository.GetPastOrderedAsync()` shows it has no date predicate at all — despite the "Past" name, it returns every non-deleted AGM (past or future-scheduled) ordered by date descending, via `_db.AnnualGeneralMeetings.Include(a => a.AttendanceRecords).OrderByDescending(a => a.Date)`. That already matches acceptance scenario 2 of Story 1 ("an AGM has been scheduled but not yet recorded... still appears in the list"). Both `AnnualGeneralMeetingConfiguration` and `EventConfiguration` already declare `HasQueryFilter(x => !x.IsDeleted)`, so both source methods already exclude archived records — FR-010 is satisfied for free, no new filtering logic required.

**Alternatives considered**: Adding a new `GetAllIncludingUpcomingAsync()` to `IAgmService`/`IAgmRepository` for clarity. Rejected as unnecessary — the existing method's behavior (verified by reading its implementation, not just its name) already does exactly what's needed; adding a second method would be pure duplication.

## Decision: `CombinedEventListItem` carries raw values, not pre-formatted display strings

**Decision**: The DTO exposes `Date` (`DateTime`), `Notes` (`string?`), `TypeName` (`string`), `Kind` (enum), `ParticipationRate` (`decimal?`, events only), `IsAgmRecorded` (`bool?`, AGMs only), and a precomputed `DetailUrl` (`string`) — not formatted strings like `"85.0%"` or `"Recorded"`.

**Rationale**: Date formatting (`"d MMM yyyy"`), the `%` suffix, and the `Recorded`/`bg-success`/`bg-warning` badge markup already live correctly in `EventList.razor`'s and `AgmList.razor`'s templates today. Keeping those in Razor means the merge only changes *what* feeds the grid, not *how* each cell renders — the smallest diff consistent with "Simple over clever code."

**Alternatives considered**: A fully pre-formatted "display row" with a ready-to-render string per cell. Rejected — it would duplicate formatting logic the two existing templates already have correct and tested, and would push presentation concerns into `StageFright.Core`, which the constitution's Separation of Concerns principle (§3.3) reserves for the UI/presentation layer.

## Decision: Precompute `DetailUrl` (and `Kind`) in the Core service, not via a Razor conditional

**Decision**: `CombinedEventListService` sets `DetailUrl` to `$"/events/{Id}"` for Event rows and `$"/events/agm/{Id}"` for AGM rows; `EventList.razor` just renders `@item.DetailUrl` in the row's anchor.

**Rationale**: FR-006 is explicitly safety-critical — an AGM row must never open the generic event detail screen. Putting the exact route string behind a Core-level unit test (asserting the literal `/events/agm/{id}` vs `/events/{id}` string per kind) closes that gap directly, rather than relying only on a bUnit assertion against rendered HTML.

**Alternatives considered**: Branching on `item.Kind` inline in the Razor markup to build the href. Workable, but leaves the safety-critical route construction untested outside of bUnit; rejected in favor of the extra, cheap unit-test seam.

## Decision: Status/Actions columns keep per-kind `@if` branches in `EventList.razor`, no new shared component

**Decision**: The Status and Actions columns each branch on `item.Kind`, carrying over the existing `EventList`/`AgmList` markup for each kind essentially unchanged (event: participation rate or "Not recorded" / "Record Participation" + "Print"; AGM: "Recorded"/"Scheduled" badge / "Record" link + "Print").

**Rationale**: FR-005 and FR-007 want each row kind to look and act exactly as its own dedicated screen already does — copying the existing, already-correct markup into per-kind branches is the lowest-risk way to guarantee that, and two call sites (Event, AGM) don't yet justify a new shared render-fragment abstraction.

**Alternatives considered**: Extracting a `CombinedEventRowActions` shared component. Rejected as premature abstraction for a single consuming grid; revisit only if a third combined-kind list appears later.

## Decision: `CombinedEventListItemKind` lives in `Modules/Events/`, not the shared `Enums/` folder

**Decision**: The `Event | Agm` discriminant enum is a new file in `StageFright.Core/Modules/Events/`, alongside `CombinedEventListItem`.

**Rationale**: It's a display-only discriminant scoped to this one read-model, matching the module-slice convention that a module's request/response DTOs live with that module — unlike the domain-wide enums in `StageFright.Core/Enums/` (`AccountType`, `PaymentMethod`, etc.) that are referenced by persisted entities across modules.

**Alternatives considered**: Placing it in `Enums/` for consistency with other enums. Rejected — it isn't a persisted or cross-module domain concept, just this feature's row-kind tag.
