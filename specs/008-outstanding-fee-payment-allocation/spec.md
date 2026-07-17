# Feature Specification: Outstanding Fee Selection on the Record Member Payment Form

**Feature Branch**: `008-outstanding-fee-payment-allocation`

**Created**: 2026-07-17

**Status**: Draft

**Input**: User description: "on the page that records member payments, add a grid containing the outstanding items for the member selected. There should also be a check box for the user to select what outstanding fees are being paid. The check box should be to the left of the member's name. As the user selects the transactions to be paid, the total paid should be updated to reflect the total of the selected items."

## Clarifications

### Session 2026-07-17

- Q: Should checking specific fee rows actually control which fees get paid off, or just calculate the total? → A: True allocation — the checked fees are exactly what gets paid (in whatever order checked), not just a UI convenience total while the backend keeps paying oldest-first regardless of selection.
- Q: Should the Amount field be read-only (auto-computed), or editable after checking items? → A: Editable, pre-filled from selection — checking fees fills in a starting total, and the user can reduce it to record a partial payment.
- Q: If Amount exceeds the sum of checked fees, what happens? → A: Block with a validation error — Amount can never exceed the sum of checked fees.
- Q: When Amount is less than the sum of checked fees, in what order is it applied across them? → A: Oldest fee first (by FeeDate/DueDate), fully settling each before moving to the next — consistent with the FIFO convention already used in GL posting.
- Q: The request said "left of the member's name" — but each grid row is an outstanding fee for the one already-selected member, not a list of members. What does the checkbox sit to the left of? → A: The checkbox is the leftmost column, before Fee Type/Amount/Due Date — same layout convention as the existing AttendanceGrid/ParticipationGrid checkbox columns.
- Q: Should recording a payment require at least one checked fee? → A: Yes — Save is blocked if Amount > 0 but no fee is checked, since allocation is now driven entirely by the checkboxes.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Select which outstanding fees a payment covers (Priority: P1)

A committee member recording a payment from a member (via Finance → Outstanding → Record Member Payment) wants to see that member's outstanding fees and choose exactly which ones this payment settles, rather than the system silently deciding for them.

**Why this priority**: This is the core of the request — without it, the payment form has no visibility into what's actually owed, and allocation happens invisibly (always oldest-fee-first across the member's entire history) with no user control.

**Independent Test**: Open the payment form for a member with multiple outstanding fees, confirm each fee is listed with its remaining amount owed, check one or more fees, and confirm the Amount field reflects their combined total.

**Acceptance Scenarios**:

1. **Given** a member with outstanding fees is selected on the Record Member Payment form, **When** the form loads, **Then** a grid lists each of that member's outstanding fees, showing at minimum its type, fee date, due date, and remaining amount owed.
2. **Given** the outstanding fees grid is displayed, **When** the user checks a fee's checkbox, **Then** the Amount field updates to the sum of all currently checked fees' remaining amounts.
3. **Given** one or more fees are checked, **When** the user unchecks a fee, **Then** the Amount field updates to reflect the new sum of checked fees.
4. **Given** the outstanding fees grid, **When** the user checks a "select all" control in the grid header, **Then** every listed fee is checked and the Amount field updates to their combined total.
5. **Given** a fee was partially paid by a previous payment, **When** it appears in the outstanding fees grid, **Then** its displayed amount is the remaining unpaid balance on that fee, not its original full amount.

---

### User Story 2 - Record a full or partial payment against selected fees (Priority: P1)

Having selected which fees a payment covers, the user needs to actually save that payment and have it apply to exactly those fees — fully if the amount covers them, or partially (oldest-selected-first) if it doesn't.

**Why this priority**: Selection without correct allocation on save would be misleading — the GL must reflect what the user actually chose, not silently fall back to the old behavior.

**Independent Test**: Check two fees, submit with an amount equal to their combined total, and confirm both fees no longer appear as outstanding. Repeat with a smaller amount and confirm only the older fee is (fully or partially) settled.

**Acceptance Scenarios**:

1. **Given** the user has checked one or more fees and the Amount equals their combined remaining total, **When** the payment is saved, **Then** each checked fee is fully settled and no longer appears as outstanding for that member.
2. **Given** the user has checked multiple fees and reduces Amount below their combined total, **When** the payment is saved, **Then** the amount is applied to the checked fees oldest-first, fully settling as many as the amount covers and partially settling the next one, leaving any remaining checked fees untouched.
3. **Given** the user has checked one or more fees, **When** they attempt to save with Amount greater than the checked fees' combined remaining total, **Then** the save is blocked with a validation message and no payment is recorded.
4. **Given** no fee is checked, **When** the user attempts to save (with Amount > 0), **Then** the save is blocked with a validation message indicating at least one outstanding fee must be selected.
5. **Given** the payment has been successfully saved, **When** the form re-renders in its post-save state, **Then** the outstanding fees grid becomes read-only (checkboxes disabled), consistent with the existing post-save behavior of the other form fields.

