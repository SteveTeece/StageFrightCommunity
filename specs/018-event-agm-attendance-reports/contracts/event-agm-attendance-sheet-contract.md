# Contract: Event & AGM Attendance Sheets (StageFright.Core → StageFright.Reports → StageFright.UI boundaries)

**Feature**: `018-event-agm-attendance-reports` | **Phase**: 1 (Design)

This is a desktop MAUI Blazor Hybrid application with no external network API — the relevant
"interfaces exposed to other systems" are the four new internal module/renderer contracts this
feature introduces, plus the existing contracts they consume read-only. Per
`specs/012-printable-attendance-roll/contracts/attendance-roll-contract.md`'s established
precedent for this documentation style, this document captures those boundaries rather than a
REST/CLI schema, since none exists in this codebase.

## New contract: `IEventAttendanceSheetService` (StageFright.Core → StageFright.UI)

```csharp
public interface IEventAttendanceSheetService
{
    Task<EventAttendanceSheetData> GenerateAsync(Guid eventId, CancellationToken ct = default);
}
```

**Preconditions**: `eventId` should identify a saved (non-deleted) `Event`. No other
precondition — any active-member count (including zero), and any event date (past or future,
FR-002), is a valid input state.

**Postconditions**:
- Returns an `EventAttendanceSheetData` whose `Members` list contains exactly the members
  returned by `IMemberRepository.GetActiveAsOfAsync(event.Date)` at call time (FR-002), ordered by
  `LastName` then `FirstName` (FR-006), each with `Participated` (FR-003) computed from the
  event's own `ParticipationRecord`s per data-model.md.
- Creates, updates, or deletes no `Event`, `ParticipationRecord`, `Member`, `Fee`, `Payment`, or
  `Transaction` record (FR-010) — this call has no side effects.
- Idempotent and side-effect-free: calling it twice in a row with no intervening data changes
  returns equivalent data both times; calling it again after participation is recorded reflects
  the new state, not a cached one (spec Edge Cases).

**Failure modes**: `EntityNotFoundException("Event", eventId, nameof(GenerateAsync))` —
`eventId` does not match any saved event. This is the only exception this contract raises; any
other failure surfacing from a dependency is expected to already be wrapped in a project custom
exception at that dependency's own boundary (Constitution §5).

## New contract: `IAgmAttendanceSheetService` (StageFright.Core → StageFright.UI)

```csharp
public interface IAgmAttendanceSheetService
{
    Task<AgmAttendanceSheetData> GenerateAsync(Guid agmId, CancellationToken ct = default);
}
```

