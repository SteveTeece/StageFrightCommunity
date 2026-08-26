# Contract: AGM Scheduling & Recording (StageFright.Core → StageFright.UI boundaries)

**Feature**: `019-agm-scheduling` | **Phase**: 1 (Design)

This is a desktop MAUI Blazor Hybrid application with no external network API — the relevant
"interfaces exposed to other systems" are the `IAgmService` contract changes this feature
introduces, plus `IAgmAttendanceSheetService`'s changed behavior. Per
`specs/018-event-agm-attendance-reports/contracts/event-agm-attendance-sheet-contract.md`'s
established precedent for this documentation style, this document captures those boundaries
rather than a REST/CLI schema, since none exists in this codebase.

## Changed contract: `IAgmService.ScheduleAsync` (NEW method)

```csharp
Task<AnnualGeneralMeeting> ScheduleAsync(ScheduleAgmRequest request, CancellationToken ct = default);
```

**Preconditions**: `request.Date` may be any date, past or future (spec Assumptions — supports
catch-up entry of a historical AGM never recorded). `request.Notes` may be null or empty.

**Postconditions**:
- Persists a new `AnnualGeneralMeeting` with `Date`, `Notes`, and `IsRecorded = false` (FR-001).
- Creates no `AgmAttendanceRecord`, `CommitteePositionRecord`, or `CommitteeTerm` row (FR-002).
- Logs one `AuditAction.Create` entry against the new AGM's id.
- The returned entity's `Id` is immediately usable with `RecordAsync`/`GetByIdAsync`.

**Failure modes**: `ValidationException(nameof(AnnualGeneralMeeting), nameof(ScheduleAsync))` —
a non-archived `AnnualGeneralMeeting` already exists whose `Date` falls in the same calendar year
as `request.Date` (FR-015). No other exception is raised by this contract; nothing is persisted
on failure.

## Changed contract: `IAgmService.RecordAsync` (signature changed — was `RecordAsync(RecordAgmRequest)`)

```csharp
Task<AnnualGeneralMeeting> RecordAsync(Guid agmId, RecordAgmRequest request, CancellationToken ct = default);
```

**Preconditions**: `agmId` must identify a saved (non-deleted) `AnnualGeneralMeeting`.
`request.AttendedMemberIds` should be a subset of `request.AllActiveMemberIds`.
`request.OfficeHolderAssignments` values and `request.GeneralCommitteeMemberIds` together must
contain no member id more than once (unchanged rule from spec 013).

**Postconditions**:
- Updates the existing AGM row: `IsRecorded = true`, `GeneralCommitteeSeatCountTarget` snapshotted
  from current Settings, `UpdatedAt` refreshed (FR-004). `Date`/`Notes` are untouched.
- Creates one `AgmAttendanceRecord` per `request.AllActiveMemberIds` entry, `Attended` set from
  membership in `request.AttendedMemberIds` (unchanged shape from spec 013).
- Closes any currently-open `CommitteeTerm` (`EndDate = agm.Date`) and opens a new one
  (`StartedByAgmId = agm.Id`, `StartDate = agm.Date`) — unchanged rule (FR-007).
- Creates one `CommitteePositionRecord` per office-holder assignment and per general-committee
  member id — unchanged rule (FR-007).
- Logs one `AuditAction.Update` entry against the AGM's id (was `Create` before this feature,
  since `ScheduleAsync` now owns that event — research.md Decision 2).
- All-or-nothing: every write above happens inside one `IUnitOfWork` transaction; a thrown
  exception leaves the AGM row, attendance, term, and position data exactly as they were before
  the call.

**Failure modes**:
- `EntityNotFoundException(nameof(AnnualGeneralMeeting), agmId, nameof(RecordAsync))` — `agmId`
  does not match any saved AGM.
- `ValidationException(..., nameof(RecordAsync), agmId)` — the AGM has already been recorded
  (FR-006), or its `Date` is still in the future (FR-005).
