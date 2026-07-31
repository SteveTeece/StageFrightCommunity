# Feature Specification: AGM Workflow

**Feature Branch**: `013-agm-workflow`
**Created**: 2026-07-31
**Status**: Draft
**Source**: [GitHub issue #271](https://github.com/SteveTeece/StageFrightCommunity/issues/271) — "[FEATURE] Add AGM workflow to Events module"
**Related issues**: [#290](https://github.com/SteveTeece/StageFrightCommunity/issues/290) — "Add committee settings and definitions to Startup wizard"; [#292](https://github.com/SteveTeece/StageFrightCommunity/issues/292) — "Separate committee service years from calendar years"

## User Scenarios & Testing

### User Story 1 - Record an AGM's attendance and election results in one place (Priority: P1)

A committee coordinator holds the group's Annual General Meeting. Afterwards they open a dedicated "Record AGM" screen from the Events menu, enter the meeting date, mark which members attended, and record who was elected President, Secretary, Treasurer, any other defined office-holder position, and the general committee members — all in one workflow, without visiting each member's profile individually.

**Why this priority**: This is the problem stated in the issue — today a coordinator must record attendance on one screen and then edit members one at a time to record elected positions. Solving this in a single workflow is the entire point of the feature; everything else supports it.

**Independent Test**: Can be fully tested by opening the AGM screen, entering a meeting date, marking attendance for a set of members, assigning President/Secretary/Treasurer and one or two general committee members, and saving — then confirming the meeting record, attendance, and committee positions are all persisted and visible without touching the Members screens.

**Acceptance Scenarios**:

1. **Given** the coordinator is on the Events menu, **When** they select the AGM option, **Then** a "Record AGM" screen opens showing the meeting date and the list of active members for attendance.
2. **Given** the AGM screen is open with the member list loaded, **When** the coordinator marks members as attended (or uses "Select All"), **Then** each member's attendance status is tracked before saving.
3. **Given** attendance has been marked, **When** the coordinator assigns a member to President, Secretary, or Treasurer, **Then** that member cannot simultaneously be assigned to any other position or as a general committee member in the same AGM.
4. **Given** office holders and general committee members have been selected, **When** the coordinator saves the AGM, **Then** the meeting record, attendance records, and committee position records (for the meeting's resulting committee term) are saved together, or none are saved if any part fails.
5. **Given** an AGM has already been saved, **When** the coordinator returns to that AGM's screen, **Then** the recorded attendance and election results are shown read-only, consistent with how saved attendance/participation is immutable elsewhere in the system.
6. **Given** the coordinator is marking attendance, **When** the member list is longer than fits on screen, **Then** the coordinator scrolls only within the member grid (using its own scroll bar) — the grid has no page-number controls and the rest of the page does not scroll.

---

### User Story 2 - Define committee positions and office-holder titles ahead of time, from Settings or initial setup (Priority: P2)

Before running the AGM, a coordinator sets up the list of committee office-holder titles their group uses (beyond the built-in President, Secretary, and Treasurer) and the number of general committee member seats, so that the AGM screen offers the right set of positions to fill instead of requiring free-text entry each year. This configuration is available both from the Settings committee configuration area and as a step in the first-run setup wizard, so a brand-new installation can be ready for its first AGM without a detour through Settings afterwards.

**Why this priority**: This directly answers the issue's request to let the user "define additional committee office holders" and "the number of general committee members," including doing so "as part of the startup wizard" (issue #290) as well as in Settings. It materially improves User Story 1 (faster, more consistent AGM entry) but User Story 1 remains usable without it — a coordinator can still type a custom position name during the AGM itself.

**Independent Test**: Can be tested independently by adding, renaming, reordering, and archiving custom office-holder titles and setting a general-committee-seat count — both from Settings and from the setup wizard — then confirming the values persist and are retrievable from either surface, without needing to run an actual AGM.

**Acceptance Scenarios**:

1. **Given** the coordinator opens the committee configuration area in Settings, **When** they add a new office-holder title (e.g., "Vice President"), **Then** it is saved and becomes available for selection on the next AGM screen.
2. **Given** one or more custom office-holder titles exist, **When** the coordinator archives one, **Then** it no longer appears as a selectable option on future AGMs but historical AGM records that used it are unaffected.
3. **Given** the coordinator sets the general committee member seat count (e.g., 5), **When** they open the AGM screen, **Then** the general committee member section shows how many of the target seats have been filled as members are selected.
4. **Given** President, Secretary, and Treasurer are built-in positions, **When** the coordinator views the office-holder configuration, **Then** those three cannot be renamed, reordered ahead of custom titles, or archived.
5. **Given** a brand-new installation is running the first-run setup wizard, **When** the coordinator reaches the committee configuration step, **Then** they can set the general committee seat count and add office-holder titles before finishing setup, using the same underlying values as the Settings committee configuration area.
6. **Given** the coordinator leaves the committee configuration step at its defaults during setup, **When** they finish the wizard, **Then** setup completes normally and the seat count and office-holder titles can still be added or changed later from Settings, with no difference in outcome.

---

### User Story 3 - Set the AGM month so committee terms follow the AGM cycle, not the calendar year (Priority: P2)

During setup, the coordinator selects the month in which the group's AGM is normally held. Because committee terms run from one AGM to the next rather than following the calendar year, the system uses this AGM month to work out when each committee term starts and ends, and to label the term with the calendar year most of it falls within — for example, members elected at an October AGM serve a term that starts in October and is recorded as the following year's committee, since most of that term's days fall in the following year.

**Why this priority**: Without this, every committee position saved by the new AGM workflow (User Story 1) is filed under the calendar year it happened to be created in rather than the year its service actually belongs to, which quietly corrupts the committee reporting this feature exists to feed. It is independent of the office-holder/seat configuration in User Story 2, so it can be built and verified on its own, but it should land early since Story 1's save behaviour depends on it.

**Independent Test**: Set the AGM month to October during setup, record two AGMs roughly a year apart, and confirm each resulting committee term is dated from its AGM to the next one (not 1 January – 31 December) and labeled with the correct calendar year — verifiable without needing the special-election workflow in User Story 4.

**Acceptance Scenarios**:

1. **Given** the coordinator is in the first-run setup wizard, **When** they reach the AGM configuration step, **Then** they can select the calendar month in which the AGM is normally held.
2. **Given** the AGM month is set to October, **When** an AGM is recorded and its election results saved, **Then** the resulting committee term is recorded as running from that AGM's date to the date of the next AGM, not from 1 January to 31 December.
3. **Given** an October AGM starts a new committee term, **When** the term is labeled, **Then** it is labeled with the calendar year containing the majority of its days (the year following the AGM, for an October AGM).
4. **Given** a committee term is in progress, **When** the coordinator views a report or screen that names "the committee" for that term's label year, **Then** the members and positions shown are those elected at the AGM that started the term, regardless of what the current calendar month is.

---

### User Story 4 - Record a mid-term replacement when a committee member or office holder leaves (Priority: P3)

When a committee member or office holder leaves the organisation before the next AGM, the coordinator holds a special election and records the replacement directly, without waiting for or re-running the full AGM workflow. Both the outgoing and incoming holder's service are kept for that committee term, each shown with the dates they actually served.

**Why this priority**: This handles a real but comparatively infrequent event — the standard AGM path (User Story 1) covers most terms end-to-end. It sits behind the core AGM workflow and committee configuration (User Stories 1–3), but must work correctly whenever a departure does happen, since without it the committee record would silently show someone who has already left as still holding the position.

**Independent Test**: Record an AGM (User Story 1) assigning a member to a position, then use the special-election path to record a replacement partway through that term, and confirm both the outgoing and incoming holder are retained for the term with correct start/end dates, and that both dates appear together with each name wherever that position is displayed or printed.

**Acceptance Scenarios**:

1. **Given** a committee term is in progress and one of its position holders has left, **When** the coordinator records a special election for a replacement, **Then** the incoming member is recorded in that position for the remainder of the current term.
2. **Given** a special election has been recorded for a position, **When** that position is displayed or printed, **Then** both the outgoing and incoming holder appear, each with their own start and end date of service in that position.
3. **Given** a position has had exactly one, unbroken holder for the whole term, **When** it is displayed or printed, **Then** no start/end dates are shown for it — dates only appear once a position has had more than one holder in the same term.
4. **Given** the coordinator saves a special election, **When** the outgoing holder's record is closed out, **Then** it is given an end date and preserved, never deleted, consistent with the system's soft-delete/immutability rules.

---

### User Story 5 - Review a past AGM's record (Priority: P3)

A coordinator or committee member wants to look back at a previous AGM to confirm when it was held, who attended, and who was elected, without digging through the Members module or the Committee Report.

**Why this priority**: Useful for accountability and historical reference, but the organisation's immediate pain (recording the outcomes) is already solved by User Story 1. Reviewing history is a natural but lower-urgency follow-on.

**Independent Test**: Can be tested by saving one or more AGM records (via User Story 1) and then confirming a list of past AGMs is browsable, each opening to show its date, attendance, and elected positions in read-only form.

**Acceptance Scenarios**:

1. **Given** one or more AGMs have been recorded, **When** the coordinator opens the AGM list, **Then** each entry shows at least the meeting date and attendance count, ordered most-recent-first.
2. **Given** the coordinator selects a past AGM from the list, **When** its detail view opens, **Then** attendance and elected positions for that meeting are displayed read-only, including each position's service start/end dates whenever a mid-term replacement occurred (User Story 4).
3. **Given** an AGM record is no longer relevant (e.g., entered in error), **When** the coordinator archives it, **Then** it is hidden from the default AGM list but not permanently deleted, consistent with the system's soft-delete rule for non-financial records.

---

### Edge Cases

- What happens when the coordinator saves an AGM with zero members marked as attended? The meeting and any election results are still saved; attendance is simply recorded as zero.
- What happens if no members are assigned to a given office-holder position (including President/Secretary/Treasurer)? The position is saved as vacant for that term, consistent with how the existing Committee Report already displays unfilled named roles as "Vacant."
- What happens if a member is elected to a position without being marked as attended (e.g., elected by proxy or in absentia)? Attendance and election are tracked independently — a member may be elected without having attended, and may have attended without being elected.
- What happens if two AGMs are recorded in the same calendar year (e.g., an original meeting failed to reach quorum and was rerun)? Both meeting records are kept; the most recently saved AGM's election results become the term's current committee positions.
- What happens to a custom office-holder title that is archived after it was used in a past AGM? The historical AGM and committee records keep showing the archived title's name; it simply stops appearing as a choice for future AGMs.
- What happens to an AGM record or its attendance/election data if a member referenced by it is later archived? The historical AGM record is unaffected — attendance and election history are preserved as they were at the time.
- What happens if the coordinator leaves the setup wizard's committee configuration at its defaults? Setup completes normally with no office-holder titles and no seat-count target set; both can be configured at any time afterward from Settings with identical behaviour.
- What happens to committee position records created before AGM-to-AGM term tracking existed? They are left as historical calendar-year records and are not retroactively re-dated; only terms produced by AGMs and special elections recorded after this feature ships use AGM-to-AGM boundaries and term labeling.
- What happens if the AGM month setting is changed after AGMs have already been recorded under the previous setting? Already-recorded committee terms keep the boundaries they were given at the time; only terms started by AGMs recorded after the change use the new AGM month.
- What happens if a special election is recorded for a position in a term that has already ended (the next AGM has already been recorded)? The system does not allow it — a special election can only replace a holder within the currently active (not yet superseded) committee term.
- What happens if a special election tries to appoint someone who already holds a different position in the same term? It is prevented, the same as the one-position-per-member rule enforced within an AGM (FR-008).

## Requirements

### Functional Requirements

- **FR-001**: The system MUST provide a dedicated "Record AGM" entry point from the Events menu, separate from the existing generic "create event" flow.
- **FR-002**: An Annual General Meeting MUST be recorded as its own record type with its own date, not as an entry in the general Events list.
- **FR-003**: The system MUST no longer offer "Annual General Meeting" as a selectable type when creating a new (generic) event, while preserving any existing events historically tagged with that type.
- **FR-004**: The AGM screen MUST list active members for attendance marking, with the ability to mark individual members and to select/deselect all members at once.
- **FR-005**: The member attendance list on the AGM screen MUST NOT use page-number controls; instead the member grid MUST scroll independently (its own vertical scroll bar) while the rest of the page stays fixed.
- **FR-006**: The AGM screen MUST let the coordinator assign one member each to the President, Secretary, and Treasurer positions for the meeting's resulting committee term, leaving a position vacant if no one is assigned.
- **FR-007**: The AGM screen MUST let the coordinator assign members to any additional office-holder positions defined for the organisation (User Story 2), and record a member with no specific title as a general committee member.
- **FR-008**: The system MUST prevent a single member from being assigned to more than one office-holder position or general-committee-member slot within the same AGM.
- **FR-009**: Saving an AGM MUST persist the meeting record, its attendance records, and its committee position results together as one atomic operation; if any part fails, none of it is saved.
- **FR-010**: Saving an AGM's election results MUST supersede any existing committee position records for the committee term it starts — members not reselected in this AGM are no longer recorded as holding a committee position for that term, unless already closed out earlier by a special election (User Story 4).
- **FR-011**: Once an AGM has been saved, its attendance and election results MUST be displayed read-only; the system MUST NOT provide an edit path for a saved AGM's attendance or election data (corrections happen by recording a superseding AGM, consistent with FR-010).
- **FR-012**: The system MUST let the coordinator define additional office-holder position titles beyond the built-in President, Secretary, and Treasurer, including adding, renaming, reordering, and archiving a title.
- **FR-013**: The built-in President, Secretary, and Treasurer positions MUST NOT be renamed, reordered, or archived.
- **FR-014**: The system MUST let the coordinator set a target number of general committee member seats, and the AGM screen MUST show how many of that target have been filled as selections are made.
- **FR-015**: The system MUST provide a browsable list of past AGMs, ordered most-recent-first, showing at minimum each meeting's date and attendance count.
- **FR-016**: The system MUST let the coordinator open a past AGM to view its attendance and elected positions read-only.
- **FR-017**: The system MUST allow a past AGM record to be archived (soft-deleted) rather than permanently removed, consistent with the system's soft-delete rule for non-financial records.
- **FR-018**: The existing annual committee-reset reminder, which currently triggers off an AGM recorded as a generic event, MUST be updated to trigger off AGMs recorded through this new workflow instead.
- **FR-019**: The system MUST include automated tests covering every reachable code path introduced by this feature (success, validation failure, exception, and boundary/null cases), per the project's exhaustive test-coverage rule.
- **FR-020**: The first-run setup wizard MUST include a step for configuring committee office-holder titles and the general committee seat count target, using the same underlying configuration as the Settings committee configuration area (User Story 2).
- **FR-021**: Completing committee configuration during setup MUST be optional — the coordinator MUST be able to finish the setup wizard with default (empty/unset) values and configure office-holder titles and the seat count later from Settings, with no difference in behaviour.
- **FR-022**: The first-run setup wizard MUST let the coordinator select the calendar month in which the organisation's AGM is normally held.
- **FR-023**: A committee term MUST be defined as running from the date of the AGM at which it was elected to the date of the next recorded AGM, not from 1 January to 31 December of a fixed calendar year.
- **FR-024**: A committee term MUST be labeled with the calendar year containing the majority of the days within that term (e.g., a term beginning at an October AGM is labeled with the following calendar year).
- **FR-025**: Saving a new AGM's election results MUST supersede existing committee position records for the term it starts, not for a fixed 1 January – 31 December calendar year (see FR-010).
- **FR-026**: The system MUST let the coordinator record a special/interim election to replace a committee member or office holder who leaves before the next AGM, without creating a new full AGM record.
- **FR-027**: Recording a special election MUST close the departing holder's committee position record with an end date and create a new committee position record for the incoming member starting on the recorded replacement date, both attributed to the same committee term.
- **FR-028**: The system MUST allow more than one committee position record to exist for the same position within the same committee term, to represent a holder followed by their replacement.
- **FR-029**: When a committee term's position has more than one holder recorded, the system MUST display and print each holder's name together with their service start and end dates for that position; when a position has a single unbroken holder for the term, dates need not be shown.
- **FR-030**: The AGM month setting MUST be the single source of truth for both the existing committee-reset reminder timing and the new committee term boundaries — the coordinator configures the AGM month once, not as two separate settings.

## Key Entities

- **Annual General Meeting**: Represents one AGM sitting — meeting date, optional notes, and the standard archive/audit fields. Owns its attendance and, indirectly (via the committee position records for its resulting committee term), its election results.
- **AGM Attendance Record**: Records whether one member attended one specific AGM. One record per (AGM, member) pair.
- **Committee Office-Holder Type**: A defined position title an organisation can elect members into at an AGM (e.g., "Vice President"), beyond the three built-in positions. Has a display order and can be archived.
- **Committee Position Record** *(existing — reused, extended)*: The elected outcome for one member, in one named position (or blank for a general committee member), for one committee term (an AGM-to-AGM cycle, not necessarily a calendar year — see User Story 3). Carries a start date and an optional end date, so a term with a mid-term replacement (User Story 4) can hold more than one record for the same position, each dated to when that person actually served.
- **Committee Term**: The AGM-to-AGM cycle a set of committee position records belongs to — its label year, its start date (the AGM that began it), and its end date (the next AGM, or open-ended while still current).

## Success Criteria

### Measurable Outcomes

- **SC-001**: A coordinator can record a complete AGM — attendance for all active members plus every office-holder and general-committee election — in a single workflow, with zero visits to individual member edit screens.
- **SC-002**: 100% of AGMs recorded through the new workflow appear in committee reporting (existing Committee Report) with the correct year, positions, and members, matching what was entered on the AGM screen.
- **SC-003**: Coordinators can define at least one custom office-holder title and have it available for selection on the very next AGM they record, with no restart or reconfiguration step required.
- **SC-004**: Reviewing any past AGM's attendance and election results takes no more than two navigation steps from the Events menu.
- **SC-005**: The member attendance grid on the AGM screen remains usable (no pagination, independently scrollable) regardless of how many active members exist.
- **SC-006**: Every committee term recorded after this feature ships is dated from its AGM to the next AGM and labeled with the calendar year containing the majority of its days, with zero terms left dated to a fixed 1 January – 31 December calendar year.
- **SC-007**: When a mid-term replacement is recorded, both the outgoing and incoming holder's service dates are visible on screen and in print for 100% of affected positions.

## Assumptions

- "Define the number of general committee members" is interpreted as a configurable target seat count shown for guidance on the AGM screen (e.g., "4 of 5 general committee seats filled"), not a hard limit that blocks saving if unmet or exceeded — the coordinator may still record more or fewer members than the target.
- Election results are stored using the existing Committee Position Record structure (member, committee term, position — blank position meaning general committee member), extended with start/end dates as described under Key Entities; no other changes to that record's shape are required, only new ways of producing it from the AGM screen and from special elections.
- Committee Position Records are unique per (member, committee term, position slot) rather than per (member, calendar year); a member still cannot hold two positions concurrently within the same term, so the "cannot be assigned twice in one AGM" rule (FR-008) carries that constraint into the new screen and into special elections (User Story 4) alike.
- A committee term is labeled by the calendar year containing the majority of its days — equivalently, the year following the AGM when the AGM falls between July and December, or the AGM's own year when it falls between January and June — matching the issue's October-AGM example (elected 2025, recorded as the 2026 committee).
- Existing committee position records created before AGM-to-AGM term tracking existed are left as historical calendar-year data and are not retroactively re-dated; only terms produced by AGMs and special elections recorded after this feature ships use AGM-to-AGM boundaries.
- The AGM month setting introduced here reuses/replaces the existing committee-renewal-month configuration rather than adding a second, separate setting for the same real-world fact.
- Corrections to a saved AGM are handled by recording a new, superseding AGM (FR-010, FR-011) rather than by editing history in place, consistent with the project's pattern of immutable attendance/participation records elsewhere (Rehearsals, Events).
- The feature is scoped to attendance, office-holder elections, and general-committee-member elections only. The issue notes "additional items may need to be recorded in the future" — this spec does not add speculative fields for unspecified future outcomes, but nothing here precludes extending the Annual General Meeting record later.
- "Members" throughout this spec means active members, consistent with how attendance and participation are recorded elsewhere in the system (Rehearsals, Events).
