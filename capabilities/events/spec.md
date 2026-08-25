# Events — Living Spec

> [DRAFT] Surface-first draft from existing code — every requirement is observed from the code surface unless tagged otherwise. Review before trusting.

## Purpose

Events lets the group schedule performances, eisteddfods, fundraisers, promotional appearances, and AGMs, and record which members took part. Without it there is no historical record of what the group did or how many members showed up, and the dashboard/committee-reset logic that depends on event and participation history has nothing to read.

## Requirements

### Events never generate financial records
Scheduling an event or recording participation MUST NOT create a `Fee`, `Payment`, or `Transaction`, and MUST NOT touch the GL. Events are a purely operational/historical record, distinct from Rehearsals or Finance workflows that do post financial entries.

#### Scenario: an event is scheduled and participation recorded
- **WHEN** a new event is scheduled and a participation batch is later saved for it
- **THEN** no fee, payment, transaction, or GL entry is created as a side effect of either action

### Scheduling an event requires a date and a known event type
The system SHALL reject an event submission that is missing a date or a valid event type; notes are optional free text.

#### Scenario: required fields omitted
- **WHEN** a user submits the schedule form without picking a date or an event type
- **THEN** the form is blocked client-side by required-field validation and no event is created

### Participation can only be recorded for an event that has already happened
The system MUST reject any attempt to record participation for an event whose date is still in the future, since attendance can't be known ahead of time.

#### Scenario: participation attempted early
- **WHEN** a participation batch is submitted for an event dated after today
- **THEN** the operation is rejected with a validation error and no participation records are written

### Participation recording is idempotent per member and freezes the event's rate exactly once
Submitting the same member twice for the same event MUST NOT create duplicate participation records, and the event's participation rate MUST be computed and stored only the first time participation is successfully recorded for that event — never recalculated afterward.

#### Scenario: a member already has a record for this event
- **WHEN** a participation batch includes a member who already has a participation record for the event
- **THEN** that member's entry is silently skipped rather than duplicated or overwritten

#### Scenario: first successful participation save
- **WHEN** participation is recorded for a past event that has no stored rate yet
- **THEN** the new records are saved and the event's participation rate is computed from them and stored permanently

The UI treats a recorded event as immutable and hides the entry screen once a rate exists ("Records are immutable after saving"), but this guarantee is enforced only at the presentation layer. [NEEDS CLARIFICATION: should the record-participation operation itself also reject new submissions once an event's rate is already frozen, rather than relying solely on the UI to gate re-entry?]

### The participation rate measures turnout against who was active at the time of the event
The stored rate MUST be computed as (members marked participated) ÷ (members who were active as of the event's date) × 100, rounded to two decimal places, and MUST be zero rather than undefined when there were no active members on that date.

#### Scenario: rate computed for a past event
- **WHEN** participation is recorded and the roster of members active as of the event date is non-empty
- **THEN** the stored rate reflects participated-count over that historical active-member count, not the count of members currently active today

### Event types classify events, and system-seeded defaults are protected from change
The system SHALL prevent an `IsSystemDefault` event type from being archived, and SHALL block archiving any event type — default or user-created — while a non-deleted event still references it.

#### Scenario: archiving a default type
- **WHEN** a user attempts to archive a system-default event type (e.g. "Annual General Meeting")
- **THEN** the archive is rejected with a validation error

#### Scenario: archiving a type still in use
- **WHEN** a user attempts to archive a user-created event type that at least one non-deleted event still references
- **THEN** the archive is rejected with a validation error explaining why

### Event and participation data are archivable at the data layer, but no reviewed workflow exposes deleting them [inferred]
`Event`, `EventType`, and `ParticipationRecord` all carry the standard soft-delete fields (`IsDeleted`/`DeletedAt`/`DeletedBy`), consistent with the platform-wide soft-delete convention. Within this capability's service and UI surface, only `EventType` has an exposed archive/restore path (`EventTypeService`); nothing reachable schedules an `Event`'s or a `ParticipationRecord`'s deletion — once scheduled, an event and its participation history are effectively permanent from this capability's own surface.

#### Scenario: an event is scheduled and participation saved
- **WHEN** an event is created and its participation is recorded
- **THEN** neither the event nor its participation records can be archived through any code path in `StageFright.Core/Modules/Events` or `StageFright.UI/Pages/Events`

### The events list is searchable and grid-standard
The events list SHALL let users filter visible rows by a single free-text search across date, event type, and notes, and SHALL present results in the shell's standard sortable/paged data grid rather than a hand-rolled table.

#### Scenario: user searches the list
- **WHEN** a user types into the search box
- **THEN** only events whose formatted date, event type name, or notes contain the search text (case-insensitive) remain visible

### Attendance detail is only shown once participation exists, and prompts action otherwise
The event detail page SHALL only render an attendance table when participation records exist for that event; when no rate has been stored yet it SHALL instead direct the user to record participation, and when a rate exists but no records were saved it SHALL say so rather than showing an empty grid.

#### Scenario: viewing a future or not-yet-recorded event
- **WHEN** a user opens the detail page for an event with no stored participation rate
- **THEN** the page shows a prompt and a link to record participation instead of an attendance grid

### Recording participation is a single bulk action over the full member roster with per-row and select-all toggling
The participation entry screen SHALL pre-populate one row per member (not participated by default), let the user toggle rows individually or all at once, and submit the whole roster as one batch.

#### Scenario: user marks most members present via select-all then excludes one
- **WHEN** the user checks "select all" and then unchecks a single member's row
- **THEN** the select-all checkbox itself reflects the mixed state and only that one member is submitted as not-participated

### The dashboard tile summarizes upcoming events and the most recently recorded turnout
The Events dashboard tile SHALL show a count of upcoming events, the next event's date, and — once at least one past event has recorded participation — that event's rate, visually distinguished once it reaches a "good" threshold.

#### Scenario: a past event has a strong turnout
- **WHEN** the most recently recorded past event's stored rate is 80% or higher
- **THEN** the tile highlights the rate and its progress bar in a success/positive style rather than the neutral default

## Uncovered

_None — every file in the area was read._

### Users can print a read-only attendance sheet for any event
The events list and event detail page SHALL each offer a "Print" action that generates a two-column, checkbox-style PDF listing every member active as of the event's date, with a "Participated" checkbox that prints blank until participation has been recorded for that event and matches the real recorded value afterward. Printing MUST NOT create, modify, or delete any Event, ParticipationRecord, or Member record.

#### Scenario: printing before participation is recorded
- **WHEN** a user clicks Print for an event whose participation has not yet been recorded
- **THEN** every member active as of the event's date appears on the generated PDF with a blank "Participated" checkbox

#### Scenario: printing after participation is recorded
- **WHEN** a user clicks Print for an event whose participation has already been recorded
- **THEN** each listed member's checkbox on the generated PDF matches their actual recorded participation

#### Scenario: no active members to list
- **WHEN** a user clicks Print for an event with nobody active as of its date
- **THEN** the system shows an inline empty-state message instead of generating a blank PDF
