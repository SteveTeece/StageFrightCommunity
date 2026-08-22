# Feature Specification: Print Reports for Event and AGM Attendance

**Feature Branch**: `018-event-agm-attendance-reports`

**Created**: 2026-08-23

**Status**: Draft

**Input**: GitHub issue #302 ("[FEATURE] Add print buttons to Events"): "Users need to be able to print reports showing member participation at past events and AGMs, and attendance sheets for future events and AGMs. These reports should have a format similar to the existing rehearsal attendance sheet."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Print an event's attendance sheet (Priority: P1)

As a person who manages events, I want to print an attendance sheet for an event — blank and ready to mark by hand if it hasn't happened yet or hasn't had its participation recorded, or showing the real recorded result if it has — so I have an accurate paper record without keeping a separate manually updated list, in the same style as the existing rehearsal attendance sheet.

**Why this priority**: This is the change the GitHub issue is named for and the larger of the two domains in scope. It delivers the core "print buttons on Events" value on its own.

**Independent Test**: Can be fully tested by printing the sheet for an event whose participation hasn't been recorded yet and confirming every eligible member appears with a blank checkbox, then recording participation and reprinting the same event to confirm the checkboxes now show the real recorded result.

**Acceptance Scenarios**:

1. **Given** an event (past or future) for which participation has not yet been recorded, **When** the user prints its attendance sheet, **Then** every member active as of the event's date appears, sorted alphabetically by surname, each with a blank "Participated" checkbox.
2. **Given** an event for which participation has already been recorded, **When** the user prints its sheet, **Then** each listed member's checkbox matches their actual recorded participation.
3. **Given** a member is archived or soft-deleted, **When** the sheet is printed, **Then** that member does not appear on it.
4. **Given** no members are active as of the event's date, **When** the user attempts to print the sheet, **Then** the system shows an empty-state message instead of producing a blank sheet.

---

### User Story 2 - Print a past AGM's attendance report (Priority: P2)

As a person who manages AGMs, I want to print an attendance report for a recorded AGM, so I have a paper record of who attended without separately transcribing it, in the same style as the event and rehearsal sheets.

**Why this priority**: Extends the same printable-report capability to the AGM domain named in the issue. It is independently valuable but secondary to the core Events piece in User Story 1, and depends on nothing from it.

**Independent Test**: Can be fully tested by recording an AGM with a mix of attended and absent members, then printing its attendance report and confirming the checkboxes match exactly what was recorded.

**Acceptance Scenarios**:

1. **Given** a recorded AGM, **When** the user prints its attendance report, **Then** every member on that AGM's recorded attendance roster appears, sorted alphabetically by surname, with a checked box for members recorded as attended and an unchecked box for members recorded as absent.
2. **Given** an AGM with no attendance roster (e.g. it was recorded with zero active members at the time), **When** the user attempts to print its report, **Then** the system shows an empty-state message instead of producing a blank sheet.

---

### User Story 3 - Consistent, print-ready layout for both sheets (Priority: P3)

As a person printing these sheets, I want the event and AGM attendance sheets to use the same compact, print-friendly format as the existing rehearsal attendance roll — organization header, meeting title and date, surnames in capitals, minimal-width checkbox columns, and two-column overflow — so every printed roll in the system looks and behaves the same way.

**Why this priority**: A formatting and consistency refinement layered on top of Stories 1 and 2 — both sheets are already functional and printable without it, but matching the existing format is explicitly what the issue asks for.

**Independent Test**: Can be fully tested by generating an event sheet and an AGM sheet each with enough members to overflow a single column, and confirming both use the two-column layout, narrow checkbox columns, wrapping column headings, and capitalized surnames.

**Acceptance Scenarios**:

1. **Given** either sheet with enough members to fill more than one column, **When** it is generated, **Then** the alphabetically sorted list fills the first column before continuing into a second column on the same page, and onto further pages if still longer than one page.
2. **Given** either sheet, **When** it is reviewed, **Then** the checkbox column is visibly narrower than the name column, and every listed member's surname appears in capital letters.
3. **Given** the organization name is configured in Settings, **When** either sheet is generated, **Then** its header shows the organization name, a title identifying the report as an event or AGM attendance sheet, and the relevant date — without a "Generated: <timestamp>" line.

---

### Edge Cases

