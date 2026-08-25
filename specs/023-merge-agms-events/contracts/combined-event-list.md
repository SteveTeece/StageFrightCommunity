# Contract: Combined Events List (All Events screen)

This feature has no HTTP/CLI surface — it exposes a C# service contract plus a UI contract (routes and the literal identifiers a test codes against). Every quoted string below is copied verbatim from `spec.md`'s Functional Requirements and MUST NOT be renamed, recased, or reworded by implementation or tests.

## Service contract

```csharp
namespace StageFright.Core.Contracts;

public interface ICombinedEventListService
{
    Task<IReadOnlyList<CombinedEventListItem>> GetAllAsync(CancellationToken ct = default);
}
```

See [data-model.md](../data-model.md) for `CombinedEventListItem`'s fields.

## Route contract

The All Events screen keeps its existing route; no route is added, removed, or renamed (FR-012).

| Screen | Route | Change |
|---|---|---|
| All Events (combined list) | `/events` | UNCHANGED route; now renders merged Event + AGM rows |
| Event detail | `/events/{Id:guid}` | UNCHANGED — target of an Event row's Date link |
| Record participation | `/events/{eventId:guid}/participation` | UNCHANGED — target of an Event row's "Record Participation" button |
| AGM list (dedicated) | `/events/agm` | UNCHANGED, still reachable (FR-012) |
| AGM detail | `/events/agm/{id:guid}` | UNCHANGED — target of an AGM row's Date link (FR-006) |
| Record AGM | `/events/agm/{Id:guid}/record` | UNCHANGED — target of an AGM row's "Record" action (FR-007) |
| Schedule Event | `/events/new` | UNCHANGED — the All Events screen's "Schedule Event" button still only creates a generic `Event` (FR-013) |
| Schedule AGM | `/events/agm/new` | UNCHANGED, still the only entry point for scheduling an AGM (FR-013) |

## Per-kind row behavior a test codes against

| Aspect | Event row | AGM row |
|---|---|---|
| Type column text | the event's own `EventType.Name` | `"Annual General Meeting"` (FR-004) |
| Status column (unrecorded) | `"Not recorded"` | badge text `"Scheduled"`, class `bg-warning text-dark` |
| Status column (recorded) | formatted rate, e.g. `"85.0%"` | badge text `"Recorded"`, class `bg-success` |
| Actions column (unrecorded) | button `"Record Participation"` → navigates to `/events/{id}/participation` | link `"Record"` → navigates to `/events/agm/{id}/record` (FR-007) |
| Actions column (recorded) | text `"Recorded"` | no "Record" action rendered |
| Actions column (always) | button `"Print"`, via `IEventAttendanceSheetService` + `IEventAttendanceSheetPdfRenderer` | button `"Print"`, via `IAgmAttendanceSheetService` + `IAgmAttendanceSheetPdfRenderer` (FR-007) |
| Row click target | `/events/{id}` | `/events/agm/{id}` (FR-006 — MUST NOT be `/events/{id}`) |

## Empty-state / no-results messages (FR-009, verbatim strings)

| Condition | Message |
|---|---|
| No Events and no AGMs at all | `"No events scheduled yet."` |
| A search term matches neither an Event nor an AGM | `No events match "<strong>{searchTerm}</strong>".` |

## Search fields (FR-008)

A search term matches a row when it is a case-insensitive substring of any of:
- the row's formatted date (`Date.ToString("d MMM yyyy")`)
- the row's `TypeName` (including the literal `"Annual General Meeting"` for AGM rows)
- the row's `Notes`
