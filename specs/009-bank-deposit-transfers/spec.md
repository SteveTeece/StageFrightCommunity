# Feature Specification: Bank Deposit Recording

**Feature Branch**: `009-bank-deposit-transfers`

**Created**: 2026-07-23

**Status**: Draft

**Input**: User description: "create a new spec (and branch) based on issue #237 — The Transfer and Journal Entry pages in the finance module do effectively the same thing. The Transfer tab probably should be refactored to record bank deposits of cash collected. This should reduce the total in the cash account and increase the balance of the bank account per standard accounting conventions."

## Clarifications

### Session 2026-07-23

- Q: When a bank deposit is recorded, should it be classified under the same record type as historical generic transfers, or given its own distinct classification? → A: Its own distinct classification — bank deposits are recorded as a distinct kind of financial record, separate from the historical "transfer" classification, even though both still post matching debit/credit entries the same way.
- Q: What should the page previously labeled "Transfer" be titled/labeled going forward? → A: "Record Bank Deposit".

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Record a bank deposit of collected cash (Priority: P1)

A treasurer has collected cash (e.g., door sales at an event, cash membership fee payments) and physically deposited it at the bank. They need to record that deposit in the system so the club's books show the cash removed from the cash-on-hand float and added to the correct bank account balance.

**Why this priority**: This is the core, most frequent real-world action a treasurer performs and is the specific workflow issue #237 asks for. It delivers the primary value: accurate, easy-to-record cash vs. bank balances.

**Independent Test**: Can be fully tested by recording a deposit of a set amount from Cash on Hand into a nominated bank account and confirming Cash on Hand decreases and the bank account balance increases by that amount, with no other accounts affected.

**Acceptance Scenarios**:

1. **Given** a Cash on Hand balance of $500 and a bank account "Operating Account" with a balance of $2,000, **When** the treasurer records a $300 deposit into Operating Account, **Then** Cash on Hand shows $200 and Operating Account shows $2,300.
2. **Given** the bank deposit form, **When** the treasurer enters an amount of $0 or a negative amount, **Then** the system rejects the entry with a clear validation message and no ledger entry is created.
3. **Given** a successfully recorded deposit, **When** the treasurer views the account activity for Cash on Hand and the destination bank account, **Then** both show the deposit with a matching date, amount, and description.

---

### User Story 2 - One clear workflow instead of two overlapping ones (Priority: P2)

A treasurer needs to move money into the bank and wants one obvious workflow for it, instead of two overlapping pages (Transfer and Journal Entry) that do almost the same thing, so they don't accidentally use the wrong page or create inconsistent records.

**Why this priority**: Reduces confusion and data-entry errors, addressing the "duplicate functionality" complaint at the heart of issue #237. It's secondary to actually delivering the deposit workflow in User Story 1.

**Independent Test**: Can be tested by confirming the page previously labeled "Transfer" now presents only the bank-deposit-specific workflow (fixed cash source, bank destination picker), while the ability to move funds between two arbitrary accounts for other reasons remains available solely through the Journal Entry page.

**Acceptance Scenarios**:

1. **Given** the finance navigation, **When** the treasurer opens the page previously labeled "Transfer" (now titled "Record Bank Deposit"), **Then** they see a bank-deposit-specific form (cash source fixed, destination bank account picker, amount, optional description) rather than a generic any-account-to-any-account picker.
2. **Given** a treasurer wants to correct a misallocated entry between two bank accounts (not a cash deposit), **When** they look for that capability, **Then** they find it available via the existing Journal Entry page, unchanged.

---

### User Story 3 - Historical transfers remain accurate in reports (Priority: P3)

A treasurer or auditor reviewing historical financial reports (e.g., Account Register, Trial Balance) after this change ships needs previously recorded transfers to still appear correctly, so historical books remain accurate and unaffected by the refactor.

**Why this priority**: Important for trust and audit integrity, but it protects existing data rather than adding new capability, so it is not a blocker to shipping the new deposit workflow.

**Independent Test**: Can be tested by generating an Account Register report covering a date range that includes pre-refactor transfer entries and confirming they display unchanged (same accounts, amounts, dates, descriptions) after the refactor ships.

**Acceptance Scenarios**:

1. **Given** a transfer recorded before this change, **When** the treasurer runs the Account Register report for the relevant account, **Then** the historical entry still appears with its original debit/credit amounts and date.

