# Feature Specification: Printable Member Attendance Roll

**Feature Branch**: `012-printable-attendance-roll`

**Created**: 2026-07-27

**Status**: Draft

**Input**: User description: "Add a printed members roll (attendance sheet) that can be generated as part of the create-rehearsal workflow, listing active members sorted by surname with Attended, Rehearsal Fee Paid, and Annual Fee Paid checkboxes. Sourced from GitHub issue #257: the person taking attendance currently records it on paper and maintains a separate manually-updated roll document; they want to print a roll of the currently active members for a rehearsal, with unchecked 'Attended' and 'Rehearsal Fee Paid' checkboxes, and an 'Annual Fee Paid' checkbox that reflects whether the member's current year's annual fee is already paid. Surnames shown in all capitals, members sorted by surname, laid out in two columns per page with the second column as overflow, minimal-width checkbox columns, and wrapping column headings."

## Clarifications

### Session 2026-07-27

- Q: Should generating the printable roll require the rehearsal to already be scheduled (saved) in the system, or should it also be available directly from the "create rehearsal" form using the date being entered, before the rehearsal is saved? → A: Roll generation requires an already-scheduled (saved) rehearsal; the print action is surfaced afterward (e.g., from the rehearsal's list row or detail/attendance page), not from the in-progress create form.

### Correction — 2026-07-28

The initial implementation (User Story 2 below, "Annual Fee Paid") did not match the actual requirement and is superseded by this correction:

- Q: Should the roll list whoever is active *right now* (at print time), or whoever was active *as of the rehearsal's date*? → A: As of the rehearsal's date — a point-in-time snapshot, so a roll reprinted later for a past rehearsal still reflects who was actually active for that rehearsal.
- Q: Should the "Attended" checkbox always print blank? → A: No. The roll is printed before attendance is taken (so it prints blank, ready for hand-marking) but may also be reprinted after attendance has been recorded digitally — in that case it MUST show the real recorded attendance, not a blank box.
- Q: Is the "Annual Fee Paid" checkbox still required? → A: No, removed. It is not part of this spec.
- Q: Should the "Rehearsal Fee Paid" checkbox column keep its label, or show something else? → A: Its header is replaced by a short static "Pd" (Paid) label — not the fee amount and not the original descriptive text. (Superseded once more: an earlier revision of this correction showed the configured attendance fee amount, e.g. "$5", in the header instead; that was replaced with the static "Pd" label per direct follow-up feedback.) The checkbox itself keeps real payment-status semantics (see FR-007).
- Q: Should the "Rehearsal Fee Paid" checkbox also reflect real state when reprinted after the fact? → A: Yes, and independently of the "Attended" checkbox — it reflects whether the attendance fee was actually recorded as paid, since a member can be marked attended but have their fee marked unpaid (see the existing "mark as unpaid" option on the digital attendance-entry screen).
- Q: Should the roll's header still show a "Generated: <timestamp>" line? → A: No, removed per direct follow-up feedback — the header now shows only the organization name, "Attendance Roll" title, and the rehearsal date/time.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Print a roll instead of maintaining a separate paper list (Priority: P1)

As a person who takes attendance at rehearsals, I want to generate and print a roll of the members active as of a specific rehearsal's date, so that I have an accurate, up-to-date paper list to record attendance and payments on during the rehearsal, without maintaining a separate manually updated document.

**Why this priority**: This is the core problem stated in the request — replacing a manually maintained paper roll with one generated on demand from the system's live member data. Without this, the other refinements in this spec have nothing to appear on.

**Independent Test**: Can be fully tested by generating a roll for a scheduled rehearsal before any attendance has been recorded, and confirming it lists every member active as of that rehearsal's date, sorted by surname, each with blank "Present" and fee checkboxes, ready to print.

**Acceptance Scenarios**:

1. **Given** a rehearsal has been scheduled and at least one member is active as of that rehearsal's date, **When** the user generates the roll for that rehearsal before attendance has been recorded, **Then** every such member appears exactly once, sorted alphabetically by surname, each with blank "Present" and fee checkboxes.
2. **Given** a member's status is archived (or the member has been soft-deleted), **When** the roll is generated, **Then** that member does not appear on the roll.
3. **Given** no members are active as of the rehearsal's date, **When** the user attempts to generate the roll, **Then** the system shows an empty-state message instead of producing a blank roll.

---

### User Story 2 - Roll reflects real attendance and fee-payment state when reprinted (Priority: P2)

As a person taking attendance, I want a roll reprinted after attendance has already been recorded digitally to show who actually attended and whether each member's attendance fee was actually paid, so the printed sheet is trustworthy as a record rather than always looking blank.

**Why this priority**: Adds real information value on top of the base roll from User Story 1 for the common case of reprinting a roll after the fact (e.g. for record-keeping or a re-print request), but the roll is fully usable pre-attendance without it.

**Independent Test**: Can be fully tested by recording attendance for a rehearsal (marking some members present, and marking at least one present member's fee as unpaid via the existing "mark as unpaid" option), then generating the roll for that same rehearsal and confirming "Present" is checked only for members actually marked attended, and the fee checkbox is checked only for members whose attendance fee has no outstanding balance.

**Acceptance Scenarios**:

1. **Given** a rehearsal for which attendance has not yet been recorded, **When** the roll is generated, **Then** every member's "Present" and fee checkboxes are shown blank.
2. **Given** a rehearsal for which attendance has been recorded and a member was marked attended with their fee paid, **When** the roll is generated, **Then** that member's "Present" and fee checkboxes are both shown checked.
3. **Given** a rehearsal for which attendance has been recorded and a member was marked attended but their fee was marked unpaid, **When** the roll is generated, **Then** that member's "Present" checkbox is shown checked and their fee checkbox is shown unchecked.
4. **Given** a rehearsal for which attendance has been recorded and a member was marked absent (or has no attendance record), **When** the roll is generated, **Then** that member's "Present" and fee checkboxes are both shown unchecked.

---

### User Story 3 - Compact, print-friendly layout (Priority: P3)

As a person taking attendance, I want the roll printed in a compact two-column layout with narrow checkbox columns, wrapped column headings, and surnames in capitals, so the sheet is easy to scan and uses as few printed pages as possible.

**Why this priority**: A formatting and usability refinement on top of Stories 1 and 2 — the roll is functional without it, but this is explicitly requested to make the printed sheet practical to carry and use during a rehearsal.

**Independent Test**: Can be fully tested by generating a roll with enough members to overflow a single column and confirming the list continues into a second column on the same page, that checkbox columns are visibly narrower than the name column, that column headings wrap rather than widen the column, and that every surname is displayed in capital letters.

**Acceptance Scenarios**:

1. **Given** enough members to fill more than one column, **When** the roll is generated, **Then** the alphabetically sorted list fills the first column completely before continuing into the second column on the same page.
2. **Given** a printed roll, **When** it is reviewed, **Then** the "Present" and fee-amount checkbox columns are visibly narrower than the member name column(s).
3. **Given** a column heading whose text is wider than its column, **When** the roll is generated, **Then** the heading text wraps onto multiple lines rather than widening the column or being cut off.
4. **Given** any member on the roll, **When** their name is displayed, **Then** their surname is shown in all capital letters.

---

### Edge Cases

- What happens when there are more members than fit on a single page even with two columns? The roll continues onto additional pages, repeating the two-column layout and column headings on each page.
- How does the system handle a member who is active as of the rehearsal's date but has no attendance record for it yet? They are shown with unchecked "Present" and fee checkboxes.
- How does the system handle a member who was marked attended but their fee payment is only partially settled (outstanding balance remains)? Their fee checkbox is shown unchecked, since an outstanding balance remains.
- How does the system handle two members who share the same surname? They are sub-sorted by first name so the alphabetical order remains stable and unambiguous.
- What happens if the roll is generated again later (e.g., after new members joined or others became inactive, or after attendance has since been recorded) for the same rehearsal? Membership is recomputed as of the rehearsal's date each time (so it does not change based on today's roster), while the "Present" and fee checkboxes always reflect whatever attendance/payment data exists in the system at the moment of generation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow a user to generate a printable member roll for any already-scheduled (saved) rehearsal; the action is surfaced from an existing rehearsal (e.g., its list row or detail/attendance page), not from the in-progress "create rehearsal" form prior to saving.
- **FR-002**: The roll MUST list every member who was active as of the rehearsal's scheduled date (a point-in-time snapshot derived from each member's status and activation/inactivation history), not merely whoever is active at print time; archived and soft-deleted members MUST be excluded. This uses the same point-in-time active-membership definition already used to compute the rehearsal's attendance rate.
- **FR-003**: The roll MUST display each member's surname in all capital letters, alongside their first name.
- **FR-004**: Members on the roll MUST be sorted alphabetically by surname, then by first name for members who share a surname.
- **FR-005**: Each member row MUST include a "Present" checkbox column. If no attendance has been recorded yet for that rehearsal, it MUST print unchecked, for the attendance-taker to mark by hand. If attendance has already been recorded for that rehearsal, it MUST show checked for members recorded as attended and unchecked for members recorded as absent or not recorded.
- **FR-006**: Each member row MUST include a fee checkbox column whose column heading is a static "Pd" (Paid) label. If no attendance fee has been recorded yet for that member for that rehearsal, the checkbox MUST print unchecked. If an attendance fee has been recorded, the checkbox MUST show checked only when that fee has no outstanding balance, and unchecked otherwise (including when the member was marked attended but the fee was marked unpaid).
- **FR-007**: The fee checkbox described in FR-006 reflects actual payment status and is independent of the "Present" checkbox in FR-005 — a member may be checked "Present" while their fee checkbox remains unchecked, and vice versa is not possible (no fee is recorded for a rehearsal a member wasn't marked attended for).
- **FR-008**: The roll MUST display the rehearsal's date (and time, if recorded) so the printed sheet can be identified and matched to the correct rehearsal. It MUST NOT display a "Generated: <timestamp>" line.
- **FR-009**: When the number of members exceeds what fits in a single column on a page, the roll MUST continue the same alphabetically sorted list into a second column on that page as overflow, rather than starting a new or separate list, and MUST continue onto additional pages using the same two-column layout if the list is still longer than one page.
- **FR-010**: The "Present" and fee checkbox columns MUST be rendered at minimal width relative to the name column(s).
- **FR-011**: Column headings MUST wrap onto multiple lines to fit their column width rather than widening the column or truncating the heading text.
- **FR-012**: The roll MUST be produced in a form suitable for printing, consistent with how other reports in the system are generated and printed.
- **FR-013**: If there are no members active as of the rehearsal's date at the time of generation, the system MUST show an empty-state message instead of producing a blank printable roll.

### Key Entities *(include if feature involves data)*

- **Member**: Existing entity. Surname, first name, status, and activation/inactivation dates are used to determine point-in-time roll membership. No new fields are introduced.
- **Rehearsal**: Existing entity. Supplies the date (and time) used both to identify the printed sheet and to determine point-in-time roll membership. Unchanged by this feature.
- **AttendanceRecord**: Existing entity. Read to determine whether the "Present" checkbox is checked, when attendance has already been recorded for the rehearsal. This feature only reads it — generating the roll never creates, updates, or deletes an attendance record.
- **Fee (Attendance type)**: Existing financial record, together with the member's ledger balance, used to determine whether the fee checkbox is checked. This feature only reads existing fee and balance data — generating the roll does not create, modify, or delete any fee, payment, or ledger record.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can generate a printable roll for a scheduled rehearsal in a few clicks, without needing to create or maintain any separate manual roll document.
- **SC-002**: 100% of members active as of the rehearsal's date appear on a generated roll, and no archived, soft-deleted, or not-yet-active-as-of-that-date members appear.
- **SC-003**: When a roll is generated before attendance has been recorded for that rehearsal, 100% of "Present" and fee checkboxes print blank.
- **SC-004**: When a roll is generated after attendance has been recorded for that rehearsal, the "Present" checkbox matches each listed member's actual recorded attendance with 100% accuracy, and the fee checkbox matches each listed member's actual attendance-fee payment status with 100% accuracy.
- **SC-005**: Members appear in correct alphabetical surname order, with surnames shown in all capitals, on 100% of generated rolls.
- **SC-006**: For a typical rehearsal roster, the two-column layout keeps the printed roll to a single page, compared to needing two pages for the same member count in a single-column layout.

## Assumptions

- The roll lists members who were active as of the rehearsal's scheduled date — the same point-in-time "who was active" definition already used to compute the rehearsal's attendance rate — rather than whoever happens to be active at print time. Reprinting a roll for a past rehearsal after membership has since changed still reflects who was actually active for that rehearsal.
- The fee column's heading is a fixed "Pd" label, not a value read from Settings or any other data — Settings is no longer a data dependency of this feature.
- "Fee paid" means the member has an Attendance-type fee recorded for that specific rehearsal with no outstanding balance owed. A member for whom no attendance fee has been recorded yet for that rehearsal is treated as unpaid (checkbox unchecked).
- The roll can be generated, and re-generated, for a rehearsal at any point after it has been scheduled — both before attendance is recorded (to bring to the rehearsal on paper) and after (to reprint an accurate record) — rather than being restricted to a single step of the creation workflow.
- The roll is produced only as a printable document; no separate spreadsheet-style export is needed, since its value is the physical checkbox layout intended for handwriting and its at-a-glance real-state view.
- Generating the roll is a read-only operation — it does not create, update, or delete any Member, Rehearsal, AttendanceRecord, Fee, Payment, or Transaction record.
- This feature remains the intended work for issue #257; this correction supersedes the "Annual Fee Paid" behavior from the original implementation, which did not match the actual requirement.
