# Data Model: AGMs on the All Events List

No persisted entity changes. `Event` and `AnnualGeneralMeeting` stay exactly as they are today (per the spec's Assumptions: "the underlying `Event` and `AnnualGeneralMeeting` data models stay separate... this feature only changes the read/list-display layer, not storage"). This feature introduces one new, unpersisted read-model DTO and its discriminant enum.

## CombinedEventListItem (new)

**File**: `src/StageFright.Core/Modules/Events/CombinedEventListItem.cs`

A read-only row shown on the All Events screen — the spec's "Combined Events List Entry" key entity. Not persisted; rebuilt fresh every time `ICombinedEventListService.GetAllAsync()` is called, by projecting either one `Event` or one `AnnualGeneralMeeting`.

| Field | Type | Populated from | Notes |
|---|---|---|---|
| `Id` | `Guid` | `Event.Id` or `AnnualGeneralMeeting.Id` | The source record's own identity (FR carries identity per spec's Key Entities). |
| `Date` | `DateTime` | `Event.Date` or `AnnualGeneralMeeting.Date` | Drives the combined sort (FR-002) and the Date column (FR-003). |
| `Notes` | `string?` | `Event.Notes` or `AnnualGeneralMeeting.Notes` | Rendered in the existing Notes column (FR-003) and searched by it (FR-008). |
| `TypeName` | `string` | `Event.EventType?.Name` or the fixed literal `"Annual General Meeting"` | Satisfies FR-004; searched as the "type" field for FR-008. |
| `Kind` | `CombinedEventListItemKind` | derived from the source type | Discriminates which per-kind Status/Actions branch and Print pipeline the row uses. |
| `ParticipationRate` | `decimal?` | `Event.StoredParticipationRate` | Always `null` for AGM rows; `null` for an Event whose participation isn't yet recorded. |
| `IsAgmRecorded` | `bool?` | `AnnualGeneralMeeting.IsRecorded` | Always `null` for Event rows; drives the "Recorded"/"Scheduled" badge (FR-005) and whether the "Record" action shows (FR-007). |
| `DetailUrl` | `string` | computed: `"/events/{Id}"` for an Event, `"/events/agm/{Id}"` for an AGM | Row-click navigation target (FR-006) — the safety-critical field distinguishing this feature from a naive merge. |

**Validation rules**: none — this is a read-only projection with no user input and no persistence; nothing to validate.

**State transitions**: none on the DTO itself. Each row simply reflects its source record's *current* state at load time (e.g. `IsAgmRecorded` flips from `false` to `true` only because the underlying `AnnualGeneralMeeting.IsRecorded` changed via the existing `AgmService.RecordAsync` — a workflow this feature does not touch, per FR-011).

## CombinedEventListItemKind (new)

**File**: `src/StageFright.Core/Modules/Events/CombinedEventListItemKind.cs`

```csharp
public enum CombinedEventListItemKind
{
    Event,
    Agm
}
```

Two values only — there are exactly two source record types being merged, per the spec's fixed scope (FR-011: this change does not introduce any new record kind).

## ICombinedEventListService (new contract)

**File**: `src/StageFright.Core/Contracts/ICombinedEventListService.cs` (interface); `src/StageFright.Core/Modules/Events/CombinedEventListService.cs` (implementation)

```csharp
public interface ICombinedEventListService
{
    /// <summary>
    /// Returns every non-archived Event and non-archived AnnualGeneralMeeting as one list of
    /// CombinedEventListItem, sorted by Date descending (FR-001, FR-002). Read-only — creates,
    /// updates, or deletes nothing.
    /// </summary>
    Task<IReadOnlyList<CombinedEventListItem>> GetAllAsync(CancellationToken ct = default);
}
```

Implementation composes `IEventService.GetAllAsync(ct)` and `IAgmService.GetPastAsync(ct)` (both already excluding archived records via their existing EF Core query filters — see [research.md](./research.md)), maps each source record to a `CombinedEventListItem`, concatenates, and orders the result by `Date` descending. No tie-break beyond a stable sort is required (per the spec's Assumptions).
