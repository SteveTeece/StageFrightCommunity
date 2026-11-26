# Feature Specification: Banking Rework — Undeposited Funds, Petty Cash, Deposits and Cash Withdrawals

**Feature Branch**: `033-banking-cash-deposits`
**Created**: 2026-11-26
**Status**: Draft
**Source**: #368

## Clarifications

### Session 2026-11-26

- Q: Where does a payment or income received by card or electronic transfer go? → A: Straight into a bank account the user picks when entering it. It never passes through the holding account.
- Q: One cash account or two? → A: One. The existing Cash on Hand system account is renamed **Petty Cash**. Cash takings and cash withdrawn as event floats both land in it.
- Q: How is a mistaken deposit corrected? → A: By reversing the whole deposit. Its cheques return to "awaiting deposit", its cash returns to Petty Cash, and the user records the deposit again.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cheques wait in a holding account until they are banked (Priority: P1)

A treasurer receives a cheque from a member for their annual fee, and another from a sponsor at a fundraiser. Each is recorded as received the day it arrives. The member's balance drops at once. The money does not appear in the bank account yet, because it is not there yet. It sits in **Undeposited Funds**, listed cheque by cheque, until the treasurer takes it to the bank.

**Why this priority**: Recording money as "in the bank" before it is banked is the root of the confusion in #368. Every other story depends on received items being held somewhere visible first.

**Independent Test**: Record a member payment by cheque and an income entry by cheque. Confirm that both appear as separate items awaiting deposit, that the Undeposited Funds balance equals their total, that no bank account balance changed, and that the member's outstanding balance fell by the cheque amount.

**Acceptance Scenarios**:

1. **Given** a member owes a fee, **When** the user records a payment by cheque, **Then** the member's outstanding balance falls by the payment amount, the Undeposited Funds balance rises by the same amount, and no bank account balance changes.
2. **Given** a non-member income entry, **When** the user records it as received by cheque, **Then** the income is recognised in its income account and the amount is held in Undeposited Funds.
3. **Given** two cheques have been received and not deposited, **When** the user opens the Banking screen, **Then** both cheques are listed as awaiting deposit, each with its date, payer (per FR-008), optional cheque reference, and amount.
4. **Given** a payment received by card or electronic transfer, **When** the user records it, **Then** the amount goes straight into the bank account the user chose and is not listed as awaiting deposit.

---

### User Story 2 - Cash takings go into Petty Cash (Priority: P1)

At a rehearsal, members pay their rehearsal fee in cash. After the show, the raffle takings are counted. Each cash receipt is recorded as received into **Petty Cash** (the renamed Cash on Hand account). The Banking screen shows how much cash is on hand and available to deposit.

**Why this priority**: Cash is the most common way the organisation is paid. It must be kept apart from cheques, because a cash deposit is a counted total while cheques are listed one by one.

**Independent Test**: Record a cash member payment, a cash rehearsal-fee payment and a cash income entry. Confirm that the Petty Cash balance rises by their total and that none of them is listed individually as a cheque awaiting deposit.

**Acceptance Scenarios**:

1. **Given** an existing organisation, **When** the user opens the chart of accounts after this change, **Then** the account previously called "Cash on Hand" is called "Petty Cash", keeps its account number and its full history, and its balance is unchanged.
2. **Given** a member pays in cash, **When** the payment is recorded, **Then** Petty Cash rises by the payment amount.
3. **Given** cash income from a raffle, **When** it is recorded as received in cash, **Then** Petty Cash rises by the amount and the income account is credited.

---

### User Story 3 - Make a bank deposit from what is waiting (Priority: P1)

The treasurer goes to the bank with three cheques and some of the cash. In the app they start a new deposit and pick the destination bank account. They tick the cheques they are banking and enter the cash amount they are banking. The app shows the deposit total split into cash and cheques, matching the bank's deposit slip. Once saved, the bank account rises by the deposit total. The cheques leave the awaiting-deposit list, and Petty Cash falls by the cash amount.

**Why this priority**: This replaces the confusing "Record Bank Deposit" screen named in #368. It is the single action that moves collected money into the bank.

**Independent Test**: With two undeposited cheques and a Petty Cash balance, record a deposit that includes one cheque and part of the cash. Confirm that the bank account rises by the deposit total, Undeposited Funds falls by the one cheque, Petty Cash falls by the cash amount, the other cheque is still awaiting deposit, and the books balance.

