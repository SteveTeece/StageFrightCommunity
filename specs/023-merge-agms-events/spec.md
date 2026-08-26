# Feature Specification: AGMs on the All Events List

**Feature Branch**: `023-merge-agms-events`
**Created**: 2026-08-25
**Status**: Draft
**Source**: [GitHub issue #320](https://github.com/SteveTeece/StageFrightCommunity/issues/320) — "Add AGMs to list of events"

## User Scenarios & Testing

### User Story 1 - See AGMs in the All Events list (Priority: P1)

A committee member opens the All Events screen wanting a single, chronological record of everything the group has held — performances, fundraisers, promotional appearances, and Annual General Meetings. Today an AGM only shows up on the separate AGMs screen, so answering "what did we do, and when" means checking two places. This story makes AGMs appear as rows in the same All Events list.

**Why this priority**: This is the entire ask in issue #320 — without it, the feature doesn't exist. It delivers value on its own: a user can immediately see the combined history, even before the refinements in the stories below.

**Independent Test**: Seed one recorded AGM and one Event with different dates, open the All Events screen, and confirm the AGM appears as a row in the same list as the Event, ordered by date. Verifiable without Stories 2 or 3.

**Acceptance Scenarios**:

1. **Given** at least one recorded AGM and one Event exist, **When** a user opens the All Events screen, **Then** both appear as rows in the same list, ordered by date with the most recent first.
2. **Given** an AGM has been scheduled but not yet recorded, **When** a user opens the All Events screen, **Then** that AGM still appears in the list alongside past events.
3. **Given** no Events and no AGMs exist, **When** a user opens the All Events screen, **Then** the existing "No events scheduled yet" empty state is shown.

---

### User Story 2 - Tell AGM rows apart and open the right screen (Priority: P2)

Mixing AGMs into the events list is only useful if a user can tell an AGM row apart from an ordinary event row at a glance, and if selecting it opens the AGM's own detail/recording screen rather than the generic event detail screen an AGM was never designed for.

**Why this priority**: Builds directly on Story 1. Without it, users can see AGMs in the list but can't act on them correctly — this is the smallest addition that makes the merged list genuinely usable rather than merely informative.

**Independent Test**: With an AGM present in the combined list (from Story 1), select its row and confirm it opens the AGM's dedicated screen; confirm the row's type and status text read distinctly from an ordinary event's.

**Acceptance Scenarios**:

1. **Given** an AGM row is shown in the combined list, **When** a user selects that row, **Then** they are taken to that AGM's own detail screen, not the generic event detail screen.
2. **Given** a recorded AGM is shown, **When** a user views its row, **Then** the row shows it as recorded rather than the participation-rate indicator used for ordinary events.
3. **Given** a scheduled but unrecorded AGM is shown, **When** a user views its row, **Then** the row offers the same "Record" action available on the dedicated AGMs screen.

---

### User Story 3 - Search the combined list (Priority: P3)

The All Events screen already lets a user filter the list by typing a date, type, or note text. Once AGMs are part of that list, the same search box should find them too, so a user looking for "the 2025 AGM" isn't left wondering why it isn't there.

**Why this priority**: A refinement on top of Stories 1–2 — the list is already useful without it, but search silently excluding AGMs would be a confusing gap once they're visibly part of the same list.

**Independent Test**: With an AGM and a non-AGM event both present, type a search term that matches only the AGM's date or notes and confirm only the AGM row remains; type a term matching only the other event and confirm the AGM row is filtered out.

**Acceptance Scenarios**:

1. **Given** the combined list is shown, **When** a user types text matching an AGM's date, **Then** only rows whose date matches remain, including the AGM.
2. **Given** the combined list is shown, **When** a user types "annual general meeting" or a partial match, **Then** AGM rows remain visible and non-matching event rows are filtered out.
3. **Given** the combined list is shown, **When** a user types text matching only an AGM's notes, **Then** that AGM row remains and unrelated rows are filtered out.

---

### Edge Cases

- An Event and an AGM fall on the exact same date — both rows must still appear; no assumption of date uniqueness.
- An AGM is archived — it must disappear from the combined list, exactly as a soft-deleted Event already does.
- A long combined history (many years of events plus AGMs) — the list still pages through the shell's standard grid rather than becoming unusable.
- There are zero Events but at least one AGM, or vice versa — the combined list is not treated as empty just because one source has nothing.
- A search term matches neither an AGM nor an Event — the existing "no events match" message applies to the combined set, not just the Event-only set.

## Requirements

### Functional Requirements

- **FR-001**: The All Events screen MUST display every non-archived Annual General Meeting alongside every non-archived Event in one combined list.
- **FR-002**: The combined list MUST be sorted by date, most recent first, matching the All Events screen's current default sort order.
- **FR-003**: Each AGM row MUST display its date and notes using the same columns the All Events screen already presents for other events.
- **FR-004**: Each AGM row MUST display "Annual General Meeting" as its type, in the same column that shows an ordinary event's type name.
- **FR-005**: Each AGM row MUST show whether that AGM has been recorded or is still scheduled, in place of the participation-rate indicator shown on ordinary event rows.
- **FR-006**: Selecting an AGM row MUST navigate the user to that AGM's own detail screen; it MUST NOT navigate to the generic event detail screen used for non-AGM events.
- **FR-007**: The actions offered on an AGM row MUST match what the dedicated AGMs screen already offers for that AGM (a "Record" action while unrecorded, and a "Print" action for its attendance report), rather than the "Record Participation" action shown on ordinary event rows.
- **FR-008**: The All Events screen's search box MUST match AGM rows using the same fields it already searches for other events: formatted date, type name, and notes.
- **FR-009**: The All Events screen's empty-state and no-results messages MUST reflect the combined set: the "No events scheduled yet" message MUST appear only when there are no Events and no AGMs at all, and the "no matches" message MUST appear whenever a search term matches neither an Event nor an AGM.
- **FR-010**: A soft-deleted (archived) AGM MUST NOT appear in the combined All Events list, consistent with how a soft-deleted Event is already excluded.
- **FR-011**: This change MUST NOT alter how Events or AGMs are scheduled, recorded, archived, or how AGM attendance and committee elections are processed — only the All Events screen's display and search behavior changes.
- **FR-012**: The dedicated "AGMs" list screen, "Schedule AGM" screen, and AGM record/detail screens MUST remain reachable and behave exactly as they do today; the combined All Events view is an additional read surface, not a replacement.
- **FR-013**: The "Schedule Event" action on the All Events screen MUST continue to schedule only a generic Event; scheduling an AGM remains reachable only through the existing "Schedule AGM" navigation entry.

### Key Entities

- **Combined Events List Entry**: A read-only row shown on the All Events screen, not persisted in its own right — built by pairing either an Event or an AnnualGeneralMeeting with enough information to display and act on it. Carries the source record's date, notes, and identity; a display type (the AGM's fixed "Annual General Meeting" label, or the event's own type name); a status indicator (an event's participation rate, or an AGM's recorded/scheduled state); and a route to the correct source screen with the correct action for that row's kind.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A user viewing the All Events screen sees every non-archived AGM listed alongside every non-archived Event, with no need to visit a separate screen to confirm an AGM happened.
- **SC-002**: Selecting any AGM row opens that AGM's own screen in a single action, with zero AGM rows routing to the generic event detail screen.
- **SC-003**: A search term matching an AGM's date, type, or notes returns that AGM in the filtered list every time, with existing Event search results unaffected.
- **SC-004**: Every existing AGM scheduling, recording, and archiving workflow continues to complete successfully after this change, with zero regressions.

## Assumptions

- "All Events screen" refers to the existing `/events` ("All Events") page, distinct from the dedicated `/events/agm` AGMs page.
- The underlying `Event` and `AnnualGeneralMeeting` data models stay separate, per spec 013's deliberate decision to split AGM recording out of the generic Event entity — this feature only changes the read/list-display layer, not storage.
- When an Event and an AGM share the same date, the combined list sorts Event rows before AGM rows as an explicit, deterministic secondary sort key (by `Kind`) — not a human-meaningful ordering requirement, just determinism (issue #325); the original issue only asked that AGMs appear in the list, not that same-day ordering carry any particular meaning.
- The Dashboard Events tile, Committee report, and other screens that read Event or AGM data outside the All Events list are unaffected by this change and out of scope.
- The "Schedule Event" button on the All Events screen continues to create only generic Events; AGM scheduling stays on its existing dedicated entry point.