---

### Edge Cases

- **Member with no outstanding fees**: If the selected member currently has no outstanding fees (e.g. reached via a direct URL rather than the Outstanding grid, after their balance was already cleared), the grid shows an empty-state message and Save is disabled — there is nothing to allocate a payment against.
- **Existing callers unaffected**: Other code paths that record payments without a fee selection (e.g. data seeding, any future bulk/import tooling) continue to behave exactly as before — automatic FIFO allocation across the member's full unpaid fee history, with the existing overpayment-credit handling. This feature only changes behavior when the caller explicitly supplies selected fees, which only the updated Record Member Payment form does.
- **Fee list reflects true remaining balance**: A fee that was partially settled by an earlier payment shows only its remaining unpaid amount in the grid (and that is the amount actually payable against it), not its original full amount — otherwise the "can't exceed checked total" validation would be checking against the wrong number.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Record Member Payment form MUST display a grid of the selected member's outstanding fees, each showing its type, fee date, due date, and remaining amount owed.
- **FR-002**: Each row in the outstanding fees grid MUST have a checkbox as its leftmost column, letting the user select that fee.
- **FR-003**: The outstanding fees grid MUST provide a "select all" control that checks/unchecks every listed fee.
- **FR-004**: Whenever the set of checked fees changes, the Amount field MUST be updated to the sum of the checked fees' remaining amounts owed.
- **FR-005**: The Amount field MUST remain editable after being updated from a selection, allowing the user to reduce it below the checked total to record a partial payment.
- **FR-006**: The form MUST block saving (with a validation message) if Amount exceeds the sum of the checked fees' remaining amounts.
- **FR-007**: The form MUST block saving (with a validation message) if no fee is checked.
- **FR-008**: When a payment is saved with a set of checked fees, the amount MUST be applied to those fees in oldest-first order (by FeeDate/DueDate), fully settling each fee before applying any remainder to the next.
- **FR-009**: Recording a payment against selected fees MUST NOT allocate any part of the payment to fees that were not checked.
- **FR-010**: After a payment is successfully saved, the outstanding fees grid MUST become read-only (checkboxes disabled), consistent with the existing post-save behavior of the rest of the form.
- **FR-011**: The remaining amount owed shown for a fee MUST reflect any prior partial settlement of that fee (original fee amount minus amounts already allocated to it by earlier payments), not its original full amount.
- **FR-012**: Recording a payment without any fee selection (via existing code paths that don't supply one) MUST continue to behave exactly as before this feature — automatic FIFO allocation across the member's full unpaid fee history, including existing overpayment-credit handling.
- **FR-013**: If the selected member has no outstanding fees, the form MUST show an empty-state message in place of the grid and disable Save.

### Key Entities *(include if feature involves data)*

- **Fee**: An existing, immutable financial obligation record. This feature does not add fields to `Fee` — it adds a computed, read-only "remaining amount owed" view over existing fees for display and selection.
- **Payment**: An existing immutable payment record. This feature adds an optional list of selected fee identifiers to the *request* used to create a payment, not to the stored `Payment` record itself — allocation is still expressed entirely through GL `Transaction` rows, as it is today.
- **Transaction (GL)**: Existing immutable GL debit/credit entries linking a `Payment` to a `Fee`. This feature changes which fees a given payment's transactions link to (the checked fees, oldest-first) when the caller supplies a selection; the linkage mechanism itself is unchanged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user recording a member payment can see, without leaving the payment form, exactly which fees are outstanding and how much remains owed on each.
- **SC-002**: A user can select any combination of outstanding fees and the Amount field always reflects their exact combined remaining total, with no manual arithmetic required.
- **SC-003**: After saving a payment against selected fees, those fees' outstanding status (fully or partially settled) matches exactly what was checked and how much was paid — never a fee that wasn't checked.
- **SC-004**: 100% of payments recorded through this form are traceable, via GL transactions, to the specific fee(s) the user selected.
- **SC-005**: Existing automated payment flows that don't select specific fees (data seeding, existing tests) continue to pass unmodified, confirming no regression to current FIFO-all-fees behavior.

## Assumptions

- "Outstanding fees" for the grid means the same set of fees already surfaced today in the Outstanding tab's per-member fee breakdown (`MemberBalanceList`) — fees with a nonzero remaining balance — not a new definition of what counts as outstanding.
- The existing FIFO-across-all-fees behavior of `IPaymentService.RecordAsync` is preserved as the default for any caller that does not supply a fee selection; this feature adds a new, additive way to invoke it, rather than replacing its existing contract.
- No new user roles or permissions are introduced; anyone who could already reach the Record Member Payment form retains that same access.
- This feature does not change how fees are created, how GL balances are computed elsewhere (Chart of Accounts, Trial Balance, dashboard tiles), or how the Outstanding tab's member list/fee-breakdown display works — only the Record Member Payment form's behavior when saving a payment.