---

### Edge Cases

- What happens when no bank account exists yet other than Cash on Hand (nowhere to deposit to)? The system must clearly indicate no deposit destination is available and direct the treasurer to add a bank account first, rather than allowing an invalid submission.
- What happens when the deposit amount exceeds the current Cash on Hand balance? The system still records the deposit (consistent with today's transfer behavior, which does not enforce a sufficient-funds check), resulting in a negative Cash on Hand balance the treasurer can identify and investigate.
- What happens if the treasurer leaves the description blank? The system records the deposit with a standard default description (e.g., "Bank deposit") so the entry is still identifiable in reports.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a dedicated bank-deposit workflow that records cash collected as deposited into a nominated bank account.
- **FR-002**: System MUST always source deposited funds from the club's designated Cash on Hand balance; users MUST NOT be able to choose an arbitrary source account for this workflow.
- **FR-003**: Users MUST be able to select which bank account receives the deposit, from the set of accounts designated as bank accounts, excluding Cash on Hand itself.
- **FR-004**: System MUST require a deposit date and a positive deposit amount before a deposit can be recorded.
- **FR-005**: System MUST allow an optional description for each deposit, defaulting to a standard label when left blank.
- **FR-006**: Upon recording a deposit, system MUST decrease the Cash on Hand balance and increase the selected bank account balance by the exact deposit amount, keeping the books balanced (total debits equal total credits).
- **FR-007**: System MUST prevent recording a deposit when no eligible bank account (other than Cash on Hand) exists, and MUST direct the user to add one before proceeding.
- **FR-008**: System MUST retire the prior generic "transfer between any two accounts" workflow from the page previously labeled "Transfer" — now titled "Record Bank Deposit" — so that moving funds between two accounts for reasons other than depositing collected cash continues to be handled through the existing Journal Entry workflow.
- **FR-009**: System MUST preserve all previously recorded transfer transactions and continue to display them accurately in existing financial reports after this change ships.
- **FR-010**: System MUST record an audit trail entry for every bank deposit, consistent with how other financial transactions are audited.
- **FR-011**: System MUST classify each bank deposit as its own distinct kind of financial record, separate from the historical generic "transfer" classification used by transactions recorded before this change, even though both post matching debit/credit entries in the same way.

### Key Entities *(include if feature involves data)*

- **Bank Deposit**: A record of cash moved from the Cash on Hand balance into a specific bank account; includes date, amount, destination bank account, and an optional description. Recorded as its own distinct classification of financial record, separate from the historical generic "transfer" classification (see FR-011).
- **Bank Account**: An account designated to receive deposits; distinguished from other financial accounts by being flagged as a bank account.
- **Cash on Hand**: The club's single designated holding account for cash collected before it is deposited at the bank; the fixed source for every bank deposit.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Treasurers can record a bank deposit in under 30 seconds from opening the page to confirmation, without needing to manually calculate or enter offsetting debit/credit amounts.
- **SC-002**: 100% of recorded bank deposits leave Cash on Hand decreased and the chosen bank account increased by the exact same amount, with the books remaining balanced.
- **SC-003**: After launch, there is exactly one page treasurers use to record a cash-to-bank deposit, eliminating the duplicate-functionality complaint raised in issue #237.
- **SC-004**: Financial reports (e.g., Account Register, Trial Balance) covering periods before and after this change show consistent, accurate account balances with no discrepancies introduced by the refactor.

## Assumptions

- The club has exactly one designated Cash on Hand account acting as the universal cash-collection bucket, as is currently the case; this feature does not introduce support for multiple concurrent cash floats or petty-cash accounts.
- Deposits are one-directional (cash to bank); withdrawing cash back out of a bank account is out of scope for this feature and can continue to be handled via Journal Entry if ever needed.
- The general-purpose ability to move funds between any two arbitrary accounts is not being removed from the system altogether — it remains available via the existing Journal Entry workflow, which already supports multi-line, any-account postings. This feature only narrows the page previously labeled "Transfer" to the specific bank-deposit use case, to eliminate duplication with Journal Entry.
- No minimum/maximum deposit amount limits or approval workflow are required beyond the existing positive-amount validation used elsewhere in Finance.
- Existing historical transfer records remain valid and are not migrated or reclassified; only the entry point for new records changes going forward.