**Acceptance Scenarios**:

1. **Given** cheques awaiting deposit and cash in Petty Cash, **When** the user records a deposit of selected cheques and a cash amount into a bank account, **Then** that bank account rises by the deposit total, Undeposited Funds falls by the selected cheques' total, and Petty Cash falls by the cash amount.
2. **Given** a deposit was recorded, **When** the user views the bank account during reconciliation, **Then** the deposit appears as one line for its full total, matching the single line on the bank statement.
3. **Given** a deposit is being prepared, **When** the user enters a cash amount greater than the Petty Cash balance, **Then** the deposit cannot be saved and the user is told the cash available.
4. **Given** a deposit is being prepared, **When** neither a cheque nor a cash amount is included, **Then** the deposit cannot be saved.
5. **Given** a cheque was included in a saved deposit, **When** the user starts another deposit, **Then** that cheque is no longer offered.

---

### User Story 4 - Withdraw cash from the bank as a float (Priority: P2)

Before opening night, the treasurer withdraws cash from the bank so the box office and bar have change. In the app they record a cash withdrawal: the bank account it came from, the date, the amount and a purpose such as "Opening night float". The bank account falls and Petty Cash rises. After the event the float is counted back with the takings and banked through a normal deposit.

**Why this priority**: #368 asks for it, but the organisation can run the basic receive-and-deposit cycle without it.

**Independent Test**: Record a cash withdrawal from a bank account. Confirm that the bank account falls and Petty Cash rises by the amount, the books balance, and the withdrawal appears in the Banking history with its purpose.

**Acceptance Scenarios**:

1. **Given** a bank account, **When** the user records a cash withdrawal of an amount with a purpose, **Then** the bank account falls by the amount and Petty Cash rises by the same amount.
2. **Given** a withdrawal form, **When** the amount is zero or negative, or no bank account is selected, **Then** the withdrawal cannot be saved.
3. **Given** a withdrawal was recorded, **When** the user reconciles that bank account, **Then** the withdrawal appears as one line for its amount.

---

### User Story 5 - Review and reverse past deposits (Priority: P3)

The treasurer can list past deposits and withdrawals and open a deposit to see exactly what it contained: its cash amount and each cheque. If a deposit was recorded wrongly, they reverse it. The reversal undoes its effect on every account, puts its cheques back on the awaiting-deposit list and returns its cash to Petty Cash. The original deposit and its reversal both stay on record.

**Why this priority**: Mistakes are rare but must be fixable without breaking the rule that financial records are never edited or deleted.

**Independent Test**: Record a deposit, reverse it, and confirm that every affected account is back to its pre-deposit balance, the cheques are awaiting deposit again, and both the deposit and its reversal are listed.

**Acceptance Scenarios**:

1. **Given** past deposits and withdrawals, **When** the user opens the Banking history, **Then** each is listed with its date, type, bank account, total and status (recorded or reversed).
2. **Given** a recorded deposit, **When** the user opens it, **Then** its cash amount and each included cheque are shown, and their sum equals the deposit total.
3. **Given** a recorded deposit, **When** the user reverses it and confirms, **Then** the bank account, Undeposited Funds and Petty Cash return to their balances before that deposit, its cheques are offered for deposit again, and the deposit is shown as reversed.
4. **Given** a deposit that was already reversed, **When** the user views it, **Then** no reverse action is offered.
5. **Given** a deposit dated in a closed financial period, **When** the user tries to reverse it, **Then** the reversal is refused with the same message the app gives for other closed-period postings.

---

### Edge Cases

