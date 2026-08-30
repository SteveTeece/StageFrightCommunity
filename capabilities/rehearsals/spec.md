# Rehearsals — Living Spec

> [DRAFT] Surface-first draft from existing code — every requirement is observed from the code surface unless tagged otherwise. Review before trusting.

## Purpose
The Rehearsals capability schedules rehearsal sessions and records who attended them, tying each attendance into the double-entry ledger as a fee and (usually) an immediate payment. Without it there is no way to track attendance history, no attendance-fee revenue, and no data to drive the attendance-rate metrics shown elsewhere in the app.

## Requirements

### Rehearsals are scheduled with a date, time, and optional notes
Scheduling a rehearsal MUST persist the date, time, and optional notes as one record and write an audit trail "Create" entry, both inside a single transaction. The date is not restricted to the future — a rehearsal may be scheduled for a past date (e.g. to backfill a session).

#### Scenario: scheduling a new rehearsal
- **WHEN** a user submits a date, time, and optional notes on the Schedule Rehearsal form
- **THEN** a new rehearsal is saved and an audit entry recording its creation is written in the same transaction

#### Scenario: time is left blank or unparsable
- **WHEN** the time field is empty or cannot be parsed as a time value
- **THEN** the form blocks submission and shows a "Time is required" message without calling the scheduling service

### Attendance can be recorded only once, and never before the rehearsal date
Recording attendance for a rehearsal MUST be rejected while the rehearsal's date is still in the future, and MUST become permanently unavailable once that rehearsal's attendance rate has been frozen — both the service and the UI enforce this independently.

#### Scenario: attempting to record attendance early
- **WHEN** a user opens the attendance page for a rehearsal dated after today
- **THEN** the UI shows an informational message instead of the entry grid, and a direct service call for that rehearsal is rejected with a validation error

#### Scenario: attendance already recorded
- **WHEN** a rehearsal already has a frozen attendance rate
- **THEN** the UI shows the saved attendance as a read-only list instead of an editable grid, and the records shown are exactly what was saved — there is no edit or re-submit path

### Recording a batch of attendance creates a fee and balanced GL entries for each present, active member, atomically
For every member marked attended who is currently Active, the system MUST create an attendance Fee plus a balanced GL accrual pair (debit Member Receivable, credit Income, plus a Tax Collected credit when the fee is taxable and sales tax applies), and — unless the entry is explicitly marked unpaid — MUST also create a Payment and a balanced GL payment pair (debit Cash, credit Member Receivable) clearing it immediately. All AttendanceRecord rows, fees, and GL entries for the whole batch are written in one all-or-nothing transaction. A member already recorded for that rehearsal is skipped entirely (idempotent per rehearsal/member pair); a member marked attended but not Active gets an AttendanceRecord but no fee or GL entries.

#### Scenario: mixed paid and unpaid attendees
- **WHEN** a batch marks one active member attended-and-paid and another attended-but-unpaid
- **THEN** the paid member ends up with a fee, GL accrual, Payment, and GL payment pair all in the same transaction; the unpaid member ends up with only the fee and GL accrual, leaving an outstanding receivable

#### Scenario: resubmitting a batch that partially succeeded before
- **WHEN** a batch is submitted for a rehearsal where some members already have an AttendanceRecord for it
- **THEN** those members are skipped without error or duplicate fees, while any new members in the batch are still processed

#### Scenario: no income account configured
- **WHEN** no non-system Income-type account exists in the chart of accounts
- **THEN** the entire recording operation is rejected before any records are written, so partial fee/GL data is never left behind

### The attendance-entry grid defaults a marked-attended member to paid-in-cash; unpaid is an explicit opt-out
Checking a member as attended in the entry grid MUST default their fee to paid; unchecking attended MUST clear paid; the paid checkbox remains independently uncheckable while attended stays checked, and is disabled whenever the member is not marked attended. This UI state maps directly onto the service's paid/unpaid branch at submission time.

#### Scenario: marking someone attended and unpaid
- **WHEN** staff check a member as attended and then uncheck their Paid box before saving
- **THEN** the submitted batch item has an unpaid marker for that member, so the service creates their fee accrual without an accompanying payment