**Preconditions**: `agmId` should identify a saved (non-deleted) `AnnualGeneralMeeting`. No other
precondition — an empty attendance roster is a valid input state (spec Edge Cases: "an AGM was
recorded with zero active members at the time").

**Postconditions**:
- Returns an `AgmAttendanceSheetData` whose `Members` list is exactly the AGM's persisted
  `AgmAttendanceRecord` roster (FR-005), ordered by `LastName` then `FirstName` (FR-006), with each
  member's `Attended` value copied unchanged from that record.
- Creates, updates, or deletes no `AnnualGeneralMeeting`, `AgmAttendanceRecord`, or `Member`
  record (FR-010) — this call has no side effects.
- Stable across calls: because `AgmAttendanceRecord` rows are immutable once saved, calling this
  twice for the same AGM always returns identical data, unlike the event sheet (spec Edge Cases:
  "the AGM sheet always reflects the fixed attendance roster captured when the AGM was originally
  recorded").

**Failure modes**: `EntityNotFoundException("AnnualGeneralMeeting", agmId, nameof(GenerateAsync))`
— `agmId` does not match any saved AGM. No other exception is raised by this contract.

## New contract: `IEventAttendanceSheetPdfRenderer` (StageFright.Reports → StageFright.UI)

```csharp
public interface IEventAttendanceSheetPdfRenderer
{
    byte[] Render(EventAttendanceSheetData data, string organizationName = "");
}
```

**Preconditions**: `data` is non-null (an empty `Members` list is a valid, accepted input — see
Postconditions). `organizationName` may be empty.

**Postconditions**:
- Returns a non-empty PDF byte array for any valid input, including a zero-member sheet (the
  renderer itself does not enforce FR-009's empty-state rule — see "Division of responsibility"
  below).
- The rendered document lays out members in two columns per page, overflowing to additional
  physical pages for larger rosters, with a minimal-width "Participated" checkbox column and
  wrapping column headings (FR-007), and each member's surname in capitals alongside their first
  name (FR-006).
- Pure function of its inputs — no I/O, no repository/`DbContext` access, no mutation of `data`.

**Failure modes**: None expected under normal QuestPDF operation for valid, well-formed
`EventAttendanceSheetData`; any unexpected QuestPDF exception is caught and handled by the UI
caller exactly as `RehearsalList.razor.cs`'s `PrintRoll` already catches and reports
`AttendanceRollPdfRenderer.Render` failures today.

## New contract: `IAgmAttendanceSheetPdfRenderer` (StageFright.Reports → StageFright.UI)

```csharp
public interface IAgmAttendanceSheetPdfRenderer
{
    byte[] Render(AgmAttendanceSheetData data, string organizationName = "");
}
```

**Preconditions/Postconditions/Failure modes**: identical in shape to
`IEventAttendanceSheetPdfRenderer` above, except the checkbox column is headed "Attended" and the
title reads "AGM Attendance Report". Both renderers share their page-composition mechanics via
the internal `CheckboxSheetPdfBuilder` (research.md Decision 3, data-model.md), which is why their
contracts are guaranteed to behave identically with respect to columns, checkbox width, and
header wrapping (User Story 3) — this is not merely a documentation convention, it is the same
code path.

## Division of responsibility: who enforces FR-009 (empty-state message)?

Neither new service nor either new renderer enforces "nobody to list → show an empty-state
message instead of a blank printable sheet" (FR-009) internally:
- `IEventAttendanceSheetService.GenerateAsync` / `IAgmAttendanceSheetService.GenerateAsync` return
  a normal, valid data object with an empty `Members` list — neither throws for this case
  (research.md Decision 7).
- `IEventAttendanceSheetPdfRenderer.Render` / `IAgmAttendanceSheetPdfRenderer.Render` will happily
  render a header-only PDF for an empty `Members` list if called — neither has an opinion on
  whether that's an appropriate thing to do.
- The UI caller (each page's own `PrintAttendanceSheet`/`PrintAttendanceReport` handler) is the
  single place FR-009 is enforced: it inspects `data.Members.Count` after calling `GenerateAsync`
  and, if zero, shows an inline message instead of calling `Render`/writing a temp file/launching
  a viewer — mirroring `RehearsalList.razor.cs`'s existing `PrintRoll` precedent exactly.

## Out of scope for this contract

- `IReportProvider` (`StageFright.Reports/Registry/IReportProvider.cs`) — unchanged; this feature
  deliberately does not implement or register against this contract (research.md Decision 1).
- `IEventRepository`, `IMemberRepository`, `IAgmRepository`, `IAgmAttendanceRepository` — no
  signature changes; the two new services only consume their existing, published methods
  (`GetByIdWithDetailsAsync`, `GetActiveAsOfAsync`, `GetByIdAsync`, `GetByAgmAsync`).
- `IAttendanceRollPdfRenderer`/`AttendanceRollPdfRenderer` — unchanged; the two new renderers are
  separate sibling contracts sharing a new, separate internal helper
  (`CheckboxSheetPdfBuilder`), not a modification of the rehearsal roll's own renderer.
- `ICsvReportExporter`, `PdfReportRenderer`, `ReportData` — unchanged; neither sheet has a CSV
  export path (spec expectations: "No spreadsheet/CSV export for either sheet — print/PDF only").
