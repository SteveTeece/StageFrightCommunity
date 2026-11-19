# Feature Specification: External Organisations

**Feature Branch**: `032-external-organisations`
**Created**: 2026-11-20
**Status**: Draft
**Source**: #388

## Clarifications

### Session 2026-11-20

- Q: Which kinds of financial entries should a user be able to link to an organisation? → A: Expense payments and income entries only.
- Q: Should a user be able to link an earlier or unlinked expense or income entry to an organisation afterwards? → A: No. Linking happens only when the entry is recorded.
- Q: What details should each contact person carry? → A: Name (required), role, phone, mobile, email.
- Q: On the expense payment screen, how should the organisation choice work alongside the free-text Payee field? → A: Choosing an organisation fills an empty payee box with its name; the user can still edit it.
- Q: Where in the main navigation should the Organisations screen appear? → A: Its own top-level "Organisations" menu entry, next to Members.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Keep a register of external organisations (Priority: P1)

A committee member (usually the secretary or treasurer) opens a new Organisations screen and adds the venues, suppliers, councils, sponsors and other groups the society deals with. For each one they record the organisation's name, its postal or street address, and the email address that receipts and correspondence should go to. They can later find an organisation in a sortable, searchable list, open it, correct its details, and archive it when the society no longer deals with it.

**Why this priority**: The register is the core of the request. Contacts and financial history both hang off it.

**Independent Test**: Add, edit, search and archive organisations with no contacts and no financial links. The list and detail screens alone give the group a usable address book.

**Acceptance Scenarios**:

1. **Given** the Organisations list is empty, **When** the user adds an organisation with a name, address and email, **Then** it appears in the list showing its name and email.
2. **Given** an organisation exists, **When** the user changes its address and saves, **Then** the detail screen shows the new address and an audit-trail entry records the change.
3. **Given** several organisations exist, **When** the user types part of a name into the search box, **Then** only organisations whose name contains that text are listed.
4. **Given** an active organisation, **When** the user archives it, **Then** it disappears from the default list and reappears when "show archived" is switched on.
5. **Given** an organisation named "City Council" already exists, **When** the user tries to add another active organisation named "city council", **Then** the save is refused with a message that the name is already in use.
6. **Given** the user enters an email address that is not a valid address, **When** they save, **Then** the save is refused and the email field shows a validation message.

---

### User Story 2 - Record contact people for each organisation (Priority: P2)

On an organisation's detail screen the user adds the people they deal with there, such as the venue manager or the sponsor's marketing officer. For each person they record a name, their role at the organisation, a phone number, a mobile number and an email address. They can correct or remove a contact when that person moves on.

**Why this priority**: Contacts make the register useful day to day, but an organisation record is still worth having without them.

**Independent Test**: On one existing organisation, add two contacts, edit one, remove the other, and confirm that the detail screen reflects each change.

**Acceptance Scenarios**:

1. **Given** an organisation with no contacts, **When** the user adds a contact with a name, role, phone, mobile and email, **Then** the contact appears in that organisation's contact list with all five details.
2. **Given** an organisation with two contacts, **When** the user removes one, **Then** only the other remains listed, and the removed contact is kept in the data but hidden.
3. **Given** the user adds a contact with a name and nothing else, **When** they save, **Then** the contact is saved, because only the name is required.
4. **Given** the user adds a contact with no name, **When** they save, **Then** the save is refused with a message that a name is required.
5. **Given** the user enters a contact email that is not a valid address, **When** they save, **Then** the save is refused and the contact's email field shows a validation message.

---

### User Story 3 - See the financial history with an organisation (Priority: P2)

When the treasurer records an expense payment or an income entry, they can optionally choose which organisation it was paid to or received from. On the organisation's detail screen, a financial history lists every linked transaction with its date, description, account and amount, plus a total paid to and a total received from that organisation.

**Why this priority**: This turns the register into the "small CRM" the issue asks for. It depends on Story 1 and adds to the existing finance entry screens.

**Independent Test**: Record one expense to an organisation and one income entry from it. Open the organisation and confirm that both appear, with correct totals.

**Acceptance Scenarios**:

1. **Given** an active organisation "Town Hall", **When** the treasurer records a $500 expense payment and selects "Town Hall" as the organisation, **Then** Town Hall's financial history lists that payment and shows "Total paid" of $500.
2. **Given** "Town Hall" has one linked expense of $500 and one linked income entry of $200, **When** the user opens its detail screen, **Then** the history lists both entries, newest first, with "Total paid" $500 and "Total received" $200.
3. **Given** the treasurer records an expense without choosing an organisation, **When** it is saved, **Then** it posts exactly as it does today and appears in no organisation's history.
4. **Given** an organisation is archived, **When** the treasurer opens the expense or income entry screen, **Then** that organisation is not offered for selection, but its existing financial history stays intact and viewable.
5. **Given** the society's configured currency is not AUD, **When** the history is shown, **Then** every amount and total uses that currency's symbol and precision.
6. **Given** the expense payment screen with an empty payee box, **When** the treasurer chooses "Town Hall" as the organisation, **Then** the payee box shows "Town Hall" and can still be edited.
7. **Given** the treasurer has typed "Town Hall Café" as the payee, **When** they then choose "Town Hall" as the organisation, **Then** the payee box still shows "Town Hall Café".

---

### Edge Cases

