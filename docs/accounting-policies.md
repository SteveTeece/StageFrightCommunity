# Accounting Policies

This document states the accounting policies StageFright Community applies when it keeps an
organisation's books. It is the reference FR-027 of
[`specs/028-international-accounting-standards`](../specs/028-international-accounting-standards/spec.md)
requires: a single place a treasurer, an auditor, or a contributor can read to understand how the
software recognises, measures, rounds, stores, and protects financial data.

The benchmark throughout is **universal double-entry and bookkeeping good practice**, not a statutory
financial-reporting framework. See [Status of the reports](#status-of-the-reports) below.

---

## Basis of accounting

The books are kept on a **hybrid basis**:

- **Member fees** (annual subscriptions and per-rehearsal / per-event attendance fees) are recognised
  **when they are levied** — the accrual basis. Levying a fee posts the fee record and its ledger
  accrual immediately, whether or not it has been paid.
- **All other income and expenditure** — manual income entries, expense payments, bank deposits — is
  recognised **when the money is received or paid** — the cash basis.

Every financial statement the application produces carries this basis-of-accounting statement on its
face (screen, PDF, and CSV), worded so a reader who did not build the system can rely on it.

## Revenue recognition

- **Annual fees** are accrued once per member per calendar year. Applying the annual-fee batch posts,
  for each eligible member, a debit to Member Receivable (gross) and a credit to an Income account
  (net of any sales tax), plus a Tax Collected credit when the fee is taxable while sales tax applies.
- **Attendance fees** are accrued the same way when attendance is recorded; if the fee is marked paid
  at the point of recording, the automatic payment is posted in the same transaction.
- **Outstanding fees** sit in Member Receivable (an asset) until settled. A member's balance is always
  computed from the ledger (Σ debits − Σ credits on that member's receivable), never from a stored
  "paid" flag.
- **Other income** is recognised when banked: recording income debits a bank/cash account and credits
  a non-system Income account (and Tax Collected when taxable).
- **Payments** allocate against a member's outstanding fees oldest-first (FIFO) by default, or against
  an explicitly chosen subset. An amount left over after all selected fees are settled is held as an
  overpayment credit on the member's account.

## Rounding

- Every monetary amount is stored and presented to the **configured currency's minor unit** — 0, 2,
  or 3 fractional digits, per ISO 4217 (for example 0 for Japanese yen, 2 for most currencies, 3 for
  Kuwaiti dinar).
- Sales tax on a tax-inclusive amount is split so the parts **re-sum to the gross exactly**: the tax
  component is `round(gross × rate ÷ (100 + rate))` to the currency's minor unit (away from zero), and
  the net component is the remainder. This holds at 0, 2, and 3 minor digits alike.
- No stored amount carries more precision than the currency's minor unit.

## Currency

- An organisation keeps its books in **one currency**, identified by its ISO 4217 code, chosen during
  first-run setup. The default is `AUD`.
- The currency is **fixed for the life of the dataset**: it cannot be changed in place, there is no
  support for multiple simultaneous currencies, and there is no foreign-exchange translation.
- The currency **symbol** and **minor-unit precision** come from the configured currency. Digit
  **grouping** and **symbol placement** follow the operating system's active regional format, so a
  French- or German-region device groups and places the symbol its own way while still showing the
  organisation's currency.
- Amounts are entered independently of the device region: a typed value is read with the invariant
  decimal point, so "one and a half" stores exactly `1.5` whether the region uses a comma or a period
  as its decimal separator.

## Record immutability and corrections

- The **General Ledger is append-only** and is the single source of truth for every balance shown
  anywhere in the application.
- `Fee`, `Payment`, `Transaction`, and `JournalEntry` records are **immutable once written and are
  never deleted**. `Payment` allows only its `Notes` field to change.
- Every posting is a **balanced, atomic transaction**: the fee/payment/journal row and its ledger
  debit/credit lines are written together inside one database transaction, and the lines must sum to
  zero (Σ debits = Σ credits) or the whole operation is rejected and rolled back.
- A correction to a previously posted amount is made **only by posting new, offsetting ledger lines**
  — a reversing entry or a write-off — never by editing or deleting the original record.

## Period locking

- Once a reporting period's accounts have been presented, the treasurer can mark **all periods up to
  and including a chosen date as closed**.
- After that, any financial transaction dated **on or before** the closed-through date is rejected,
  and **no partial record is left** — no business row and no ledger line.
- Opening balances entered during first-run setup are always accepted: setup completes before any
  period can be closed.

## Financial year

- The financial year is a **start month plus a start day**, running twelve months. It is an explicit
  choice at first-run setup (default 1 July — day 1 of month 7).
- A start on a day other than the first of the month is supported. Every financial-year-preset report
  and dashboard figure honours the configured month **and** day.
- 52/53-week ("4-4-5") fiscal calendars are out of scope. A first financial year shorter than twelve
  months, labelled as a part-year, is not yet supported and is tracked as a follow-up.

## Audit-trail retention

- **Every financial posting path** — including attendance-fee accruals and their automatic payments —
  writes an audit-trail entry, so the history of who changed what is independently reconstructable.
- Audit-trail entries are retained for a **configurable period of 1 to 7 years**, defaulting to
  **5 years** on a new dataset. An existing dataset keeps whatever retention value it was already
  configured with.
- Entries older than the retention window are purged at application startup. A purge failure is
  **logged and surfaced** to the user, never silently discarded.

## Status of the reports

The financial statements this application produces — Income Statement, Balance Sheet, Trial Balance,
Bank Reconciliation, Tax Summary, Account Register, General Ledger, Member Account Summary, and the
rest — are **unaudited management accounts**. They are prepared to universal double-entry and
bookkeeping good practice, not to IFRS, GAAP, or any local statutory financial-reporting framework.
Producing audited or statutory financial statements remains the organisation's own accountant's
responsibility.
