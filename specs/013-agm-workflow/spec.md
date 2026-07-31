# Feature Specification: AGM Workflow

**Feature Branch**: `013-agm-workflow`
**Created**: 2026-07-31
**Status**: Draft
**Source**: [GitHub issue #271](https://github.com/SteveTeece/StageFrightCommunity/issues/271) — "[FEATURE] Add AGM workflow to Events module"

## User Scenarios & Testing

### User Story 1 - Record an AGM's attendance and election results in one place (Priority: P1)

A committee coordinator holds the group's Annual General Meeting. Afterwards they open a dedicated "Record AGM" screen from the Events menu, enter the meeting date, mark which members attended, and record who was elected President, Secretary, Treasurer, any other defined office-holder position, and the general committee members — all in one workflow, without visiting each member's profile individually.

**Why this priority**: This is the problem stated in the issue — today a coordinator must record attendance on one screen and then edit members one at a time to record elected positions. Solving this in a single workflow is the entire point of the feature; everything else supports it.

**Independent Test**: Can be fully tested by opening the AGM screen, entering a meeting date, marking attendance for a set of members, assigning President/Secretary/Treasurer and one or two general committee members, and saving — then confirming the meeting record, attendance, and committee positions are all persisted and visible without touching the Members screens.

**Acceptance Scenarios**:

1. **Given** the coordinator is on the Events menu, **When** they select the AGM option, **Then** a "Record AGM" screen opens showing the meeting date and the list of active members for attendance.
2. **Given** the AGM screen is open with the member list loaded, **When** the coordinator marks members as attended (or uses "Select All"), **Then** each member's attendance status is tracked before saving.
3. **Given** attendance has been marked, **When** the coordinator assigns a member to President, Secretary, or Treasurer, **Then** that member cannot simultaneously be assigned to any other position or as a general committee member in the same AGM.
4. **Given** office holders and general committee members have been selected, **When** the coordinator saves the AGM, **Then** the meeting record, attendance records, and committee position records (for the meeting's year) are saved together, or none are saved if any part fails.
5. **Given** an AGM has already been saved, **When** the coordinator returns to that AGM's screen, **Then** the recorded attendance and election results are shown read-only, consistent with how saved attendance/participation is immutable elsewhere in the system.
6. **Given** the coordinator is marking attendance, **When** the member list is longer than fits on screen, **Then** the coordinator scrolls only within the member grid (using its own scroll bar) — the grid has no page-number controls and the rest of the page does not scroll.

---

### User Story 2 - Define the committee's office-holder positions ahead of time (Priority: P2)

Before running the AGM, a coordinator sets up the list of committee office-holder titles their group uses (beyond the built-in President, Secretary, and Treasurer) and the number of general committee member seats, so that the AGM screen offers the right set of positions to fill instead of requiring free-text entry each year.

**Why this priority**: This directly answers the issue's request to let the user "define additional committee office holders" and "the number of general committee members." It materially improves User Story 1 (faster, more consistent AGM entry) but User Story 1 remains usable without it — a coordinator can still type a custom position name during the AGM itself.

**Independent Test**: Can be tested independently by adding, renaming, reordering, and archiving custom office-holder titles and setting a general-committee-seat count in Settings, then confirming the values persist and are retrievable — without needing to run an actual AGM.

**Acceptance Scenarios**:

1. **Given** the coordinator opens the committee configuration area in Settings, **When** they add a new office-holder title (e.g., "Vice President"), **Then** it is saved and becomes available for selection on the next AGM screen.
2. **Given** one or more custom office-holder titles exist, **When** the coordinator archives one, **Then** it no longer appears as a selectable option on future AGMs but historical AGM records that used it are unaffected.
3. **Given** the coordinator sets the general committee member seat count (e.g., 5), **When** they open the AGM screen, **Then** the general committee member section shows how many of the target seats have been filled as members are selected.
4. **Given** President, Secretary, and Treasurer are built-in positions, **When** the coordinator views the office-holder configuration, **Then** those three cannot be renamed, reordered ahead of custom titles, or archived.

---

### User Story 3 - Review a past AGM's record (Priority: P3)

A coordinator or committee member wants to look back at a previous AGM to confirm when it was held, who attended, and who was elected, without digging through the Members module or the Committee Report.

**Why this priority**: Useful for accountability and historical reference, but the organisation's immediate pain (recording the outcomes) is already solved by User Story 1. Reviewing history is a natural but lower-urgency follow-on.

**Independent Test**: Can be tested by saving one or more AGM records (via User Story 1) and then confirming a list of past AGMs is browsable, each opening to show its date, attendance, and elected positions in read-only form.

**Acceptance Scenarios**:

1. **Given** one or more AGMs have been recorded, **When** the coordinator opens the AGM list, **Then** each entry shows at least the meeting date and attendance count, ordered most-recent-first.
2. **Given** the coordinator selects a past AGM from the list, **When** its detail view opens, **Then** attendance and elected positions for that meeting are displayed read-only.
3. **Given** an AGM record is no longer relevant (e.g., entered in error), **When** the coordinator archives it, **Then** it is hidden from the default AGM list but not permanently deleted, consistent with the system's soft-delete rule for non-financial records.

---

### Edge Cases

- What happens when the coordinator saves an AGM with zero members marked as attended? The meeting and any election results are still saved; attendance is simply recorded as zero.
- What happens if no members are assigned to a given office-holder position (including President/Secretary/Treasurer)? The position is saved as vacant for that year, consistent with how the existing Committee Report already displays unfilled named roles as "Vacant."
- What happens if a member is elected to a position without being marked as attended (e.g., elected by proxy or in absentia)? Attendance and election are tracked independently — a member may be elected without having attended, and may have attended without being elected.
- What happens if two AGMs are recorded in the same calendar year (e.g., an original meeting failed to reach quorum and was rerun)? Both meeting records are kept; the most recently saved AGM's election results become the year's current committee positions.
- What happens to a custom office-holder title that is archived after it was used in a past AGM? The historical AGM and committee records keep showing the archived title's name; it simply stops appearing as a choice for future AGMs.
- What happens to an AGM record or its attendance/election data if a member referenced by it is later archived? The historical AGM record is unaffected — attendance and election history are preserved as they were at the time.

## Requirements

### Functional Requirements

- **FR-001**: The system MUST provide a dedicated "Record AGM" entry point from the Events menu, separate from the existing generic "create event" flow.
- **FR-002**: An Annual General Meeting MUST be recorded as its own record type with its own date, not as an entry in the general Events list.
- **FR-003**: The system MUST no longer offer "Annual General Meeting" as a selectable type when creating a new (generic) event, while preserving any existing events historically tagged with that type.
- **FR-004**: The AGM screen MUST list active members for attendance marking, with the ability to mark individual members and to select/deselect all members at once.
- **FR-005**: The member attendance list on the AGM screen MUST NOT use page-number controls; instead the member grid MUST scroll independently (its own vertical scroll bar) while the rest of the page stays fixed.
- **FR-006**: The AGM screen MUST let the coordinator assign one member each to the President, Secretary, and Treasurer positions for the meeting's year, leaving a position vacant if no one is assigned.
- **FR-007**: The AGM screen MUST let the coordinator assign members to any additional office-holder positions defined for the organisation (User Story 2), and record a member with no specific title as a general committee member.
- **FR-008**: The system MUST prevent a single member from being assigned to more than one office-holder position or general-committee-member slot within the same AGM.
- **FR-009**: Saving an AGM MUST persist the meeting record, its attendance records, and its committee position results together as one atomic operation; if any part fails, none of it is saved.
- **FR-010**: Saving an AGM's election results MUST supersede any existing committee position records for that calendar year — members not reselected in this AGM are no longer recorded as holding a committee position for that year.
- **FR-011**: Once an AGM has been saved, its attendance and election results MUST be displayed read-only; the system MUST NOT provide an edit path for a saved AGM's attendance or election data (corrections happen by recording a superseding AGM, consistent with FR-010).
- **FR-012**: The system MUST let the coordinator define additional office-holder position titles beyond the built-in President, Secretary, and Treasurer, including adding, renaming, reordering, and archiving a title.
- **FR-013**: The built-in President, Secretary, and Treasurer positions MUST NOT be renamed, reordered, or archived.
- **FR-014**: The system MUST let the coordinator set a target number of general committee member seats, and the AGM screen MUST show how many of that target have been filled as selections are made.
- **FR-015**: The system MUST provide a browsable list of past AGMs, ordered most-recent-first, showing at minimum each meeting's date and attendance count.
- **FR-016**: The system MUST let the coordinator open a past AGM to view its attendance and elected positions read-only.
- **FR-017**: The system MUST allow a past AGM record to be archived (soft-deleted) rather than permanently removed, consistent with the system's soft-delete rule for non-financial records.
- **FR-018**: The existing annual committee-reset reminder, which currently triggers off an AGM recorded as a generic event, MUST be updated to trigger off AGMs recorded through this new workflow instead.
- **FR-019**: The system MUST include automated tests covering every reachable code path introduced by this feature (success, validation failure, exception, and boundary/null cases), per the project's exhaustive test-coverage rule.

## Key Entities

- **Annual General Meeting**: Represents one AGM sitting — meeting date, optional notes, and the standard archive/audit fields. Owns its attendance and, indirectly (via the committee position records for its year), its election results.
- **AGM Attendance Record**: Records whether one member attended one specific AGM. One record per (AGM, member) pair.
- **Committee Office-Holder Type**: A defined position title an organisation can elect members into at an AGM (e.g., "Vice President"), beyond the three built-in positions. Has a display order and can be archived.
- **Committee Position Record** *(existing — reused, not new)*: The elected outcome for one member, for one calendar year, in one named position (or blank for a general committee member). An AGM's saved election results are expressed as these records for the meeting's year.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A coordinator can record a complete AGM — attendance for all active members plus every office-holder and general-committee election — in a single workflow, with zero visits to individual member edit screens.
- **SC-002**: 100% of AGMs recorded through the new workflow appear in committee reporting (existing Committee Report) with the correct year, positions, and members, matching what was entered on the AGM screen.
- **SC-003**: Coordinators can define at least one custom office-holder title and have it available for selection on the very next AGM they record, with no restart or reconfiguration step required.
- **SC-004**: Reviewing any past AGM's attendance and election results takes no more than two navigation steps from the Events menu.
- **SC-005**: The member attendance grid on the AGM screen remains usable (no pagination, independently scrollable) regardless of how many active members exist.

## Assumptions

- "Define the number of general committee members" is interpreted as a configurable target seat count shown for guidance on the AGM screen (e.g., "4 of 5 general committee seats filled"), not a hard limit that blocks saving if unmet or exceeded — the coordinator may still record more or fewer members than the target.
- Election results are stored using the existing Committee Position Record structure (member, calendar year, position — blank position meaning general committee member); no changes to that record's shape are required, only new ways of producing it from the AGM screen.
- Because Committee Position Records are unique per (member, calendar year), a member can hold only one committee position per year — consistent with today's behaviour — so the "cannot be assigned twice in one AGM" rule (FR-008) simply carries that existing constraint into the new screen.
- Corrections to a saved AGM are handled by recording a new, superseding AGM (FR-010, FR-011) rather than by editing history in place, consistent with the project's pattern of immutable attendance/participation records elsewhere (Rehearsals, Events).
- The feature is scoped to attendance, office-holder elections, and general-committee-member elections only. The issue notes "additional items may need to be recorded in the future" — this spec does not add speculative fields for unspecified future outcomes, but nothing here precludes extending the Annual General Meeting record later.
- "Members" throughout this spec means active members, consistent with how attendance and participation are recorded elsewhere in the system (Rehearsals, Events).
