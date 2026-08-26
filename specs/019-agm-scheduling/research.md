# Research: Schedule Future AGMs

**Feature**: `019-agm-scheduling` | **Phase**: 0 (Research) | **Input**: [spec.md](./spec.md)

No `NEEDS CLARIFICATION` markers remain in spec.md — every decision below is resolved directly
from existing code precedent (chiefly spec 012's Rehearsal schedule/record split) rather than
requiring a new clarification round.

## Decision 1 — Track "scheduled vs. recorded" with a new `IsRecorded` flag, not a nullable proxy field

**Decision**: Add `public bool IsRecorded { get; set; }` directly to `AnnualGeneralMeeting`,
defaulting to `false` at scheduling and flipped to `true` (once, permanently) when
`RecordAsync` completes.

**Rationale**: The spec's own Assumptions section is explicit: *"Whether an AGM is 'scheduled' or
'recorded' is tracked directly on the AnnualGeneralMeeting record itself, not inferred from
whether attendance rows exist."* `Rehearsal` uses exactly this kind of proxy today —
`StoredAttendanceRate.HasValue` doubles as its recorded-flag — but that shortcut is unsafe for
AGM: Edge Case 5 requires an AGM recorded with zero active members to read as recorded, not
scheduled, and an attendance-row-count (or a similarly overloaded nullable field) can't
distinguish "zero rows because nobody attended" from "zero rows because nothing was recorded
yet." A dedicated boolean removes the ambiguity outright and matches the spec's explicit
instruction.

**Alternatives considered**:
- Reuse the Rehearsal `StoredAttendanceRate.HasValue` pattern (e.g. a nullable
  `RecordedAt`/`AttendanceRate`-style field on the AGM) — rejected: fails Edge Case 5 for the
  reason above, and the spec explicitly rules out inferring state from row presence.
- Infer "recorded" from `AttendanceRecords.Count > 0` — rejected for the same reason; also
  already contradicted by today's `AgmList.razor` `AttendedCount`/`Count` display, which the spec
  requires to change anyway (Decision 5 below).

## Decision 2 — Split `IAgmService` into `ScheduleAsync` (new) + a re-shaped `RecordAsync` (existing method, changed signature)

**Decision**: `AgmService` gains `ScheduleAsync(ScheduleAgmRequest)` — creates the AGM row only,
mirroring `RehearsalService.ScheduleAsync` exactly (single insert, no attendance/election data).
`RecordAsync` changes from `RecordAsync(RecordAgmRequest)` (which built a brand-new AGM) to
`RecordAsync(Guid agmId, RecordAgmRequest)` (which loads and completes an *existing* scheduled
AGM) — mirroring `AttendanceService.RecordBatchAsync(Guid rehearsalId, ...)`'s existing
id-first shape. `RecordAgmRequest` drops its `Date`/`Notes` fields, since those are already fixed
on the AGM row from scheduling and (per spec Assumptions) can never be edited afterward — only
`AttendedMemberIds`, `AllActiveMemberIds`, `OfficeHolderAssignments`, and
`GeneralCommitteeMemberIds` remain.

**Rationale**: This is the direct, mechanical translation of the Rehearsal precedent the spec's
own `.spec-context.json` constraint names (`RehearsalService.ScheduleAsync` +
`AttendanceService.RecordBatchAsync` date guard) onto the Agm module's existing single service.
Keeping schedule+record in one service (rather than splitting into two services, the way
Rehearsal splits `RehearsalService`/`AttendanceService` across two classes) matches how `AgmService`
is already the sole owner of AGM lifecycle (including `ArchiveAsync` and
`RecordSpecialElectionAsync`) — introducing a second AGM-domain service class for this feature
alone would fragment ownership without the Rehearsal module's original reason (Rehearsal's split
predates this feature and also carries GL/fee side effects AGM attendance never has).

**Alternatives considered**:
- Keep `RecordAsync(RecordAgmRequest)` unchanged and add a *new* `CompleteAsync` method — rejected:
  leaves two overlapping "create/complete an AGM" entry points with confusingly similar
  signatures; renaming the existing method's role (rather than adding a parallel one) is the
  smaller, clearer surface.
- Give `RecordAgmRequest` an `AgmId` field instead of an `agmId` method parameter — rejected: the
  codebase's own closest precedent (`RecordBatchAsync(Guid rehearsalId, IReadOnlyList<...>)`)
  passes the id as a separate parameter, not embedded in the payload record; matching it exactly
  keeps the two "record an already-scheduled thing" call shapes consistent across modules.

## Decision 3 — Reject a second same-year AGM with an application-level check, not a DB constraint