#### Scenario: select-all
- **WHEN** the "Select All" header checkbox is toggled on
- **THEN** every row's attended state is set to match, and each row's paid state follows the same default-to-paid rule as an individual toggle

### The attendance rate is computed once from active membership and never recalculated
Freezing a rehearsal's rate MUST divide present-count by the count of members active as of the rehearsal's date (archived members always excluded from the denominator), round to two decimals, and store it. If the rate is already stored, freezing MUST be a no-op, even if called again with different inputs.

#### Scenario: freezing after a successful recording
- **WHEN** attendance recording completes for a rehearsal with no rate yet stored
- **THEN** the rate is computed and saved, and the rehearsal is thereafter treated as "recorded"

#### Scenario: freeze invoked again for an already-frozen rehearsal
- **WHEN** freeze is called for a rehearsal that already has a stored rate
- **THEN** the stored rate is left unchanged regardless of the present-count or date passed in

### AttendanceRecord rows are permanently immutable once saved
An AttendanceRecord carries soft-delete fields for structural consistency with the rest of the domain, but no code path in this capability ever sets `IsDeleted` on one: the attendance repository exposes only existence-check, read, and batch-insert operations — no update, archive, restore, or delete. This is an intentional deviation from the rest of the app's soft-delete convention; corrections to a mistaken attendance entry must go through a manual GL reversal rather than editing or removing the record. [inferred: "must go through a manual GL reversal" is stated in the entity's doc comment but no reversal workflow was itself in this capability's scope to verify]

#### Scenario: an attendance record needs correcting
- **WHEN** a mistake in a saved AttendanceRecord is discovered after the batch transaction has committed
- **THEN** there is no operation in this capability to edit, soft-delete, or restore that record — it remains exactly as saved

### The rehearsal list shows near-term future rehearsals plus this year's past ones, newest first, and is searchable
The list MUST combine at most the next 3 upcoming rehearsals with all past rehearsals dated within the current calendar year, then sort the combined set by date and time descending. Free-text search MUST filter the visible set by a case-insensitive match against the formatted date or the notes text.

#### Scenario: more than three rehearsals are upcoming
- **WHEN** more than 3 future rehearsals are scheduled
- **THEN** only the 3 soonest are included in the list

#### Scenario: searching narrows the list
- **WHEN** a search term is entered that matches part of a rehearsal's displayed date or its notes
- **THEN** only matching rehearsals remain visible, and a "no matches" message is shown if none do

### Printing an attendance roll is a read-only report that always reflects current data
Generating a roll MUST list every member active as of the rehearsal's date, sorted by surname then first name, with each row's Attended and RehearsalFeePaid flags computed fresh from existing attendance and fee/balance data — the operation MUST NOT create, update, or delete anything. "Fee paid" is determined by whether the member's attendance fee for that rehearsal has no outstanding balance, independent of whether they're marked attended.

#### Scenario: printing before attendance has been recorded
- **WHEN** a roll is printed for a rehearsal with no attendance recorded yet
- **THEN** every listed member shows Attended=false and RehearsalFeePaid=false, and the roll can still be used as a blank paper sign-in sheet

#### Scenario: no active members to list
- **WHEN** there are no members active as of the rehearsal's date
- **THEN** the UI shows a warning message and does not attempt to render or open a PDF

### Rehearsals contribute a navigation entry and two independently-loading dashboard tiles
The capability MUST register a top-level navigation entry (ordered after Members, before Events) and MUST provide a dashboard summary tile (upcoming count, next date, most recent recorded attendance rate highlighted at an 80%-or-above threshold) plus a separate 6-month attendance-trend line-chart tile. Each tile MUST load its own data independently and degrade to a plain message — never a raw error — when data is missing or the load fails.

#### Scenario: no attendance has ever been recorded
- **WHEN** the dashboard loads and no rehearsal has a stored attendance rate
- **THEN** the summary tile shows "No attendance recorded yet" instead of a rate, and the trend tile shows "No attendance recorded in the last 6 months" instead of a chart

#### Scenario: a tile's data load fails
- **WHEN** loading data for either tile throws an exception
- **THEN** that tile shows an "Unable to load" message rather than propagating the error to the rest of the dashboard

## Uncovered
_None — every file in the area was read._