- An event of type "Annual General Meeting" is scheduled ahead of time (a future date): it is printed through the same event attendance sheet as any other event, since the system has no separate "future AGM" record — see Assumptions.
- A member is active as of the event's date but has no participation record yet: their "Participated" checkbox prints unchecked.
- Two members share the same surname: they are sub-sorted by first name so alphabetical order stays stable.
- An event or AGM sheet is reprinted later after data has changed: the event sheet recomputes membership as of the event's date and re-reads current participation each time; the AGM sheet always reflects the fixed attendance roster captured when the AGM was originally recorded, since that roster is never changed afterward.
- A past AGM has already been archived: it no longer appears on the Past AGMs list this feature prints from, so it is not reachable for printing through this feature's surfaces.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST let a user print an attendance sheet for any event, with the print action reachable from both the events list and the event's detail page.
- **FR-002**: The event attendance sheet MUST list every member active as of the event's date (archived and soft-deleted members excluded), regardless of whether the event's date is in the past or the future.
- **FR-003**: Each member row on the event sheet MUST include a "Participated" checkbox: unchecked if participation has not yet been recorded for that event, and matching the real recorded value once participation has been recorded.
- **FR-004**: System MUST let a user print an attendance report for any past (already recorded) AGM, with the print action reachable from both the Past AGMs list and the AGM's detail page.
- **FR-005**: The AGM attendance report MUST list every member on that AGM's recorded attendance roster — the fixed set of members captured when the AGM was saved — each with a checkbox matching their actual recorded attended/absent status.
- **FR-006**: Both sheets MUST display each listed member's surname in capital letters alongside their first name, sorted alphabetically by surname then by first name for members who share a surname.
- **FR-007**: Both sheets MUST render in a two-column, print-ready layout: checkbox columns at minimal width relative to the name column, column headings that wrap to fit their column rather than widening it or being truncated, and the member list overflowing into a second column (and further pages) once it exceeds one column.
- **FR-008**: Both sheets MUST show a header identifying the organization (when one is configured in Settings), a title identifying the sheet as an event or AGM attendance report, and the relevant date — and MUST NOT display a "Generated: <timestamp>" line.
- **FR-009**: If there is nobody to list — no members active as of the event's date, or an AGM whose attendance roster is empty — the system MUST show an empty-state message instead of producing a blank sheet.
- **FR-010**: Printing either sheet MUST be a read-only operation: it MUST NOT create, modify, or delete any Event, AnnualGeneralMeeting, ParticipationRecord, AgmAttendanceRecord, Member, Fee, Payment, or Transaction record.
- **FR-011**: Both sheets MUST be produced in a form suitable for printing, consistent with how other printable reports in the system (including the existing rehearsal attendance roll) are generated and handed off to the user's default viewer.

### Key Entities *(include when the feature involves data)*

- **Event**: Existing entity. Supplies the date and event type used to identify the printed sheet, and determines event-sheet membership (members active as of its date). Unchanged by this feature.
- **ParticipationRecord**: Existing entity. Read to determine whether each member's "Participated" checkbox is checked, once participation has been recorded for the event. This feature only reads it.
- **AnnualGeneralMeeting**: Existing entity. Supplies the date and notes used to identify the printed AGM report. Unchanged by this feature.
- **AgmAttendanceRecord**: Existing entity. Read to populate the AGM sheet's fixed member roster and each member's attended/absent checkbox. This feature only reads it.
- **Member**: Existing entity. Surname, first name, and (for the event sheet only) activation/inactivation history determine who is listed and how their name is displayed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can generate and print an event's attendance sheet, or a past AGM's attendance report, in a few clicks, without maintaining any separate manual roll.
- **SC-002**: 100% of members active as of an event's date appear on that event's printed sheet, and no archived or soft-deleted members appear.
- **SC-003**: When an event's participation has not yet been recorded, 100% of "Participated" checkboxes print blank; once recorded, 100% of checkboxes match the actual recorded value.
- **SC-004**: 100% of members on a recorded AGM's attendance roster appear on its printed report, with checkboxes matching actual recorded attendance with 100% accuracy.
- **SC-005**: Members appear in correct alphabetical surname order, with surnames in capitals, on 100% of generated sheets.
- **SC-006**: For a typical event or AGM roster size, the two-column layout keeps a printed sheet to a single page where a single-column layout would need two.

## Assumptions

- The system has no "scheduled future AGM" entity — an `AnnualGeneralMeeting` record only exists once it is recorded (with its attendance and elected positions) after the meeting takes place. A forward-looking "AGM attendance sheet" is therefore produced the same way as for any other future gathering: by scheduling an `Event` of type "Annual General Meeting" and printing its event attendance sheet ahead of the meeting, then recording the AGM itself afterward as today's workflow already does.
- The AGM attendance report always reflects the fixed member roster captured at the moment the AGM was recorded — it is never recomputed against current active membership, since `AgmAttendanceRecord` rows are immutable once saved.
- Print actions are added to both the list and detail pages for events, and both the list and detail pages for AGMs, so a user can print from wherever they are already reviewing the record.
- Neither sheet needs a spreadsheet-style export; like the rehearsal roll, its value is the printable checkbox layout, either for hand-marking ahead of the event or as a compact printed record afterward.
- Printing either sheet never creates, updates, or deletes any Event, AnnualGeneralMeeting, ParticipationRecord, AgmAttendanceRecord, Member, Fee, Payment, or Transaction record.