**Decision**: Add `IAgmRepository.ExistsForYearAsync(int year)` (`AnyAsync(a => a.Date.Year ==
year)`, relying on the existing global `!IsDeleted` query filter to naturally exclude archived
AGMs). `AgmService.ScheduleAsync` calls it before opening the write transaction and throws
`ValidationException` if it returns `true` — the same "check outside the transaction, then write
inside it" shape `RecordAsync`'s existing duplicate-assignment check already uses.

**Rationale**: Matches this codebase's established convention for business-rule rejection
(app-level `ValidationException`, not a SQL `UNIQUE` constraint) exactly as `RecordAsync`'s
duplicate-committee-assignment check already does in the same service. A real per-year uniqueness
constraint (e.g. a computed/persisted year column with a unique index) would be more robust
against races, but this is a single-user desktop app against a local SQLite file — the same
trust level the duplicate-assignment check already relies on — so introducing new schema
machinery for a case that can't occur in practice would be over-engineering relative to
Constitution §3.1 ("prefer clarity... avoid cleverness").

**Alternatives considered**:
- A EF Core `HasIndex` on a computed year column — rejected: no other business rule in this
  codebase is enforced via a computed/persisted column, and it would need its own migration
  complexity (SQLite has no native computed-column year extraction over a `DateTime` column
  without a trigger or shadow property) for a constraint the app layer already prevents in the
  only code path that can violate it.

## Decision 4 — Scheduled-AGM attendance sheet: extend the existing `AgmAttendanceSheetService`, branching on `IsRecorded`

