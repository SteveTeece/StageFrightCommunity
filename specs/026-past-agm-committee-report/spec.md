# Feature Specification: Past AGM Committee List & Print Report

**Feature Branch**: `026-past-agm-committee-report`

**Created**: 2026-08-26

**Status**: Draft

**Input**: User description: "Change the way the past AGM view lists the general committee members: place the member names into a list box with one person per line instead of a comma-separated list. Also add a print report button for past AGM views. (issue #307)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read general committee members one per line (Priority: P1)

A coordinator opens a past (already-recorded) AGM to check who was on the general committee that year. Today the names run together in one comma-separated sentence, which is hard to scan when there are several members. The coordinator instead sees each general committee member's name on its own line inside a bordered list, the same way other member lists in the app are shown.

**Why this priority**: This is the cosmetic fix the issue leads with, affects every recorded AGM with general committee members, and is the lowest-risk, highest-frequency read that coordinators perform on this screen.

**Independent Test**: Open a recorded AGM that has two or more general committee members (no elected office). Confirm the names appear as separate lines in a bordered list rather than one comma-separated line, and that named office-holder positions (President, Secretary, etc.) are unaffected.

**Acceptance Scenarios**:

1. **Given** a recorded AGM with three general committee members, **When** the coordinator opens that AGM's detail view, **Then** the three names appear as three separate lines inside a bordered list box, not as one comma-separated line.
2. **Given** a recorded AGM with no general committee members (only named office holders), **When** the coordinator opens that AGM's detail view, **Then** no general committee list box is shown (matching today's behavior of omitting the line entirely).
3. **Given** a recorded AGM with a single general committee member, **When** the coordinator opens that AGM's detail view, **Then** that one name appears as a single line inside the list box.

---

### User Story 2 - Print a past AGM's results (Priority: P2)

A coordinator needs a paper or PDF copy of a past AGM's outcome — the date, how many members attended, and who was elected to each position (including the general committee) — to file with the meeting minutes or hand to a committee member. Today only an attendance sheet (who showed up) can be printed from this screen; there is no way to print the elected results themselves.

**Why this priority**: Valuable and explicitly requested, but secondary to the P1 readability fix — it adds a new capability rather than fixing an existing one, and depends on the same position data reorganized by User Story 1.

**Independent Test**: Open a recorded AGM, select the new print button, and confirm a document is produced showing the AGM date, attendance count, and every elected position (including general committee members listed individually) without needing any other screen.

**Acceptance Scenarios**:

1. **Given** a recorded AGM with elected positions, **When** the coordinator selects the new "Print AGM Results" button, **Then** a printable document is generated showing the AGM date, the attendance count, and every elected position with its member(s) — general committee members listed one per line.
2. **Given** a recorded AGM with no elected positions recorded, **When** the coordinator selects the print button, **Then** the document is still generated, showing the AGM date and attendance count with a "No positions recorded" line in place of a position list.
3. **Given** an AGM that has not yet been recorded (scheduled only), **When** the coordinator views it, **Then** the new print button is still shown alongside the existing attendance-report button (both are always present on the detail view, matching today's behavior of the attendance-report button), and selecting it produces a document with a "No positions recorded" line.

### Edge Cases

- A general committee member's name that is unusually long must still wrap/fit within the list box row without breaking the layout, consistent with how other list boxes in the app already handle long names.
- If print generation fails (e.g., the document cannot be written), the coordinator sees the same kind of inline warning message already used for the existing attendance-report print failure, and the screen otherwise remains usable.
- The two print buttons (existing attendance report, new results report) must remain independently operable — printing one does not disable or affect the other.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The AGM detail view MUST display general committee members (positions with no named office-holder title) as individual lines within a bordered list box, one member per line, instead of a single comma-separated line.
- **FR-002**: The AGM detail view MUST continue to display named office-holder positions (e.g., President, Secretary, Treasurer, and any other titled position) exactly as today — unaffected by this change.
- **FR-003**: When an AGM has no general committee members recorded, the AGM detail view MUST omit the general committee list box entirely, consistent with current behavior of omitting the line.
- **FR-004**: The AGM detail view MUST provide a new "Print AGM Results" action in the same header area as the existing "Print Attendance Report" action, shown whenever the detail view is loaded (both actions share the same, always-present visibility today).
- **FR-005**: Selecting the new print action MUST produce a printable document containing: the AGM date, the attendance count (attended out of total), and every elected position with its member(s) — with general committee members listed individually rather than comma-separated, mirroring FR-001.
- **FR-006**: When an AGM has no positions recorded at all, the printed document MUST still be generated and MUST show a "No positions recorded" line in place of the position list, matching the on-screen empty-state wording.
- **FR-007**: If document generation fails, the AGM detail view MUST show an inline warning message and MUST leave the rest of the screen usable, consistent with the existing attendance-report failure handling.
- **FR-008**: The new print action MUST NOT alter or replace the existing "Print Attendance Report" action — both remain available side by side.

### Key Entities

- **AGM (Annual General Meeting)**: A past, recorded meeting with a date, an attendance count, and zero or more elected positions. This feature only affects how a single already-recorded AGM's positions are displayed and printed.
- **Elected Position**: A record of one member holding either a named office (e.g., President) or an unnamed general-committee seat for a given AGM/term. Named positions and general-committee seats are already grouped separately today; this feature changes only how the general-committee group is displayed and printed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A coordinator can identify every individual general committee member on a past AGM by scanning a vertical list, with zero need to mentally split a comma-separated sentence.
- **SC-002**: A coordinator can produce a printable record of a past AGM's full election results (date, attendance, positions) in one action, without navigating away from the AGM detail view.
- **SC-003**: Existing attendance-report printing continues to work unchanged for 100% of past AGMs after this change ships.

## Assumptions

- "Past AGM view" refers to the existing read-only AGM detail screen (`/events/agm/{id}`), which already hosts the "Print Attendance Report" button and the "Elected Positions" section once an AGM is recorded; the same screen also handles not-yet-recorded (scheduled) AGMs, and the new print button follows that screen's existing always-present button visibility rather than adding new conditional logic.
- The new print button produces a new, separate printable document (AGM date + attendance count + elected positions) rather than reusing or renaming the existing attendance-report button — confirmed with the user during specification.
- The new document does not need to appear in the general Reports module's report list; it is scoped to a single AGM the same way the existing attendance sheet is, since it is generated from data already loaded for the AGM detail view.
- Named office-holder positions with multiple holders (e.g., a mid-term special election) keep their existing display format; only the unnamed "General Committee Member" group changes to one-per-line, since that is the specific group the issue calls out.
- No changes are required to how AGMs are recorded, edited, or archived — this feature only affects the read-only detail view's display and printing.

## Approach

- `src/StageFright.UI/Pages/Events/AgmDetail.razor.cs` — `BuildPositionLines` currently appends a "General Committee Member" line whose `MemberText` is a comma-joined string; stop it emitting that line and add a new `GeneralCommitteeMemberNames` property (`_positions.Where(p => p.OfficeHolderTypeId is null)`, sorted the same way) returning the plain name list for the razor and the new renderer to share. `PositionLines` keeps handling only named office-holder positions, unchanged.
- `src/StageFright.UI/Pages/Events/AgmDetail.razor` — after the `PositionLines` `<ul>`, when `GeneralCommitteeMemberNames.Count > 0`, render a `BorderedListBox<string>` (`src/StageFright.UI/Shared/BorderedListBox.razor`, per CLAUDE.md's list-box standard) with one `<span>` row per name, under a "General Committee Member" label; when the list is empty, render nothing (FR-003). The "No positions recorded." fallback now triggers only when both `PositionLines` and `GeneralCommitteeMemberNames` are empty.
- New DTOs in `StageFright.Core/Modules/Agm/` (one class per file, `sealed`, `init`-only, mirroring `AgmAttendanceSheetData`/`AgmAttendanceSheetMember`): `AgmResultsData` (AgmDate, AttendedCount, TotalCount, `IReadOnlyList<AgmResultsPositionLine>` PositionLines, `IReadOnlyList<string>` GeneralCommitteeMemberNames) and `AgmResultsPositionLine` (Label, MemberText).
- New `IAgmResultsPdfRenderer`/`AgmResultsPdfRenderer` in `StageFright.Reports/Rendering/`, registered in `MauiProgram.RegisterCoreServices` next to `IAgmAttendanceSheetPdfRenderer`. Builds the PDF directly with QuestPDF (header: org name / "AGM Results" / date / attendance count; body: one line per named position, then "General Committee Member" plus one line per name, or "No positions recorded." when both are empty; footer: page numbers) — following `PdfReportRenderer`'s page setup, not `CheckboxSheetPdfBuilder` (that helper is checkbox-roll-specific and doesn't fit a plain position list, per CLAUDE.md's Reports-pipeline note on the existing print-only sheets).
- `AgmDetail.razor.cs` — inject `IAgmResultsPdfRenderer`, add a `PrintAgmResults` handler that builds `AgmResultsData` from the already-loaded `_agm`/`_attendance`/`PositionLines`/`GeneralCommitteeMemberNames` (no new repository calls needed), renders to a temp PDF, and opens it via `Process.Start` — copying `PrintAttendanceReport`'s existing try/catch-and-inline-warning shape exactly (FR-007), so both handlers behave identically on failure. Add a second header button ("Print AGM Results") next to the existing "Print Attendance Report" button, wired to the new handler.
- `CLAUDE.md`'s Reports pipeline paragraph already lists `AgmAttendanceSheetPdfRenderer` among the print-only sheets built outside `IReportProvider`; add `AgmResultsPdfRenderer` to that same sentence so the doc stays accurate.
- Tests: extend `tests/StageFright.UI.Tests/Pages/Events/AgmDetailTests.cs` for the list-box rendering (one row per general committee member, no box when empty, named positions unaffected) and the new button (renders, empty-position "No positions recorded" document still generated conceptually via the mocked renderer being called, service-throws path shows the inline warning) — mirroring the existing Print Attendance Report tests' structure (mock the renderer, never let a happy-path click reach real `Process.Start`). Add `tests/StageFright.Reports.Tests/AgmResultsPdfRendererTests.cs` mirroring `AgmAttendanceSheetPdfRendererTests.cs` (non-null/non-empty bytes for populated, zero-position, and empty-organization-name input; no exception).
- Manual verification: launch the app, open a recorded AGM with several general committee members and at least one named position, confirm the list box renders one name per line, click both print buttons and confirm two distinct PDFs open, in both light and dark theme.