- **No bank account set up**: deposits, withdrawals and card/EFT receipts need a bank account other than Petty Cash. Without one, those screens explain that an account must be added in the chart of accounts and link to it. Cash and cheque receipts still work.
- **A single bank account**: it is preselected wherever a bank account must be chosen. With several, the user must choose.
- **Nothing awaiting deposit**: the Banking screen shows zero cheques and the Petty Cash balance, and a new deposit offers only the cash line.
- **Petty Cash is zero or negative** (for example, petty-cash expenses exceeded takings): the cash line of a deposit cannot be filled in. A negative balance is shown as negative, never hidden.
- **Cheque from a member payment that covered several fees**: it is still one cheque awaiting deposit for the full payment amount.
- **A reversal would leave Petty Cash negative** (its cash was spent after the deposit): the reversal is still allowed. Petty Cash shows a negative balance, which the user corrects in the normal way.
- **Deposit date earlier than an included cheque's receipt date**: refused. A cheque cannot be banked before it was received.
- **Restoring a backup taken before this change** (`1.2.0`, or `1.3.0` from spec 032): it restores as it does today. History is not reclassified, nothing is listed as awaiting deposit, and the old Cash on Hand balance appears as Petty Cash. A `1.3.0` backup also keeps its organisations and organisation links.
- **Cheque income from an organisation that is later archived**: it stays on the awaiting-deposit list under the organisation's name and can be deposited as normal. Archiving an organisation never blocks or changes a deposit.
- **Two deposits prepared at once, with the same cheque in both**: the second save is refused because the cheque is no longer awaiting deposit.
- **Financial records**: deposits, withdrawals and reversals are never edited or deleted. A reversal is a new record.

## Requirements *(mandatory)*

### Functional Requirements

**Receiving money**

- **FR-001**: The system MUST provide an "Undeposited Funds" asset account. It is created automatically for new and existing organisations, holds cheques and other non-cash instruments received but not yet banked, and cannot be deleted or chosen as a bank account. It MUST be a system account, like the other built-in accounts, so that screens which pick an entry's account by skipping bank and system accounts (for example, an organisation's financial history from spec 032) show the income account and never "Undeposited Funds".
- **FR-002**: The existing Cash on Hand system account MUST be renamed "Petty Cash". It MUST keep its account number, history and balance, and continue to work wherever a bank-type account can be chosen, for example paying an expense from petty cash.
- **FR-003**: When a member payment is recorded, the system MUST send it to the destination for its payment method: cash to Petty Cash; cheque or "other" to Undeposited Funds; card or electronic transfer to the bank account the user selects.
- **FR-004**: The income-entry workflow (raffles, donations, fundraising) MUST ask how the money was received (cash, cheque, card, electronic transfer or other) and route it by the same rules as FR-003. It MUST no longer offer a free choice of any bank-type account. The payment method replaces the deposit-account picker at the same place on the form. The bank-account picker (card or electronic transfer only) and the cheque reference (cheque or "other" only) appear directly after it. The optional organisation picker added by spec 032 is unchanged and stays directly above the description.
- **FR-005**: Cash fee payments taken during rehearsal attendance MUST go to Petty Cash.
- **FR-006**: The user SHOULD be able to enter an optional reference for a cheque (for example, its cheque number). When given, it MUST be shown wherever that cheque is listed.

**Banking screen**

- **FR-007**: The "Record Bank Deposit" item on the Finance menu MUST be replaced by a single "Banking" item. It opens a screen showing the cheques awaiting deposit, the Petty Cash balance, and actions to record a deposit and a cash withdrawal.
- **FR-008**: The list of cheques awaiting deposit MUST show each item's received date, payer, reference and amount, and the total of all items. The payer is the member's name for a member payment. For an income entry it is the linked organisation's name (spec 032) when one was chosen, otherwise the entry's description. An organisation archived after the cheque was received is still shown by name.

**Deposits**

- **FR-009**: The user MUST be able to record a bank deposit made up of any selection of cheques awaiting deposit plus an optional cash amount, into one bank account other than Petty Cash, on a chosen date.
- **FR-010**: A deposit MUST be refused when it includes no cheque and no cash amount, when its cash amount exceeds the Petty Cash balance, when an included cheque is no longer awaiting deposit, or when its date is earlier than any included cheque's receipt date.
- **FR-011**: While a deposit is being prepared, the system MUST show its running total split into cash and cheques.
- **FR-012**: Saving a deposit MUST, in one all-or-nothing posting, increase the bank account by the deposit total, decrease Undeposited Funds by the included cheques' total, decrease Petty Cash by the cash amount, and mark each included cheque as deposited. Total debits MUST equal total credits.
- **FR-013**: In bank reconciliation, each deposit MUST appear on the bank account as a single line for its full total.

**Cash withdrawals**

- **FR-014**: The user MUST be able to record a cash withdrawal from a bank account other than Petty Cash into Petty Cash, with a date, a positive amount and an optional purpose. It is posted as one balanced pair: bank account down, Petty Cash up.

**History and reversal**

