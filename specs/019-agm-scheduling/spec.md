# Feature Specification: Schedule Future AGMs

**Feature Branch**: `019-agm-scheduling`

**Created**: 2026-08-23

**Status**: Draft

**Input**: GitHub issue #312 ("[FEATURE] Add ability to create future AGM schedule"): "The user needs to plan for future AGMs. These need to be created and saved similar to future events and rehearsals are created before marking attendance." Follow-up comment: "The user also needs to be able to print a blank AGM Attendance report before the attendance is recorded. This is similar functionality to the Rehearsal Attendance report."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Schedule a future AGM ahead of time (Priority: P1)

As a committee administrator planning ahead, I want to create and save an AGM's meeting date (and optional notes) before the meeting happens — the same way events and rehearsals are scheduled ahead of marking attendance — so the meeting exists on the calendar and its attendance report can be prepared in advance.

**Why this priority**: This is the change the issue is named for. Without a way to save an AGM ahead of time, nothing else in this spec has anything to attach to.

**Independent Test**: Schedule an AGM for a future date with no attendance or elections entered, confirm it saves immediately and appears in the AGM list as not-yet-recorded, and confirm scheduling alone creates no attendance records, elected positions, or committee term.

**Acceptance Scenarios**:

1. **Given** the AGM list, **When** the user schedules a new AGM by entering only a meeting date and optional notes, **Then** the AGM is saved immediately and appears in the AGM list with no recorded attendance or elected positions.
2. **Given** a scheduled AGM whose date is in the future, **When** the user views the AGM list or its detail page, **Then** it is clearly shown as not yet recorded, distinct from a fully recorded AGM.
3. **Given** a scheduled AGM, **When** the user views its detail page before attendance is recorded, **Then** only the meeting date and notes are shown — no attendance count and no elected positions.

---

### User Story 2 - Record attendance and elections once the AGM has happened (Priority: P1)

As a committee administrator, I want to record attendance and committee elections against an AGM I already scheduled, once the meeting has actually taken place, so the same meeting record carries both its planned date and its real outcome instead of creating a duplicate entry.

**Why this priority**: Equally essential to Story 1 — scheduling ahead only has value if the same record can later be completed with its real outcome. Together, these two stories replace today's single-step "record everything at once" workflow with the scheduled-then-recorded flow the issue asks for.

**Independent Test**: Schedule an AGM, then record its attendance and elections once its date has arrived; confirm the same AGM record now shows attendance counts and elected positions and that a new committee term has started; confirm recording it again, or recording it before its date, is rejected.

**Acceptance Scenarios**:

1. **Given** a previously scheduled AGM whose date has arrived, **When** the user records its attendance and committee elections, **Then** the same AGM record is updated with the attendance roster and elected positions, and a new committee term begins as today's workflow already does.
2. **Given** a scheduled AGM whose date is still in the future, **When** the user attempts to record its attendance and elections, **Then** the system rejects the attempt with a clear message and nothing is saved.
3. **Given** an AGM that has already been recorded, **When** the user attempts to record it a second time, **Then** the system rejects the attempt rather than creating duplicate attendance records or a second committee term.

---

### User Story 3 - Print a blank attendance report for a scheduled AGM (Priority: P2)

As a committee administrator, I want to print a blank attendance report for an AGM that has been scheduled but not yet recorded, listing every currently active member with an empty checkbox, so I can take it to the meeting and mark attendance by hand — the same way the rehearsal attendance report works before attendance is taken.

**Why this priority**: Directly requested in the issue's follow-up comment, and depends on Story 1 existing (there must be a scheduled AGM to print for), but is a secondary convenience layered on top of the core scheduling capability.

**Independent Test**: Schedule an AGM and print its attendance report before recording anything, confirming every currently active member appears with an unchecked box; then record attendance and reprint the same AGM to confirm the report now shows the real recorded roster and checkmarks.

**Acceptance Scenarios**:

1. **Given** a scheduled AGM that has not yet been recorded, **When** the user prints its attendance report, **Then** every currently active member appears, sorted alphabetically by surname then first name, each with an unchecked box.
2. **Given** the same AGM after its attendance has been recorded, **When** the user reprints its attendance report, **Then** the report now shows the fixed recorded roster with checkmarks matching actual attendance, exactly as today's recorded-AGM report already does.
3. **Given** a scheduled AGM with no members currently active, **When** the user attempts to print its report, **Then** the system shows an empty-state message instead of producing a blank report.

---

### Edge Cases

