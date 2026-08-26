# Interface Contract: AGM Workflow

**Feature**: [spec.md](../spec.md) | **Data model**: [data-model.md](../data-model.md)

This is a UI + service contract: the routes, menu entries, and service method signatures that tasks and tests are written against. Identifiers here are pinned — later phases must not rename, recase, or restructure them without updating this file first.

---

## Module placement

- **New module** `src/StageFright.Core/Modules/Agm/` — owns `AnnualGeneralMeeting`/`AgmAttendanceRecord` orchestration (`IAgmService`/`AgmService`, request/response records). New vertical slice per constitution §4.1, since no existing module owns "the AGM as its own record type."
- **Extended module** `src/StageFright.Core/Modules/Members/` — owns the committee-position-record model rework (`CommitteeOfficeHolderType`, `CommitteeTerm`, extended `CommitteePositionRecord`), since `CommitteeService`/`CommitteeMembership` already live here (research D2) — least churn, matches the spec's "existing — reused, extended" framing.
- **Extended** `src/StageFright.Core/Modules/Events/EventsMenuItemProvider.cs` — contributes the new routes below as `SubItems` (menu items are just route strings; no C# dependency between the Events and Agm modules is required).
- **Extended** Settings module + `SettingsPage.razor` — new Committee tab (research D6).
- **Extended** `src/StageFright.UI/Pages/Setup/` — new wizard step.
- **Reworked in place** `src/StageFright.Reports/Providers/CommitteeReportProvider.cs`.

---

## Routes (Blazor pages)

| Route | Component | Purpose | FRs |
|---|---|---|---|
| `/events` | *(unchanged)* `EventList.razor` | Generic events list — no longer offers "Annual General Meeting" in its type dropdown. | FR-003 |
| `/events/agm/new` | `RecordAgm.razor` (new) | Record AGM screen — meeting date, attendance grid, President/Secretary/Treasurer + custom office-holder + general-committee assignment, seat-count-target progress. | FR-001, FR-004–009, FR-014 |
| `/events/agm` | `AgmList.razor` (new) | Browsable past-AGM list, most-recent-first, date + attendance count. | FR-015 |
| `/events/agm/{id:guid}` | `AgmDetail.razor` (new) | Read-only past-AGM detail — attendance + elected positions (with start/end dates when a position had >1 holder), archive action. | FR-016, FR-017, FR-029 |
| `/events/agm/special-election/new` | `RecordSpecialElection.razor` (new) | Record a mid-term replacement against the currently-open committee term. | FR-026–028 |
| `/settings?tab=committee` | `CommitteeSettingsTab.razor` (new, 5th hardcoded core tab) | Office-holder title management (add/rename/reorder/archive) + general-committee seat-count target. | FR-012–014 |
| `/setup` (step 5) | `SetupWizard.razor` (extended, step count 4→5) | Committee configuration + AGM-month selection during first run. | FR-020–022 |

## Menu contribution

`EventsMenuItemProvider.GetMenuItems()` — existing single top-level item gains `SubItems`:

```csharp
new MenuItem
{
    Title = "Events", Route = "/events", DisplayOrder = 0,
    SubItems = new List<MenuItem>
    {
        new() { Title = "All Events", Route = "/events", DisplayOrder = 0 },
        new() { Title = "Record AGM", Route = "/events/agm/new", DisplayOrder = 1 },
        new() { Title = "Past AGMs", Route = "/events/agm", DisplayOrder = 2 },
    }
}
```

---

## Service contracts

### `IAgmService` (new — `StageFright.Core/Contracts/IAgmService.cs`)

```csharp
Task<AnnualGeneralMeeting> RecordAsync(RecordAgmRequest request, CancellationToken ct = default);
Task<AnnualGeneralMeeting?> GetByIdAsync(Guid id, CancellationToken ct = default);
Task<IReadOnlyList<AnnualGeneralMeeting>> GetAllAsync(CancellationToken ct = default); // most-recent-first; renamed from GetPastAsync, spec 023 / issue #324
Task ArchiveAsync(Guid id, string deletedBy, CancellationToken ct = default);
Task<CommitteePositionRecord> RecordSpecialElectionAsync(RecordSpecialElectionRequest request, CancellationToken ct = default);
```

`RecordAgmRequest` (record, `StageFright.Core/Modules/Agm/RecordAgmRequest.cs`):
```csharp
public record RecordAgmRequest(
    DateTime Date,
    string? Notes,
    IReadOnlyList<Guid> AttendedMemberIds,
    IReadOnlyList<Guid> AllActiveMemberIds,       // to write a "not attended" row for every active member, not just attendees
    IReadOnlyDictionary<Guid, Guid> OfficeHolderAssignments, // OfficeHolderTypeId -> MemberId (President/Secretary/Treasurer + custom)
    IReadOnlyList<Guid> GeneralCommitteeMemberIds);
```

`RecordSpecialElectionRequest` (record, `StageFright.Core/Modules/Agm/RecordSpecialElectionRequest.cs`):
```csharp
public record RecordSpecialElectionRequest(
    Guid OutgoingPositionRecordId,
    Guid IncomingMemberId,
    DateTime ReplacementDate);
```

**Validation order inside `RecordAsync`** (mirrors `PaymentService.RecordAsync`'s pre-transaction validation, research D9):
1. Before opening the transaction: throw `ValidationException` if any `MemberId` appears in more than one of `OfficeHolderAssignments`/`GeneralCommitteeMemberIds` (FR-008).
2. Inside `ExecuteInTransactionAsync`: create `AnnualGeneralMeeting` (snapshotting `Settings.GeneralCommitteeSeatCountTarget` equivalent field), write one `AgmAttendanceRecord` per `AllActiveMemberIds`, close the previously-open `CommitteeTerm` (if any) by setting its `EndDate` = `request.Date`, create the new `CommitteeTerm` (`StartedByAgmId` = this AGM, `LabelYear` computed per FR-024), create one `CommitteePositionRecord` per office-holder assignment and general-committee member (`StartDate` = `request.Date`, `EndDate` = null), write the audit entry.

**Validation inside `RecordSpecialElectionAsync`**:
1. Load the outgoing `CommitteePositionRecord` → its `CommitteeTerm`; throw `DataIntegrityException` if `CommitteeTerm.EndDate != null` (term already closed — Edge Case).
2. Throw `ValidationException` if `IncomingMemberId` already holds another open slot in the same term (FR-008 reused).
3. Inside the transaction: set outgoing record's `EndDate = ReplacementDate`; create incoming record (`StartDate = ReplacementDate`, `EndDate = null`, same `CommitteeTermId`/`OfficeHolderTypeId`); audit both.

### `ICommitteeService` (extended — `StageFright.Core/Contracts/ICommitteeService.cs`)

New methods (old `SoftDeleteCurrentYearAsync` removed, research D3):
```csharp
Task<IReadOnlyList<CommitteePositionRecord>> GetCurrentAsync(CancellationToken ct = default);       // records under the one open CommitteeTerm
Task<IReadOnlyList<CommitteePositionRecord>> GetByTermAsync(Guid committeeTermId, CancellationToken ct = default);
Task<IReadOnlyList<CommitteePositionRecord>> GetByAgmAsync(Guid annualGeneralMeetingId, CancellationToken ct = default); // "this AGM's own detail view" (FR-016)
```

### `ICommitteeOfficeHolderTypeService` (new — `StageFright.Core/Contracts/ICommitteeOfficeHolderTypeService.cs`)

```csharp
Task<IReadOnlyList<CommitteeOfficeHolderType>> GetActiveAsync(CancellationToken ct = default);   // built-ins first (DisplayOrder), then custom
Task<CommitteeOfficeHolderType> AddAsync(string name, CancellationToken ct = default);
Task RenameAsync(Guid id, string newName, CancellationToken ct = default);        // throws ValidationException if IsBuiltIn
Task ReorderAsync(IReadOnlyList<Guid> orderedCustomTitleIds, CancellationToken ct = default); // custom titles only; built-ins stay pinned at 0-2
Task ArchiveAsync(Guid id, string deletedBy, CancellationToken ct = default);      // throws ValidationException if IsBuiltIn
```

### `IEventTypeService` (extended — filters AGM out of the generic "create event" dropdown, FR-003)

```csharp
Task<IReadOnlyList<EventType>> GetSelectableForNewEventsAsync(CancellationToken ct = default); // excludes Name == "Annual General Meeting"
```

---

## Settings contract

`Settings.CommitteeRenewalMonth` (unchanged column, repurposed meaning — research D7): now documented and labeled in UI as "AGM month" everywhere it's bound (`GeneralSettingsTab`/setup wizard step 5). `Settings.LastCommitteeResetYear` removed (research D3).

`Settings` gains no new column for the seat-count target *default* — the coordinator-configured default target lives on a simple `Settings.GeneralCommitteeSeatCountTarget` (`int?`) field (the value FR-014 lets the coordinator set); each `AnnualGeneralMeeting.GeneralCommitteeSeatCountTarget` is a one-time snapshot copy of this at save time.

---

## Report contract

`CommitteeReportProvider` keeps its existing `IReportProvider` shape (`ReportId`, `ReportName`, master/detail `SummaryColumns`/`SummaryRow`) but re-keys its grouping from `GroupBy(r => r.Membership.Year)` to `GroupBy(r => r.PositionRecord.CommitteeTermId)`, section heading becomes `CommitteeTerm.LabelYear` (descending), and `BuildPositionLines`'s per-position cell changes from a flat comma-joined name list to a holder-count-aware formatter: single open holder → name only; ≥2 holders for the same `(CommitteeTermId, OfficeHolderTypeId)` → `"Name (StartDate–EndDate or 'present')"` joined per holder, ordered by `StartDate`.