**Decision**: `AgmAttendanceSheetService.GenerateAsync` keeps its existing signature and keeps
returning the fixed persisted roster when `agm.IsRecorded == true` (unchanged code path). When
`agm.IsRecorded == false`, it instead builds the roster from `IMemberRepository.GetByStatusAsync
(MemberStatus.Active)` (members active *right now*, at print time — not `GetActiveAsOfAsync(agm.Date)`,
per the spec's Edge Cases: "the report always reflects membership as of the moment it is printed,
not the moment the AGM was scheduled"), sorted by surname then first name, every member's
`Attended = false`. `IMemberRepository` is added as a third constructor dependency.

**Rationale**: FR-009 through FR-012 explicitly want one print action that works for both AGM
states and reuses "the AGM attendance report already built by the prior print-reports feature
rather than introducing a new report" (spec Assumptions) — extending the existing service with an
`IsRecorded` branch is the literal reading of that assumption. Using "active right now" rather
than "active as of the AGM's date" is a deliberate divergence from `AttendanceRollService`'s
`GetActiveAsOfAsync(rehearsal.Date)` pattern: a scheduled AGM's date can be arbitrarily far in the
future (or, per spec Assumptions, even in the past for catch-up entry), and the spec's edge case
is unambiguous that the printed roster must track membership at print time, not meeting time —
only the *recorded* roster is a fixed historical snapshot.

**Alternatives considered**:
- A second, new `IScheduledAgmAttendanceSheetService` — rejected: `AgmList`/`AgmDetail`'s print
  handlers already call one service (`IAgmAttendanceSheetService.GenerateAsync(agmId)`) with no
  branching of their own; adding a second interface would push the branch into the UI layer
  instead of keeping it as one cohesive read model, and would duplicate the
  `EntityNotFoundException`/renderer/empty-state plumbing spec 018 already established for this
  exact call site.
- `GetActiveAsOfAsync(agm.Date)` for the scheduled branch, matching `AttendanceRollService`
  exactly — rejected: contradicts the spec's Edge Cases requirement directly (see Rationale).

## Decision 5 — `AgmList`/`AgmDetail`: add a status indicator and a per-AGM "Record" action; page heading and empty-state copy stop implying "past only"

**Decision**: `AgmList.razor` gains a "Status" column ("Scheduled"/"Recorded") and, per row, a
"Record" action when `!agm.IsRecorded` (routing to the new `/events/agm/{id}/record` route,
mirroring `RehearsalList.RecordAttendance`'s unconditional navigate-then-let-the-target-page-guard
shape). The page heading changes from "Past AGMs" to "AGMs" (the underlying repository call
already returns every non-deleted AGM, scheduled or recorded, both today and after this feature —
see Decision 6) and its "Record AGM" button/empty-state link becomes "Schedule AGM"
(`/events/agm/new`). The Attendance column shows "—" instead of "0 of 0" for a still-scheduled row.
`AgmDetail.razor` branches on `_agm.IsRecorded`: `false` shows only date/notes plus a status badge
and a "Record Attendance & Elections" button (routing to the same `/events/agm/{id}/record`
route); `true` keeps today's attendance-count/positions view unchanged. Archive and Print remain
available in both states (unchanged handlers — see Decision 4 for Print, and `ArchiveAsync`
already needs no change since it already tolerates an empty `AttendanceRecords` list).

**Rationale**: Direct translation of FR-003 and FR-008's UI requirements, matching how
`RehearsalList`/`AttendanceGrid` already separate the "browse" and "record" concerns onto
different routes rather than growing one page to do both. Keeping "Record" reachable
unconditionally (not hidden until the date arrives) matches the existing Rehearsal precedent,
where the guard against recording early lives on the target page, not as a disabled/hidden
list-row control — one guard implementation, not two places that must agree.

**Alternatives considered**:
- Hide the per-row "Record" action until `agm.Date <= today` — rejected: would duplicate the
  date-guard logic in two places (list row visibility and the record page's own check) for a
  cosmetic difference in when the action is *offered* rather than *allowed*; the target page's
  existing-pattern message ("not yet due") already communicates this without a second
  implementation to keep in sync.

## Decision 6 — Leave `IAgmRepository.GetPastOrderedAsync`/`IAgmService.GetPastAsync` method names as-is; fix only the stale doc comment and UI copy

> **Superseded (spec 023 / issue #324)**: `IAgmService.GetPastAsync` was later renamed to `GetAllAsync`
> once `CombinedEventListService` took on a second caller depending on its "returns everything"
> contract — the diff-vs-benefit tradeoff below tipped once code, not just documentation, was
> relying on the misleading name. `IAgmRepository.GetPastOrderedAsync` was left as-is; the rename
> was scoped to the `IAgmService` public contract only.

**Decision**: No rename. `GetPastOrderedAsync`'s actual query (`OrderByDescending(a => a.Date)`,
no date filter) already returns every non-deleted AGM today, scheduled-in-the-future ones
included once this feature ships — its *name* has always slightly overstated what it filters, but
its *behavior* needs no code change for this feature. Its XML doc comment gets one added sentence
clarifying that despite the name it returns every non-deleted AGM (including future-dated ones),
ordered most-recent-first.

**Rationale**: A rename here (`GetPastOrderedAsync` → e.g. `GetAllOrderedAsync`, and the matching
`IAgmService.GetPastAsync` → `GetAllAsync`) touches 6 production/test files for a purely cosmetic
accuracy fix with zero behavior change — outsized diff for the benefit, and Constitution §3.1's
"avoid cleverness; prefer clarity" is already satisfied by a one-line doc-comment correction
without the churn. The *user-facing* copy that actually implied "past only" (the `AgmList.razor`
page heading and empty-state text) is corrected regardless (Decision 5), since that is the part a
user actually reads.

**Alternatives considered**: Rename both methods for full internal/external consistency —
rejected per the diff-vs-benefit tradeoff above; revisit only if a future feature needs the
distinction the current name implies but the behavior doesn't provide.

## Decision 7 — `ScheduleAgm` navigates to the AGM detail page after saving, not back to the list

**Decision**: The new `ScheduleAgm.razor` (`/events/agm/new`, replacing today's all-in-one
`RecordAgm.razor` at that route) navigates to `/events/agm/{agm.Id}` after a successful save —
matching what `RecordAgm.razor.cs` already does today (`Nav.NavigateTo($"/events/agm/{agm.Id}")`)
— rather than back to `/events/agm` the way `RehearsalForm.razor.cs` returns to `/rehearsals`.

**Rationale**: Immediately showing the freshly scheduled AGM's detail page is where FR-008's
"only date/notes until recorded" view and the new "Record Attendance & Elections" call-to-action
live — landing there directly confirms the save succeeded and shows the very next action, which a
bare return to the list would not. This keeps the AGM module's own existing post-save convention
rather than importing Rehearsal's, since Rehearsal has no per-item detail page to land on at all.

**Alternatives considered**: Return to `/events/agm` (the list), matching Rehearsal exactly —
rejected: AGM (unlike Rehearsal) already has a detail page purpose-built for this moment; using
it is less work for the user, not more code for us (the route and page already need to exist for
Decision 5 regardless).

## Decision 8 — `GeneralCommitteeSeatCountTarget` stays snapshotted at record time, not schedule time

**Decision**: The `Settings.GeneralCommitteeSeatCountTarget` snapshot onto the AGM row keeps
happening inside `RecordAsync` (unchanged from today), now overwriting the field on the existing
scheduled row rather than setting it at insert time.

**Rationale**: The snapshot only has a consumer at election time — `RecordAgm.razor`'s "Selected: N
of {target} target seats" display — which by this feature's design no longer exists at scheduling
time (scheduling only asks for date/notes). Snapshotting it earlier would capture a number nobody
sees until recording anyway, and settings could plausibly change in the (potentially long) gap
between scheduling and recording; recording time is when the target actually matters and is
displayed, matching the entity doc comment's original intent ("the target as it was at *that
meeting*").

**Alternatives considered**: Snapshot at `ScheduleAsync` time instead — rejected: no UI reads it
before recording, and it would freeze a value that may go stale across a long schedule-to-record
gap for no benefit.