- A scheduled AGM's date passes without attendance ever being recorded: it stays listed indefinitely as not-yet-recorded; nothing auto-completes or auto-expires it.
- Two AGMs are scheduled with the same date: both are allowed and listed separately, matching how events and rehearsals permit same-date entries.
- A scheduled (not-yet-recorded) AGM is archived: archiving remains available and removes it from the list without requiring attendance to have been recorded first.
- A member becomes active or inactive between when an AGM is scheduled and when its blank attendance report is printed: the report always reflects membership as of the moment it is printed, not the moment the AGM was scheduled.
- An AGM that already has a fully recorded attendance roster of zero members (recorded at a time nobody was active) is correctly shown as recorded, not as still-scheduled.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST let a user schedule an AGM by entering only its meeting date and optional notes, saving it immediately without requiring attendance or committee elections to be entered at the same time.
- **FR-002**: Scheduling an AGM MUST NOT create any attendance record, elected committee position, or committee term — those are created only when attendance and elections are later recorded against it.
- **FR-003**: The AGM list MUST show every AGM, scheduled and recorded alike, with a clear indicator of whether each one's attendance and elections have been recorded yet.
- **FR-004**: System MUST let a user record attendance and committee elections against a previously scheduled AGM, on or after its meeting date, updating that same AGM record rather than creating a new one.
- **FR-005**: System MUST reject an attempt to record attendance and elections for an AGM whose date has not yet arrived, leaving the AGM unchanged.
- **FR-006**: System MUST reject an attempt to record attendance and elections for an AGM that has already been recorded, leaving its existing attendance, elections, and committee term unchanged.
- **FR-007**: Recording attendance and elections against a scheduled AGM MUST keep today's existing rules in force: no member may hold more than one committee assignment from the same AGM, the previously open committee term is closed on the AGM's date, and a new committee term begins.
- **FR-008**: An AGM's detail page MUST show only its meeting date and notes while it is still scheduled (not yet recorded), and MUST add attendance counts and elected positions once it has been recorded.
- **FR-009**: System MUST let a user print an attendance report for any AGM, scheduled or recorded, with the print action reachable from both the AGM list and the AGM's detail page.
- **FR-010**: The attendance report for a scheduled (not-yet-recorded) AGM MUST list every member currently active, sorted alphabetically by surname then first name, each with an unchecked box.
- **FR-011**: The attendance report for a recorded AGM MUST continue to list the fixed roster captured at the moment it was recorded, exactly as today, unaffected by later membership changes.
- **FR-012**: If a scheduled AGM's attendance report would have nobody to list — no members currently active — the system MUST show an empty-state message instead of producing a blank report.
- **FR-013**: Archiving MUST remain available for both scheduled and recorded AGMs; archiving a scheduled (not-yet-recorded) AGM MUST NOT require attendance to have been recorded first.
- **FR-014**: Printing an attendance report MUST remain a read-only operation for both scheduled and recorded AGMs — it MUST NOT create, modify, or delete any AnnualGeneralMeeting, AgmAttendanceRecord, CommitteePositionRecord, CommitteeTerm, or Member record.

### Key Entities *(include when the feature involves data)*

- **AnnualGeneralMeeting**: Existing entity, extended to distinguish a scheduled (not-yet-recorded) meeting from a fully recorded one. Carries the meeting date and notes from the moment it is scheduled; attendance, elected positions, and the committee term it starts are only added once recording happens.
- **AgmAttendanceRecord**: Existing entity, unchanged in shape — now only created at recording time rather than at scheduling time.
- **CommitteePositionRecord / CommitteeTerm**: Existing entities, unchanged in shape — only created at recording time (same as today), now decoupled from the AGM's initial creation.
- **Member**: Existing entity. Supplies the currently-active roster used for a scheduled AGM's blank attendance report.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can schedule a future AGM in a few clicks, entering only a date and optional notes, without being forced to also enter attendance or elections at the same time.
- **SC-002**: 100% of scheduled AGMs appear on the AGM list clearly marked as not-yet-recorded until attendance and elections are recorded against them.
- **SC-003**: 100% of attempts to record attendance/elections before an AGM's date, or a second time against an already-recorded AGM, are rejected with no data change.
- **SC-004**: A user can print a ready-to-mark-by-hand attendance report for a scheduled AGM before any attendance is recorded, listing 100% of currently active members with blank checkboxes; once recorded, the same report reflects the fixed recorded roster with 100% accuracy.
- **SC-005**: Recording attendance and elections against a previously scheduled AGM produces the same outcome (attendance roster, elected positions, new committee term) as today's single-step recording, with 100% of existing AGM-workflow rules (duplicate-assignment rejection, term rollover) still enforced.

## Assumptions

- A scheduled AGM cannot be edited or rescheduled once saved — consistent with Events and Rehearsals, neither of which offers an edit/reschedule capability today. A mis-scheduled AGM is archived and a new one scheduled in its place.
- A scheduled AGM's date may be set in the past as well as the future, matching how Events and Rehearsals are scheduled without a date restriction — this also supports catching up on a historical AGM that was never entered.
- Whether an AGM is "scheduled" or "recorded" is tracked directly on the AnnualGeneralMeeting record itself, not inferred from whether attendance rows exist — so an AGM recorded with zero active members is still correctly shown as recorded rather than as still-scheduled.
- The existing Rehearsal Attendance report referenced in the issue is the direct model for the scheduled-AGM attendance report: blank checkboxes before recording, real values after. This reuses the AGM attendance report already built by the prior print-reports feature rather than introducing a new report.
- The special-election workflow (mid-term committee replacements) is unaffected by this change — it already operates against an existing committee term regardless of how the AGM that started that term was created.
