# Data Model: Print Reports for Event and AGM Attendance

**Feature**: `018-event-agm-attendance-reports` | **Phase**: 1 (Design) | **Depends on**: [research.md](./research.md)

No existing entity or table changes — this feature is entirely read-only (FR-010). It introduces
four new plain DTOs and two new services in `StageFright.Core` (one pair per owning module), plus
two new renderers and one shared internal layout helper in `StageFright.Reports`.

## New DTO: `EventAttendanceSheetData`

`src/StageFright.Core/Modules/Events/EventAttendanceSheetData.cs`

```csharp
public sealed class EventAttendanceSheetData
{
    public DateTime EventDate { get; init; }
    public string EventTypeName { get; init; } = string.Empty;
    public IReadOnlyList<EventAttendanceSheetMember> Members { get; init; } = Array.Empty<EventAttendanceSheetMember>();
}
```

- `EventDate`: copied from `Event.Date`, used for the sheet's header date line (FR-008) and to
  identify which point-in-time roster `Members` reflects.
- `EventTypeName`: copied from `Event.EventType.Name` (e.g. "Performance", "Annual General
  Meeting"), used as part of the header title so a forward-scheduled AGM event's sheet reads as an
  AGM sheet even though it is technically printed through the generic event path (spec
  Assumptions).
- `Members`: already sorted by surname then first name (FR-006) by the time the service returns
  it — the renderer does not re-sort. Empty when no member is active as of `EventDate` (FR-009's
  precondition; see research.md Decision 7 for who handles the empty state).

## New DTO: `EventAttendanceSheetMember`

`src/StageFright.Core/Modules/Events/EventAttendanceSheetMember.cs`

```csharp
public sealed class EventAttendanceSheetMember
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public bool Participated { get; init; }
}
```

- `FirstName`/`LastName`: plain values copied as-is from `Member`. Uppercasing the surname for
  display (FR-006) is a rendering concern applied by `EventAttendanceSheetPdfRenderer`, not baked
  into this DTO, matching `AttendanceRollMember`'s precedent.
- `Participated`: pre-computed boolean (FR-003) — `true` only if a `ParticipationRecord` exists for
  this member and event with `Participated == true`; `false` when no participation has been
  recorded yet for the event, or the member was recorded as not participating.
- No `MemberId` field — the sheet is a print-only, one-shot artifact with no interactive per-row
  action once generated.

## New DTO: `AgmAttendanceSheetData`

`src/StageFright.Core/Modules/Agm/AgmAttendanceSheetData.cs`

```csharp
public sealed class AgmAttendanceSheetData
{
    public DateTime AgmDate { get; init; }
    public IReadOnlyList<AgmAttendanceSheetMember> Members { get; init; } = Array.Empty<AgmAttendanceSheetMember>();
}
```

- `AgmDate`: copied from `AnnualGeneralMeeting.Date`, used for the sheet's header date line
  (FR-008).
- `Members`: the AGM's fixed attendance roster (FR-005) as captured when the AGM was recorded —
  already sorted by surname then first name by `IAgmAttendanceRepository.GetByAgmAsync`. Empty
  only if the AGM was recorded with zero attendance records (FR-009's precondition; spec Edge
  Cases/Acceptance Scenario 2).

## New DTO: `AgmAttendanceSheetMember`

`src/StageFright.Core/Modules/Agm/AgmAttendanceSheetMember.cs`

```csharp
public sealed class AgmAttendanceSheetMember
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public bool Attended { get; init; }
}
```

- `Attended`: copied directly from the corresponding `AgmAttendanceRecord.Attended` — never
  recomputed, since that record is immutable once saved (FR-005).

## New service contract: `IEventAttendanceSheetService`

`src/StageFright.Core/Contracts/IEventAttendanceSheetService.cs`

```csharp
public interface IEventAttendanceSheetService
{
    /// <summary>
    /// Assembles the printable event attendance sheet: every member active as of the event's
    /// date (FR-002), sorted by surname then first name (FR-006), each with a pre-computed
    /// Participated flag (FR-003) reflecting any participation already recorded. Read-only —
    /// creates, updates, or deletes nothing.
    /// </summary>
    /// <exception cref="EntityNotFoundException">eventId does not match a saved event.</exception>
    Task<EventAttendanceSheetData> GenerateAsync(Guid eventId, CancellationToken ct = default);
}
```

### `EventAttendanceSheetService` — implementation shape

`src/StageFright.Core/Modules/Events/EventAttendanceSheetService.cs`

Dependencies (constructor-injected, both existing interfaces — no new repository method):
`IEventRepository`, `IMemberRepository`.

```csharp
public async Task<EventAttendanceSheetData> GenerateAsync(Guid eventId, CancellationToken ct = default)
{
    var evt = await _eventRepo.GetByIdWithDetailsAsync(eventId, ct)
        ?? throw new EntityNotFoundException("Event", eventId, nameof(GenerateAsync));

    var activeMembers = (await _memberRepo.GetActiveAsOfAsync(evt.Date, ct))
        .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
        .ToList();

    var participatedByMember = evt.ParticipationRecords
        .ToDictionary(p => p.MemberId, p => p.Participated);

    var members = activeMembers.Select(m => new EventAttendanceSheetMember
    {
        FirstName = m.FirstName,
        LastName = m.LastName,
        Participated = participatedByMember.TryGetValue(m.Id, out var wasParticipated) && wasParticipated
    }).ToList();

    return new EventAttendanceSheetData
    {
        EventDate = evt.Date,
        EventTypeName = evt.EventType?.Name ?? string.Empty,
        Members = members
    };
}
```

### Validation / preconditions

| Rule | Requirement | Exception |
|---|---|---|
| Event must exist | `eventId` matches a non-deleted `Event` | `EntityNotFoundException("Event", eventId, nameof(GenerateAsync))` |
| No other precondition | Any active-as-of-date member count (including zero), and any event date (past or future, FR-002), is a valid input | — (empty `Members` list, not an exception; see research.md Decision 7) |

## New service contract: `IAgmAttendanceSheetService`

`src/StageFright.Core/Contracts/IAgmAttendanceSheetService.cs`

```csharp
public interface IAgmAttendanceSheetService
{
    /// <summary>
    /// Assembles the printable AGM attendance report from the AGM's fixed, already-persisted
    /// attendance roster (FR-005), sorted by surname then first name (FR-006). Read-only —
    /// creates, updates, or deletes nothing.
    /// </summary>
    /// <exception cref="EntityNotFoundException">agmId does not match a saved AGM.</exception>
    Task<AgmAttendanceSheetData> GenerateAsync(Guid agmId, CancellationToken ct = default);
}
```

### `AgmAttendanceSheetService` — implementation shape

`src/StageFright.Core/Modules/Agm/AgmAttendanceSheetService.cs`

Dependencies (constructor-injected, both existing interfaces — no new repository method):
`IAgmRepository`, `IAgmAttendanceRepository`.

```csharp
public async Task<AgmAttendanceSheetData> GenerateAsync(Guid agmId, CancellationToken ct = default)
{
    var agm = await _agmRepo.GetByIdAsync(agmId, ct)
        ?? throw new EntityNotFoundException("AnnualGeneralMeeting", agmId, nameof(GenerateAsync));

    var records = await _attendanceRepo.GetByAgmAsync(agmId, ct);

    var members = records.Select(r => new AgmAttendanceSheetMember
    {
        FirstName = r.Member.FirstName,
        LastName = r.Member.LastName,
        Attended = r.Attended
    }).ToList();

    return new AgmAttendanceSheetData
    {
        AgmDate = agm.Date,
        Members = members
    };
}
```

### Validation / preconditions

| Rule | Requirement | Exception |
|---|---|---|
| AGM must exist | `agmId` matches a non-deleted `AnnualGeneralMeeting` | `EntityNotFoundException("AnnualGeneralMeeting", agmId, nameof(GenerateAsync))` |
| No other precondition | An empty attendance roster is a valid result | — (empty `Members` list, not an exception) |

### State / lifecycle

Both services are read-only — no state transitions. `GenerateAsync` may be called any number of
times; the event sheet always reflects live data at call time (membership and participation can
change between calls), while the AGM sheet always reflects the same fixed roster, since
`AgmAttendanceRecord` rows are never edited after the AGM is saved (spec Assumptions).

## New renderer contracts

`src/StageFright.Reports/Rendering/IEventAttendanceSheetPdfRenderer.cs`

```csharp
public interface IEventAttendanceSheetPdfRenderer
{
    /// <summary>
    /// Renders an event attendance sheet to PDF bytes: two-column layout (FR-007), minimal-width
    /// checkbox column (FR-007), wrapping column headings (FR-007), surname in capitals alongside
    /// first name (FR-006). Non-empty on success, even for a zero-member sheet.
    /// </summary>
    byte[] Render(EventAttendanceSheetData data, string organizationName = "");
}
```

`src/StageFright.Reports/Rendering/IAgmAttendanceSheetPdfRenderer.cs`

```csharp
public interface IAgmAttendanceSheetPdfRenderer
{
    /// <summary>
    /// Renders an AGM attendance report to PDF bytes, using the same layout rules as
    /// <see cref="IEventAttendanceSheetPdfRenderer"/> (FR-007) so both sheets look and behave
    /// identically. Non-empty on success, even for a zero-member roster.
    /// </summary>
    byte[] Render(AgmAttendanceSheetData data, string organizationName = "");
}
```

### Implementation shape — shared internal layout helper

`src/StageFright.Reports/Rendering/CheckboxSheetPdfBuilder.cs` (internal, not published — see
research.md Decision 3):

```csharp
internal static class CheckboxSheetPdfBuilder
{
    internal static byte[] Build(
        string organizationName,
        string title,
        string dateLine,
        string checkboxColumnHeader,
        IReadOnlyList<(string LastName, string FirstName, bool Checked)> rows);
}
```

This owns the two-column chunking (`RowsPerColumn = 32`, research.md Decision 6), the A4/18pt-margin
page setup, the single-checkbox-column table (`Name` wide column + one minimal-width checkbox
column headed by `checkboxColumnHeader`), the bordered-box-plus-"✓"-glyph checkbox cell style
(matching `AttendanceRollPdfRenderer`'s existing convention and the CLAUDE.md rule that a checked
box is a bordered container, never a solid fill), and the "Page X of Y" footer. It has no header
"Generated: <timestamp>" line (FR-008).

- `EventAttendanceSheetPdfRenderer.Render` calls `CheckboxSheetPdfBuilder.Build` with
  `title = "Event Attendance Sheet"`, `dateLine = $"{data.EventTypeName}: {data.EventDate:d MMMM yyyy}"`,
  `checkboxColumnHeader = "Participated"`, and each member mapped to
  `(LastName, FirstName, Participated)`.
- `AgmAttendanceSheetPdfRenderer.Render` calls `CheckboxSheetPdfBuilder.Build` with
  `title = "AGM Attendance Report"`, `dateLine = $"Annual General Meeting: {data.AgmDate:d MMMM yyyy}"`,
  `checkboxColumnHeader = "Attended"`, and each member mapped to `(LastName, FirstName, Attended)`.

## Relationships

- `EventAttendanceSheetService` reads `Event` (via `IEventRepository`, including its `EventType`
  and `ParticipationRecords.Member` navigation) and `Member` (via `IMemberRepository`) — purely as
  read dependencies. No foreign keys, navigation properties, or schema relationships are added.
- `AgmAttendanceSheetService` reads `AnnualGeneralMeeting` (via `IAgmRepository`) and
  `AgmAttendanceRecord` (via `IAgmAttendanceRepository`, including its `Member` navigation) —
  purely as read dependencies.
- Both renderers and `CheckboxSheetPdfBuilder` have no dependency on any repository or
  `DbContext` — each is a pure function of its DTO (plus an organization-name string), exactly
  like `AttendanceRollPdfRenderer`'s relationship to `AttendanceRollData`.

## DI registration (`src/StageFright.App/MauiProgram.cs`)

Four new lines in `RegisterCoreServices`, alongside each domain's existing neighbors:

```csharp
services.AddScoped<IEventAttendanceSheetService, EventAttendanceSheetService>();     // near IEventService
services.AddScoped<IEventAttendanceSheetPdfRenderer, EventAttendanceSheetPdfRenderer>(); // near IAttendanceRollPdfRenderer
services.AddScoped<IAgmAttendanceSheetService, AgmAttendanceSheetService>();          // near IAgmService
services.AddScoped<IAgmAttendanceSheetPdfRenderer, AgmAttendanceSheetPdfRenderer>();  // near IAttendanceRollPdfRenderer
```

`CheckboxSheetPdfBuilder` is `internal static` and requires no DI registration.