- **Empty register**: the list shows an empty-state message and an "Add organisation" action.
- **Archiving an organisation that has financial history**: allowed. The linked transactions are never altered, and the history stays viewable from the archived organisation.
- **Restoring an archived organisation**: the user can restore it, provided no active organisation has taken its name in the meantime. If one has, the restore is refused with the duplicate-name message.
- **Duplicate names**: compared case-insensitively and ignoring leading and trailing spaces, among active organisations only. An archived organisation's name may be reused.
- **Expense entered with a free-text payee and an organisation**: both are kept. The payee box is filled with the organisation's name only when it was empty. Whatever payee is saved still forms part of the transaction description, as it does today.
- **Organisation choice changed or cleared before saving**: a payee that was filled in automatically stays as it is. The user edits or clears it by hand.
- **Existing transactions recorded before this feature**: they stay unlinked. Financial records are immutable, so they cannot be linked to an organisation afterwards.
- **Closed financial periods**: choosing an organisation adds nothing to the closed-period rules. An entry the period guard refuses today is still refused.
- **Long text**: the address takes multiple lines. The name, email and every contact field have sensible maximum lengths, with a validation message when one is exceeded.
- **Backup and restore**: organisations, their contacts and the organisation link on transactions survive a backup followed by a restore. A backup taken before this feature restores with an empty register.
- **Language**: every label, message and empty state appears in the user's chosen display language.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide an Organisations screen, reached from its own top-level "Organisations" menu entry placed next to Members, that lists active organisations in a sortable, paged grid showing name, email and number of contacts.
- **FR-002**: Users MUST be able to add an organisation with a required name, an optional multi-line address, and an optional email address for receipts and correspondence.
- **FR-003**: The system MUST refuse to save an organisation whose name matches another active organisation's name, ignoring case and surrounding spaces.
- **FR-004**: The system MUST refuse to save an organisation or contact whose email or text fields fail format or length validation, and show a message next to the offending field.
- **FR-005**: Users MUST be able to edit an organisation's name, address and email.
- **FR-006**: Users MUST be able to archive an organisation and restore an archived one. Archiving MUST hide the organisation without deleting its data.
- **FR-007**: The Organisations list MUST offer a "show archived" switch and a name search that filters as the user types.
- **FR-008**: Users MUST be able to add, edit and remove contact people on an organisation. Each contact has a required name and an optional role, phone number, mobile number and email address.
- **FR-009**: Removing a contact MUST hide it without deleting its data.
- **FR-010**: The expense payment and income entry screens MUST offer an optional choice of active organisation. Choosing one MUST NOT change the amounts, accounts or tax posted. No other finance entry screen (bank deposits, general journal, member fees, member payments) offers an organisation choice. The link is set only when the entry is recorded and can never be added, changed or removed afterwards. On the expense payment screen, choosing an organisation while the payee box is empty MUST fill the payee box with the organisation's name. A payee the user has already typed MUST NOT be overwritten, and the filled-in name stays editable.
- **FR-011**: An organisation's detail screen MUST show its financial history: every linked transaction, newest first, with date, description, account and amount, plus a total paid to and a total received from that organisation.
- **FR-012**: Financial history totals MUST count only the bank-side movement of each linked entry once, never every ledger line of the entry, so that double-entry pairs are not double-counted.
- **FR-013**: Every amount shown in the financial history MUST use the society's configured currency formatting.
- **FR-014**: Creating, editing, archiving and restoring an organisation, and adding, editing and removing a contact, MUST each write an audit-trail entry.
- **FR-015**: A backup MUST include organisations, contacts and transaction-to-organisation links, and a restore MUST bring them back. Restoring a backup made before this feature MUST succeed with an empty register.
- **FR-016**: All new user-facing text MUST be translated into every shipped display language in the same change.

### Key Entities

- **Organisation**: an external body the society deals with. It has a name (required, unique among active organisations), a multi-line address, an email for receipts and correspondence, and an archived state. It has many contacts and many linked financial entries.
- **Organisation Contact**: a person at an organisation. It has a name (required), a role, a phone number, a mobile number and an email address. It belongs to exactly one organisation. A removed contact is hidden, not deleted.
- **Organisation link on a financial entry**: an optional reference from an expense payment or income entry to the organisation it was paid to or received from. It is set when the entry is recorded and never changes afterwards, like the rest of the financial record.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can add a new organisation with an address, an email and one contact in under 2 minutes.
- **SC-002**: A user can find any organisation in a register of 200 by typing part of its name, with matching results showing within 1 second.
- **SC-003**: For an organisation with linked entries, the financial history totals match the sum of the bank-side amounts of those entries exactly, in 100% of tested cases.
- **SC-004**: Recording an expense or income entry with no organisation chosen produces ledger postings byte-identical to those recorded before this feature.
- **SC-005**: After a backup and restore, 100% of organisations, contacts and transaction links are present and unchanged.
- **SC-006**: No new screen text falls back to English in any shipped non-English display language.

## Assumptions

- Phone and mobile numbers are free text, so local and international formats are both accepted. Only email addresses are format-checked.
- The organisation's email is stored for reference only. Sending receipts or correspondence by email is out of scope.
- Financial history covers expense payments and income entries, the two screens where the society deals money with outside parties. Member fees, member payments and general journal entries are not linked to organisations.
- Linking happens only when an entry is recorded. Existing transactions, including those with a free-text payee, are not linked retroactively, because financial records are immutable.
- Any committee user who can use the Finance and Members screens today can manage organisations. The app has no per-user permissions, and this feature adds none.
- The organisation detail screen and the list follow the app's existing grid, list-box and toggle standards.
- Out of scope: interaction notes or a meeting log, importing organisations from a file, merging duplicates, a printable organisations report, and linking organisations to events or rehearsals.
