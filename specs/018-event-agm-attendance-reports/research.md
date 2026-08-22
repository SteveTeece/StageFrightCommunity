# Research: Print Reports for Event and AGM Attendance

**Feature**: `018-event-agm-attendance-reports` | **Phase**: 0 (Research) | **Input**: [spec.md](./spec.md)

No `NEEDS CLARIFICATION` markers remain in spec.md — every decision below is resolved directly
from existing code precedent (chiefly spec 012's rehearsal attendance roll) rather than requiring
a new clarification round.

## Decision 1 — Bypass the generic reports pipeline; follow spec 012's own precedent

**Decision**: Both sheets are produced by a dedicated Core service + DTO pair and a dedicated
QuestPDF renderer per domain, exactly like `IAttendanceRollService`/`AttendanceRollData`/
`IAttendanceRollPdfRenderer` — not implemented as `IReportProvider`s.

**Rationale**: The reports-pipeline living spec documents that `ReportData` is a flat table of
pre-formatted string cells with no multi-column/checkbox-glyph concept, and that
`PdfReportRenderer`/`ICsvReportExporter` always render every row of full detail with no bespoke
layout hooks. FR-007's two-column, minimal-width-checkbox, wrapping-header layout cannot be
expressed there. Spec 012 already established and shipped the exact escape hatch this feature
needs (a sibling renderer outside the generic pipeline, still reusing QuestPDF and the
"temp file → `Process.Start`" hand-off convention the reports-pipeline living spec requires for
all printed output).

**Alternatives considered**:
- Extend `ReportData`/`PdfReportRenderer` with a "checkbox column" and multi-column concept —
  rejected: would change the shared renderer every one of the ten existing MVP reports depends on,
  for a layout only these three attendance-style sheets need (spec 012 rejected this for the same
  reason; no new information changes that call here).
- Register these as `IReportProvider`s so they appear in the generic Reports menu — rejected: the
  spec (Assumptions) explicitly wants print actions on the Events/AGM list and detail pages
  themselves, not the separate Reports section, matching how the rehearsal roll is triggered from
  `RehearsalList`, not from Reports.

## Decision 2 — Two independent DTO/service pairs (one per owning module), not one shared cross-module model

**Decision**: `EventAttendanceSheetData`/`EventAttendanceSheetMember`/`IEventAttendanceSheetService`
live in the `Events` module; `AgmAttendanceSheetData`/`AgmAttendanceSheetMember`/
`IAgmAttendanceSheetService` live in the `Agm` module. Neither module's DTO is imported by the
other.

**Rationale**: Constitution §4.1 prohibits a module importing from a sibling module's concrete
folder. The two sheets' underlying data sources are genuinely different in shape — the event
sheet recomputes membership live from `IMemberRepository.GetActiveAsOfAsync` plus
`ParticipationRecord`s, while the AGM sheet reads a fixed, already-persisted
`AgmAttendanceRecord` roster that is never recomputed (spec Assumptions) — so collapsing them into
one shared "generic checkbox roster" DTO would need to live somewhere neither module owns, and no
such neutral location exists in this codebase's module layout (unlike `StageFright.Reports`,
which is a genuine shared downstream layer — see Decision 3).

**Alternatives considered**:
- One shared `AttendanceSheetData` DTO in a new `StageFright.Core/Shared/` folder — rejected: no
  such shared-DTO folder exists anywhere else in the codebase; introducing one for a single pair
  of call sites is more structural change than the feature needs, and the two domains' assembly
  logic (live recompute vs. fixed historical snapshot) is different enough that a shared DTO would
  need to over-generalize its own doc comments to cover both meanings.

## Decision 3 — Share the QuestPDF page-layout mechanics via one internal helper in `StageFright.Reports`

**Decision**: Both new renderers (`EventAttendanceSheetPdfRenderer`, `AgmAttendanceSheetPdfRenderer`)
delegate their actual page composition to one new `internal static class CheckboxSheetPdfBuilder`
in `StageFright.Reports/Rendering/`, parameterized by title, date-line, checkbox-column header
text, organization name, and an ordered list of `(LastName, FirstName, Checked)` rows. The
existing `AttendanceRollPdfRenderer` (two checkbox columns: Present + fee-paid) is left untouched.

**Rationale**: User Story 3 explicitly requires the event sheet and the AGM sheet to render with
*identical* two-column behavior, checkbox-column width, wrapping headings, and surname
capitalization — the surest way to guarantee two outputs stay pixel-identical in that respect is
one shared layout implementation, not two hand-copies that could drift. Because
`StageFright.Reports` is a downstream rendering layer (not a Core module), sharing code here does
not create the kind of Core-to-Core module coupling Constitution §4.1 forbids — it is the same
relationship `PdfReportRenderer` already has to every report provider that feeds it `ReportData`.

**Alternatives considered**:
- Duplicate the full ~90-line QuestPDF layout twice (once per new renderer), matching spec 012's
  file literally with no shared helper — rejected: acceptable but leaves the two sheets' pagination
  and checkbox-cell rendering free to drift out of sync over time with no single source of truth,
  which directly risks User Story 3's own acceptance scenario.