- `ValidationException(nameof(AnnualGeneralMeeting), nameof(RecordAsync))` — a member id appears
  more than once across `OfficeHolderAssignments`/`GeneralCommitteeMemberIds` (unchanged rule).

## Changed contract: `IAgmAttendanceSheetService.GenerateAsync` (signature unchanged, behavior extended)

```csharp
Task<AgmAttendanceSheetData> GenerateAsync(Guid agmId, CancellationToken ct = default);
```

**Preconditions**: `agmId` should identify a saved (non-deleted) `AnnualGeneralMeeting`. No other
precondition — a scheduled AGM with zero currently-active members, or a recorded AGM with an
empty attendance roster, are both valid input states (FR-012, spec Edge Cases).

**Postconditions**:
- **Recorded AGM** (`IsRecorded == true`, unchanged from spec 018): `Members` is exactly the
  AGM's persisted `AgmAttendanceRecord` roster, ordered by `LastName` then `FirstName`, each
  `Attended` copied unchanged from that record — a fixed historical snapshot, stable across calls.
- **Scheduled AGM** (`IsRecorded == false`, **NEW**): `Members` is every currently-active `Member`
  at call time (FR-010), ordered by `LastName` then `FirstName`, each with `Attended = false`. Not
  stable across calls — reflects live membership at the moment of each call (spec Edge Cases: "the
  report always reflects membership as of the moment it is printed").
- Creates, updates, or deletes no `AnnualGeneralMeeting`, `AgmAttendanceRecord`, `Member`, or any
  other record in either branch (FR-014) — this call has no side effects.

**Failure modes**: `EntityNotFoundException("AnnualGeneralMeeting", agmId, nameof(GenerateAsync))`
— `agmId` does not match any saved AGM. No other exception is raised by this contract.

## Division of responsibility: who enforces FR-012 (empty-state message)?

Unchanged from spec 018's own precedent: `GenerateAsync` never throws for an empty roster in
either branch — it returns a valid `AgmAttendanceSheetData` with an empty `Members` list.
`IAgmAttendanceSheetPdfRenderer.Render` will still happily render a header-only PDF if called with
an empty list. The UI caller (`AgmList`/`AgmDetail`'s existing `PrintAttendanceReport` handlers,
unchanged) remains the single place this is enforced: it checks `Members.Count == 0` after
`GenerateAsync` and shows an inline message instead of calling `Render`.

## UI routes this contract backs (StageFright.UI)

| Route | Component | Purpose |
|---|---|---|
| `/events/agm` | `AgmList.razor` | Every AGM, scheduled and recorded, with a Status column (FR-003) |
| `/events/agm/new` | `ScheduleAgm.razor` (**NEW**, replaces today's all-in-one `RecordAgm.razor` at this route) | Calls `ScheduleAsync`; date + notes only |
| `/events/agm/{Id}` | `AgmDetail.razor` | Branches on `IsRecorded`: date/notes only, or full attendance/positions view (FR-008) |
| `/events/agm/{Id}/record` | `RecordAgm.razor` (**CHANGED** — now loads an existing scheduled AGM by `Id` instead of creating one) | Calls `RecordAsync(Id, request)`; attendance grid + elections only, no date/notes fields |

## Out of scope for this contract

- `IAgmService.GetByIdAsync`, `GetAllAsync` (renamed from `GetPastAsync` by spec 023 / issue #324;
  see research.md Decision 6's superseded note), `ArchiveAsync`, `RecordSpecialElectionAsync` — no
  other signature changes.
- `IAgmAttendanceSheetPdfRenderer` — unchanged; the existing renderer already handles an
  all-unchecked `Members` list correctly with no code change (research.md's Decision 4 rationale).
- `IAgmRepository`'s existing members (`GetByIdAsync`, `AddAsync`, `UpdateAsync`, `ArchiveAsync`,
  `GetPastOrderedAsync`) — unchanged signatures; only `ExistsForYearAsync` is new (data-model.md).
- `ICommitteeService`, `ICommitteeTermRepository`, `ICommitteePositionRecordRepository` — no
  signature changes; `RecordAsync` continues consuming their existing published methods.