- **FR-015**: The Banking screen MUST list past deposits and withdrawals with their date, type, bank account, total and status. Opening a deposit MUST show its cash amount and each included cheque.
- **FR-016**: The user MUST be able to reverse a recorded deposit after confirming. The reversal posts the exact opposite entries in one all-or-nothing posting, returns each included cheque to awaiting deposit, and marks the deposit as reversed. A deposit can be reversed only once.
- **FR-017**: A withdrawal recorded by mistake MUST be correctable by recording its reverse: a deposit of that cash back into the same bank account. No separate withdrawal-reversal action is required.
- **FR-018**: Deposits, withdrawals and reversals MUST respect the existing closed-financial-period rule and be refused when dated inside a closed period.

**Integrity and compatibility**

- **FR-019**: Every deposit, withdrawal and reversal MUST write an audit trail entry, as other financial postings do.
- **FR-020**: Existing financial records MUST NOT be changed or reclassified. Payments recorded before this change stay where they were posted, and their amounts are part of the Petty Cash balance.
- **FR-021**: Previously recorded "bank deposit" and historical "transfer" journal entries MUST continue to appear correctly in every existing report.
- **FR-022**: A backup MUST include the new banking records (deposits and their lines, withdrawals, reversals and each cheque's deposit status), and restoring it MUST bring them back exactly. The backup format version MUST go from `1.3.0` (spec 032) to `1.4.0`. Backups taken before this change, both `1.2.0` and `1.3.0`, MUST still restore: a `1.3.0` backup keeps its organisations and organisation links, and neither older version lists anything as awaiting deposit.
- **FR-023**: All new screen text MUST be fully translated into every shipped language, and every displayed amount MUST use the organisation's configured currency.
- **FR-024**: The development sample data MUST include at least two years of cheques, cash takings, card receipts, deposits, at least one reversed deposit and event-float withdrawals, with the books balanced. The organisation-linked income that spec 032 seeds MUST get a payment method and keep its organisation link: the council grant arrives by electronic transfer into the bank, and the bowling-club sponsorship is a cheque that waits in Undeposited Funds until a later deposit.
- **FR-025**: The organisation link from spec 032 MUST stay on the income entry that records the receipt. Deposits, withdrawals and reversals MUST NOT carry an organisation link or offer an organisation picker. An organisation's "total received" counts income on the date it was received, whatever its payment method, so a later deposit or deposit reversal MUST NOT change that organisation's financial history.

### Key Entities

- **Undeposited Funds**: a new system asset account holding non-cash instruments received but not banked. Its balance equals the total of items awaiting deposit, plus any amounts posted to it by journal entry.
- **Petty Cash** *(existing account, renamed)*: the former Cash on Hand account. It holds cash takings and float withdrawals, and is drawn down by deposits and petty-cash expenses.
- **Received item awaiting deposit**: a cheque or "other" receipt from a member payment or an income entry. It has a received date, payer (per FR-008), optional reference, amount, and a deposit status (awaiting or deposited, with the deposit it went into). Because the status changes over time, it MUST be held in its own record (or derived from the deposit lines), never as a field on the payment or journal entry it came from. Those are financial records and are never edited.
- **Bank deposit**: a dated record of money banked into one bank account. It has a cash amount, the cheques it included, a total, a status (recorded or reversed) and, once reversed, a link to its reversal. Like its reversal, it carries no organisation link (FR-025).
- **Cash withdrawal**: a dated record of cash taken from a bank account into Petty Cash, with an amount and a purpose. It carries no organisation link.
- **Member payment** *(existing `Payment` entity)*, **income entry** *(existing; not an entity of its own but a `JournalEntry` of type Income, which since spec 032 may carry an organisation link)*: gain a payment method and a cheque reference. Both are set once, when the entry is recorded, and never changed. The payment method decides where the money is posted.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After recording a cheque, the Banking screen lists it as awaiting deposit, and every bank account balance is unchanged (pass/fail).
- **SC-002**: A deposit of N cheques plus cash moves exactly the deposit total into the bank, with Undeposited Funds and Petty Cash falling by their parts. The trial balance stays balanced after every deposit, withdrawal and reversal (pass/fail).
- **SC-003**: In bank reconciliation, a deposit made from 10 separate receipts shows as 1 line, not 10.
- **SC-004**: A treasurer can record a deposit of 5 cheques plus cash in under 1 minute from opening the Banking screen.
- **SC-005**: Reversing a deposit returns all three affected account balances to their exact pre-deposit values, and 100% of its cheques reappear as awaiting deposit.
- **SC-006**: A backup taken before this change (`1.2.0` or `1.3.0`) restores with no error, and every report total and every organisation's financial history total is the same before and after the restore.

## Non-goals

- Dishonoured (bounced) cheques. A cheque that bounces after it was banked is corrected with a journal entry.
- Card-terminal settlement batches or bank fees deducted from them.
- Printing a bank deposit slip.
- Linking a cash withdrawal or float to a specific event.
- Counting a till by denomination (notes and coins).
- Importing bank statements.
- Reclassifying historical payments that were posted to Cash on Hand before this change.
- A separate float account, apart from Petty Cash.

## Change-level rules

- Financial records stay immutable and are never deleted. Every correction is a new, reversing posting.
- Every posting is balanced and all-or-nothing, as it is today.
- Account numbers already in use are not renumbered. Petty Cash keeps the Cash on Hand number.
- No new third-party dependency.
- The backup format can only grow. Existing fields keep their meaning, and the format version is raised once for this change, to `1.4.0`. Field numbers are append-only. Envelope members 27 and 28 and journal-entry field 6 now belong to spec 032, and member 30 is `EntityCounts`. So new envelope lists take member 29 first, then 31 onwards, and any new journal-entry field starts at 7.

## Verification plan

1. Automated tests cover every routing rule (each payment method, from each entry point), each deposit and withdrawal rule, reversal, closed periods, and the backup round trip including restoring an older backup.
2. A trial-balance check after each posting type confirms debits equal credits.
3. In the running app with the sample data: record two cheques and a cash payment, deposit one cheque plus part of the cash, then check the Banking screen, the account balances and the reconciliation view. Reverse the deposit and check that everything is restored.
4. Record a float withdrawal, then deposit the takings plus the float, and check that Petty Cash ends where expected.
5. Switch the display language and check that every new screen is translated and amounts use the configured currency.

## Assumptions

- "Other" payment method receipts (for example, a money order) are physical instruments that must be banked, so they wait in Undeposited Funds like cheques.
- Petty Cash may go negative (for example, after expenses are paid from it). The system shows this rather than blocking it, except that a deposit can never take more cash than the balance.
- A withdrawal is corrected by depositing the cash back (FR-017), so it needs no reversal action of its own.
- "Banking" replaces "Record Bank Deposit" at the same position in the Finance menu. The old screen address redirects to the new screen so that bookmarks and links keep working.
- The Undeposited Funds account takes a free number in the current-asset range next to Petty Cash, without disturbing any account the organisation created itself.
- This spec supersedes spec 009's FR-001 to FR-003 and FR-006 (deposits always sourced only from Cash on Hand). Spec 009's other requirements still hold.
- Spec 032 (External Organisations) merged first and took backup version `1.3.0`, so this spec takes `1.4.0` and appends its fields after 032's (see Change-level rules).
- The app is not yet released (#368 must ship before initial release), so no production data has to be migrated beyond the account rename and the new account.

## Documentation this change updates

Implementing this spec makes the following text in other specs out of date. Each is rewritten in the same change as the code, per the project's docs workflow. Until then, the spec 032 and spec 030 docs below carry a short "pending spec 033" note.

- `specs/032-external-organisations/spec.md` FR-012 and SC-003: "bank-side movement" becomes the money-side line (bank, Petty Cash or Undeposited Funds), and totals count income when received (FR-025).
- `specs/032-external-organisations/data-model.md` §2 "Who may set the link": add the new deposit, deposit-reversal and cash-withdrawal entry types as "no, always `null`". §3 "Why Σ debits is correct": an income entry debits Petty Cash, Undeposited Funds or the chosen bank account, not "the deposit account".
- `specs/032-external-organisations/contracts/ui-contract.md` §5.3: the income form's payment-method layout (FR-004), and "Bank deposit" renamed to the Banking screen.
- `specs/030-backup-restore/contracts/backup-envelope-schema.md`: a `1.4.0` note listing this spec's new members.
- `specs/009-bank-deposit-transfers/spec.md`: FR-001 to FR-003 and FR-006 marked as superseded by this spec.
- `CLAUDE.md`: the entity count and list, the backup version, the "Finance / GL integrity" account notes (Petty Cash, Undeposited Funds), and the spec 032 data-model line about which journal entries carry an organisation link.
