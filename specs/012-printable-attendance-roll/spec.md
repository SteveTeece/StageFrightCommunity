# Feature Specification: Printable Member Attendance Roll

**Feature Branch**: `012-printable-attendance-roll`

**Created**: 2026-07-27

**Status**: Draft

**Input**: User description: "Add a printed members roll (attendance sheet) that can be generated as part of the create-rehearsal workflow, listing active members sorted by surname with Attended, Rehearsal Fee Paid, and Annual Fee Paid checkboxes. Sourced from GitHub issue #257: the person taking attendance currently records it on paper and maintains a separate manually-updated roll document; they want to print a roll of the currently active members for a rehearsal, with unchecked 'Attended' and 'Rehearsal Fee Paid' checkboxes, and an 'Annual Fee Paid' checkbox that reflects whether the member's current year's annual fee is already paid. Surnames shown in all capitals, members sorted by surname, laid out in two columns per page with the second column as overflow, minimal-width checkbox columns, and wrapping column headings."

## Clarifications

### Session 2026-07-27

- Q: Should generating the printable roll require the rehearsal to already be scheduled (saved) in the system, or should it also be available directly from the "create rehearsal" form using the date being entered, before the rehearsal is saved? → A: Roll generation requires an already-scheduled (saved) rehearsal; the print action is surfaced afterward (e.g., from the rehearsal's list row or detail/attendance page), not from the in-progress create form.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Print a roll instead of maintaining a separate paper list (Priority: P1)

As a person who takes attendance at rehearsals, I want to generate and print a roll of the currently active members for a specific rehearsal, so that I have an accurate, up-to-date paper list to record attendance and payments on during the rehearsal, without maintaining a separate manually updated document.

**Why this priority**: This is the core problem stated in the request — replacing a manually maintained paper roll with one generated on demand from the system's live member data. Without this, the fee-status and layout refinements in the other stories have nothing to appear on.

**Independent Test**: Can be fully tested by generating a roll for a scheduled rehearsal and confirming it lists every currently active member, sorted by surname, each with blank "Attended" and "Rehearsal Fee Paid" checkboxes, ready to print.

**Acceptance Scenarios**:

1. **Given** a rehearsal has been scheduled and at least one active member exists, **When** the user generates the roll for that rehearsal, **Then** every active member appears exactly once, sorted alphabetically by surname, each with blank "Attended" and "Rehearsal Fee Paid" checkboxes.
2. **Given** a member's status is archived (or the member has been soft-deleted), **When** the roll is generated, **Then** that member does not appear on the roll.
3. **Given** no active members exist, **When** the user attempts to generate the roll, **Then** the system shows an empty-state message instead of producing a blank roll.

---

### User Story 2 - See current-year annual fee status at a glance (Priority: P2)

As a person taking attendance, I want the roll to already show whether each member has paid their current year's annual fee, so I don't need to look each member up separately or ask them at the rehearsal.

**Why this priority**: Adds real information value on top of the base roll from User Story 1, saving the attendance-taker a manual lookup, but the roll is still usable without it.

**Independent Test**: Can be fully tested by generating the roll for a mix of members — some with a fully paid current-year annual fee, some with an unpaid or partially paid one, and some with no annual fee recorded yet for the current year — and confirming the "Annual Fee Paid" checkbox is checked only for members whose current-year annual fee is fully paid.

**Acceptance Scenarios**:

1. **Given** a member has a current-year annual fee with no outstanding balance, **When** the roll is generated, **Then** that member's "Annual Fee Paid" checkbox is shown checked.
2. **Given** a member has a current-year annual fee with an outstanding balance (unpaid or partially paid), **When** the roll is generated, **Then** that member's "Annual Fee Paid" checkbox is shown unchecked.
3. **Given** a member has no annual fee recorded for the current year at all, **When** the roll is generated, **Then** that member's "Annual Fee Paid" checkbox is shown unchecked.

---

### User Story 3 - Compact, print-friendly layout (Priority: P3)

As a person taking attendance, I want the roll printed in a compact two-column layout with narrow checkbox columns, wrapped column headings, and surnames in capitals, so the sheet is easy to scan and uses as few printed pages as possible.

**Why this priority**: A formatting and usability refinement on top of Stories 1 and 2 — the roll is functional without it, but this is explicitly requested to make the printed sheet practical to carry and use during a rehearsal.

**Independent Test**: Can be fully tested by generating a roll with enough active members to overflow a single column and confirming the list continues into a second column on the same page, that checkbox columns are visibly narrower than the name column, that column headings wrap rather than widen the column, and that every surname is displayed in capital letters.

**Acceptance Scenarios**:

1. **Given** enough active members to fill more than one column, **When** the roll is generated, **Then** the alphabetically sorted list fills the first column completely before continuing into the second column on the same page.
2. **Given** a printed roll, **When** it is reviewed, **Then** the "Attended," "Rehearsal Fee Paid," and "Annual Fee Paid" checkbox columns are visibly narrower than the member name column(s).
3. **Given** a column heading whose text is wider than its column, **When** the roll is generated, **Then** the heading text wraps onto multiple lines rather than widening the column or being cut off.
4. **Given** any member on the roll, **When** their name is displayed, **Then** their surname is shown in all capital letters.

---

### Edge Cases

- What happens when there are more active members than fit on a single page even with two columns? The roll continues onto additional pages, repeating the two-column layout and column headings on each page.
- How does the system handle a member who is active but has no current-year annual fee record yet (fees not yet applied for the year)? They are shown with an unchecked "Annual Fee Paid" checkbox, the same as an unpaid fee.
- How does the system handle a member with an overpaid or credit annual-fee balance? Their "Annual Fee Paid" checkbox is shown checked, since there is no outstanding balance owed.
- How does the system handle two members who share the same surname? They are sub-sorted by first name so the alphabetical order remains stable and unambiguous.
- What happens if the roll is generated again later (e.g., after new members joined or others became inactive) for the same rehearsal? It reflects the active member list at the time of that later generation, not the list from an earlier printing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow a user to generate a printable member roll for any already-scheduled (saved) rehearsal; the action is surfaced from an existing rehearsal (e.g., its list row or detail/attendance page), not from the in-progress "create rehearsal" form prior to saving.
- **FR-002**: The roll MUST list every member whose status is active at the time the roll is generated; archived and soft-deleted members MUST be excluded, using the same active-member definition already used when recording attendance, so the printed roll and the later digital attendance entry always show the same set of members.
- **FR-003**: The roll MUST display each member's surname in all capital letters, alongside their first name.
- **FR-004**: Members on the roll MUST be sorted alphabetically by surname, then by first name for members who share a surname.
- **FR-005**: Each member row MUST include an "Attended" checkbox column, printed unchecked, for the attendance-taker to mark by hand.
- **FR-006**: Each member row MUST include a "Rehearsal Fee Paid" checkbox column, printed unchecked, for the attendance-taker to mark by hand.
- **FR-007**: Each member row MUST include an "Annual Fee Paid" checkbox column, shown checked if the member's current-calendar-year annual fee is fully paid (no outstanding balance) at the time the roll is generated, and shown unchecked otherwise — including when no current-year annual fee record exists yet for that member.
- **FR-008**: The roll MUST display the rehearsal's date (and time, if recorded) so the printed sheet can be identified and matched to the correct rehearsal.
- **FR-009**: When the number of members exceeds what fits in a single column on a page, the roll MUST continue the same alphabetically sorted list into a second column on that page as overflow, rather than starting a new or separate list, and MUST continue onto additional pages using the same two-column layout if the list is still longer than one page.
- **FR-010**: The "Attended," "Rehearsal Fee Paid," and "Annual Fee Paid" checkbox columns MUST be rendered at minimal width relative to the name column(s).
- **FR-011**: Column headings MUST wrap onto multiple lines to fit their column width rather than widening the column or truncating the heading text.
- **FR-012**: The roll MUST be produced in a form suitable for printing, consistent with how other reports in the system are generated and printed.
- **FR-013**: If there are no active members at the time of generation, the system MUST show an empty-state message instead of producing a blank printable roll.

### Key Entities *(include if feature involves data)*

- **Member**: Existing entity. Surname, first name, and active/archived status are used to populate the roll. No new fields are introduced.
- **Rehearsal**: Existing entity. Supplies the date (and time) shown on the printed roll to identify which rehearsal it belongs to. Unchanged by this feature.
- **Fee (Annual type)**: Existing financial record, together with the member's ledger balance, used to determine whether the "Annual Fee Paid" checkbox is checked for the current calendar year. This feature only reads existing fee and balance data — generating the roll does not create, modify, or delete any fee, payment, or ledger record.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- [X] **SC-001**: A user can generate a printable roll for a scheduled rehearsal in a few clicks, without needing to create or maintain any separate manual roll document. — Verified live: one click of "Print Roll" on the Rehearsals list opens a print-ready PDF.
- [X] **SC-002**: 100% of currently active members appear on a generated roll, and no archived or soft-deleted members appear. — Verified via unit/integration tests and a live run against the real dev database (44 active members listed, matching the Dashboard's active count exactly).
- [X] **SC-003**: The "Annual Fee Paid" checkbox matches each listed member's actual current-year annual-fee payment status in the system with 100% accuracy at the time of generation. — Verified via unit/integration tests (fully-paid/unpaid/no-record/overpaid cases) and a live run showing correct checked/unchecked state against real GL data.
- [X] **SC-004**: Members appear in correct alphabetical surname order, with surnames shown in all capitals, on 100% of generated rolls. — Verified via unit tests (surname + first-name sub-sort) and the live-generated PDF.
- [X] **SC-005**: For a typical rehearsal roster, the two-column layout keeps the printed roll to a single page, compared to needing two pages for the same member count in a single-column layout. — Verified live: 44 active members rendered on a single page across two columns (32 + 12), which a single-column layout at the same per-page row capacity would have split across two pages.

## Assumptions

- The roll lists members who are active at generation time — the same live "who is active" definition already used by the digital attendance-entry screen — rather than a historical snapshot of who was active as of the rehearsal's date. This keeps the paper roll and the later digital entry in sync even if membership changes between printing and data entry.
- "Current year's annual fee paid" means the member has an annual-type fee dated in the current calendar year with no outstanding balance owed. A member with no annual fee record yet for the current year is treated as unpaid (checkbox unchecked).
- The roll can be generated, and re-generated, for a rehearsal at any point after it has been scheduled, rather than being restricted to a single step of the creation workflow; the primary use case is generating it before attendance is taken, to bring to the rehearsal on paper.
- The roll is produced only as a printable document; no separate spreadsheet-style export is needed, since its value is the physical checkbox layout intended for handwriting.
- Generating the roll is a read-only operation — it does not create, update, or delete any Member, Rehearsal, Fee, Payment, or Transaction record. Marks made by hand on the printed sheet are not captured by the system until separately entered through the existing attendance-recording screen.
- This feature is the intended work for issue #257.