- Refactor `AttendanceRollPdfRenderer` itself to use the new shared helper (three renderers sharing
  one builder) — rejected for this feature: that renderer has a two-checkbox-column shape (Present
  + fee-paid) the new one-checkbox-column builder doesn't support, and touching already-shipped,
  tested code for a refactor unrelated to this feature's scope adds regression risk with no
  required benefit.

## Decision 4 — Event sheet roster: reuse `GetActiveAsOfAsync` + the event's own loaded `ParticipationRecords`

**Decision**: `EventAttendanceSheetService` calls `IEventRepository.GetByIdWithDetailsAsync` (which
already eager-loads `EventType` and `ParticipationRecords.Member` — used today by
`EventService.GetByIdWithDetailsAsync`/`EventDetail.razor`) for the event itself, and
`IMemberRepository.GetActiveAsOfAsync(evt.Date)` for the full roster, building a
`MemberId -> Participated` lookup from the loaded participation records exactly as
`AttendanceRollService.GenerateAsync` builds its `attendanceByMember` dictionary.

**Rationale**: No new repository method is required — both calls already exist and are already
used together in this exact shape by the Rehearsals precedent. `GetActiveAsOfAsync`'s documented
filter (`Status=Active AND ActivateDate <= date AND (InactivateDate IS NULL OR InactivateDate >
date) AND IsDeleted=false`) already excludes archived/soft-deleted members regardless of their
historical status on the event date, which is exactly FR-002/Acceptance Scenario 3's requirement.

**Alternatives considered**:
- Add a new `IParticipationRepository.GetByEventAsync` method — rejected: unnecessary, since
  `GetByIdWithDetailsAsync` already returns everything needed in one call and this is the same
  method `EventDetail.razor` already relies on for its own attendance table.

## Decision 5 — AGM sheet roster: reuse `IAgmAttendanceRepository.GetByAgmAsync` as-is

**Decision**: `AgmAttendanceSheetService` calls `IAgmRepository.GetByIdAsync` (existence check +
date) and `IAgmAttendanceRepository.GetByAgmAsync` (roster), with no new repository method.

**Rationale**: `GetByAgmAsync`'s existing implementation already eager-loads `Member` and orders by
`LastName` then `FirstName` — precisely FR-005/FR-006's requirements — so the service needs no
extra sorting or projection step beyond mapping each record to an `AgmAttendanceSheetMember`.

**Alternatives considered**: None needed — the existing method's contract already matches the
spec's requirement exactly; there is no simpler or more-direct alternative.

## Decision 6 — Reuse the same `RowsPerColumn` tuning as the rehearsal roll

**Decision**: `CheckboxSheetPdfBuilder` uses the same `RowsPerColumn = 32` constant
`AttendanceRollPdfRenderer` uses, at the same font size (10pt) and page margins (18pt/A4).

**Rationale**: Row height is driven by font size and cell padding, not by how many checkbox
columns exist per row, so the same per-column row capacity applies whether the row has one
checkbox column (this feature) or two (the rehearsal roll).

**Outstanding risk** (carried over from spec 012's own research.md): this constant was originally
tuned by a manual visual check, not derived from an exact QuestPDF measurement API. Implementation
should re-verify visually (per this repo's MAUI CDP screenshot workflow) rather than assume it
transfers perfectly, and adjust if a real print run shows overflow or excess whitespace.

## Decision 7 — Division of responsibility for the empty-state message (FR-009)

**Decision**: Neither new service throws for an empty roster — both return a valid DTO with an
empty `Members` list. Neither new renderer refuses to render a header-only, zero-member sheet if
called. The UI page's print handler is the sole place FR-009 is enforced: after calling
`GenerateAsync`, it checks `Members.Count == 0` and shows an inline message instead of calling
`Render`/writing a temp file/launching a viewer.

**Rationale**: Exact precedent match with `RehearsalList.razor.cs`'s `PrintRoll` handler and
`AttendanceRollService`/`AttendanceRollPdfRenderer`'s own documented division of responsibility
(spec 012's contract doc) — keeps both new services and both new renderers pure, side-effect-free,
and independently testable without needing to know about UI-facing messaging.

## Decision 8 — Print is a per-page-pair handler, not a shared cross-page component

**Decision**: `EventList`/`EventDetail` each get their own `PrintAttendanceSheet` handler in their
own `.razor.cs`; `AgmList`/`AgmDetail` each get their own `PrintAttendanceReport` handler. No new
shared Blazor component is introduced to host the button/handler once for all four pages.

**Rationale**: Matches how every other page-local action in this area already works —
`RecordParticipation`, `ArchiveAsync`, `OpenDetail` are each defined directly on the page that
needs them, not factored into a shared control, and the four print handlers here are only a few
lines each (generate → empty-state check → render → temp-file → `Process.Start`), identical in
shape to `RehearsalList.PrintRoll` but calling a different service/renderer pair per domain.

**Alternatives considered**: A shared `AttendanceSheetPrintButton` component parameterized by a
`Func<Task<byte[]>>` — rejected as premature abstraction for four call sites this small and
page-local elsewhere in the codebase; would also need to reach across the Decision 2 module
boundary to be genuinely reusable between Events and Agm pages.
