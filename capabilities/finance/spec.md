# Finance — Living Spec

## Purpose

The Finance capability is the club's double-entry accounting system: it accrues member fees, records payments and expenses, and derives every balance shown anywhere in the app from a single append-only General Ledger. Without it, money movements would have no audit-proof source of truth, member balances could drift from what was actually collected, and financial corrections would silently rewrite history instead of leaving a trail.

## Requirements

### Every posting is a balanced, atomic GL transaction
Every financial mutation (fee accrual, payment, expense, income entry, bank deposit, manual journal, opening balance, reactivation write-off) MUST post its GL debit/credit lines together with any related record (Fee, Payment, JournalEntry) inside a single `DbContext` transaction, and the set of lines MUST sum to zero (Σdebits = Σcredits) or the whole operation is rejected and rolled back.

#### Scenario: a member payment is recorded
- **WHEN** a payment is submitted for a member
- **THEN** the Payment record and its Cash/Member-Receivable GL pair are all persisted together
- **AND** if the GL pair would not balance, neither the Payment nor any GL line is persisted

#### Scenario: an annual fee batch is applied to several members
- **WHEN** annual fees are applied to a list of eligible members
- **THEN** every member's Fee and GL accrual pair is posted inside the same transaction as every other member's
- **AND** a failure partway through leaves no member's fee or GL lines committed

### The General Ledger is the sole source of truth for balances
No fee or payment record carries a persisted "paid" flag that other code trusts; a member's outstanding balance, an account's balance, and the organisation's income/expense figures MUST all be computed by summing GL transactions at query time rather than from any cached or denormalized status field.

#### Scenario: a member's outstanding balance is requested
- **WHEN** the UI needs to know how much a member owes
- **THEN** the figure is computed as Σdebits − Σcredits on that member's Member Receivable GL transactions, not read from a stored balance

### Financial records are immutable; corrections are reversing entries
Fee and Transaction/JournalEntry records MUST never be updated or deleted after creation, and Payment records MUST only allow their Notes field to change. Any correction to a previously posted amount MUST be expressed as new GL lines (a reversal or write-off), never as a mutation of the original record.

#### Scenario: a member's stale fee is forgiven
- **WHEN** a prior fee is written off via reactivation forgiveness
- **THEN** the original Fee record is left completely unchanged
- **AND** a new GL write-off pair is posted to clear the receivable

### Annual fee batch application is scoped to once-per-member-per-year and posts a full accrual pair
Applying annual fees MUST exclude any active member who already has an Annual fee (paid or unpaid) for the current calendar year, and for each remaining member it MUST post Debit Member Receivable (gross) / Credit an Income account (net of any sales tax) with a Tax Collected leg added only when the fee is taxable (`Fee.TaxCode = Taxable`) while sales tax applies to the organisation (`Settings.IsTaxApplicable`).

#### Scenario: annual fees are applied twice in the same year
- **WHEN** the batch is run a second time after members already received this year's fee
- **THEN** those members no longer appear in the eligible list and are not charged again

#### Scenario: sales tax applies and the annual fee is taxable
- **WHEN** annual fees are applied with `Settings.IsTaxApplicable` true and `Settings.AnnualFeeTaxCode = Taxable`
- **THEN** each fee's GL accrual splits into a Member Receivable debit (gross), an Income credit (net), and a Tax Collected (account 2310) credit, summing back to the gross fee amount [NEEDS CLARIFICATION: annual fee income always posts to the first non-system Income account by account-number order rather than a specifically configured account — is that the intended long-term behavior once multiple income accounts exist?]

### Member payments allocate against outstanding fees, defaulting to oldest-first
Recording a payment MUST allocate the tendered amount across the member's outstanding fees — FIFO across all unpaid fees by default, or restricted to an explicitly selected subset of fees — and MUST reject an amount that exceeds what is actually owed on the fees being paid against. Any amount left over once no more fee IDs were explicitly selected becomes an overpayment credited to the member's receivable rather than being rejected.

#### Scenario: a payment is submitted for more than the selected fees' remaining total
- **WHEN** the entered amount exceeds the sum of the remaining amounts on the explicitly selected fees
- **THEN** the payment is rejected with a validation error and nothing is posted

#### Scenario: a payment is submitted with no specific fees selected and exceeds the total balance
- **WHEN** the legacy FIFO-across-all-fees path is used and the amount is more than everything owed
- **THEN** every outstanding fee is fully allocated and the remainder posts as an overpayment credit on the member's account

### Every account has a stable, type-scoped sequential number that is never reused
Creating a new account MUST assign it the next number in the range determined by its type and bank flag (e.g. bank Assets start at 1110, Income at 4000), computed as one past the highest number already used in that range — including archived accounts, so a number is never handed out twice. System accounts MUST reject rename and archive attempts outright.

#### Scenario: a second bank account is created after the first was archived
- **WHEN** a new bank/cash account is added
- **THEN** its number is one past the highest bank-range number in use, whether or not the account holding that number is currently archived

#### Scenario: an attempt is made to rename a system account
- **WHEN** UpdateAsync is called against a seeded system account
- **THEN** the request is rejected and the account is left unchanged

### Account archival is blocked when it would break referential integrity
Archiving an account MUST be refused if any GL transaction references it, and archiving a bank/cash account MUST additionally be refused while it has a draft (unfinalised) reconciliation in progress.

#### Scenario: an account with posted transactions is archived
- **WHEN** an account that GL transactions reference is archived
- **THEN** the request is rejected and the account remains active

#### Scenario: a bank account with an open reconciliation draft is archived
- **WHEN** the account has a draft reconciliation that has not been finalised or deleted
- **THEN** the archive request is rejected

### A tax treatment is only carried on a posting while sales tax applies, and is fixed for that posting's lifetime
A fee, income, or expense entry MUST only receive a `TaxCode` of `Taxable` when `Settings.IsTaxApplicable` is true at the moment it is posted; the amount entered is always treated as tax-inclusive and split into net and tax components at the configured `Settings.TaxRate` percentage (tax = `round(gross × rate ÷ (100 + rate))` to the configured currency's minor unit, net = the remainder). The three treatments are `Taxable`, `TaxExempt`, and `Excluded` (transfers, journals, opening balances). Once posted, a row's `TaxCode` MUST never be revisited even if the organisation's tax settings later change.

#### Scenario: sales tax does not apply to the organisation
- **WHEN** an expense is recorded while `Settings.IsTaxApplicable` is false
- **THEN** the posted transaction carries no tax component and its `TaxCode` is not `Taxable`, regardless of what was requested on the form

#### Scenario: a taxable entry is posted while sales tax applies
- **WHEN** an income entry is recorded with the `Taxable` tax code while `Settings.IsTaxApplicable` is true
- **THEN** the net and tax components are computed by rounding `gross × rate ÷ (100 + rate)` to the configured currency's minor unit and the net component is the remainder, so the two always sum exactly back to the entered gross amount

### Non-member income and expenses always move through a designated bank/cash account
Recording income requires a bank/cash account as the deposit destination (Cash on Hand by default) and a non-system Income account to credit; recording an expense requires a bank/cash account to pay from and a non-system Expense account to debit. A selection that is a system account or the wrong account type MUST be rejected.

#### Scenario: a system account is selected as the income category
- **WHEN** the chosen account for a manual income entry is a system account
- **THEN** the request is rejected before any GL lines are written

### Bank deposits move cash from hand to a bank account without going through Income
A bank deposit MUST post Debit destination bank account / Credit Cash on Hand only, and the destination account MUST be a bank account other than Cash on Hand itself.

#### Scenario: Cash on Hand is chosen as its own deposit destination
- **WHEN** a bank deposit's destination account is Cash on Hand
- **THEN** the request is rejected

### Manual journals must self-balance and cannot touch per-member balances
A manually entered general journal MUST contain at least two lines, exactly one non-zero side (debit or credit) per line, and Σdebits equal to Σcredits; no line may post to the Member Receivable account, since per-member balances may only move through the fee and payment workflows.

#### Scenario: a journal line targets Member Receivable
- **WHEN** a manual journal includes a line against the Member Receivable account
- **THEN** the entire journal is rejected and nothing is posted

#### Scenario: a journal's debits and credits don't match
- **WHEN** the submitted lines don't sum to zero
- **THEN** the journal is rejected with a validation error before any GL write is attempted

### Opening balances post once per account at its normal side, self-balanced by an equity plug
The opening balances wizard MUST post each entered non-zero balance to its account's normal debit/credit side (debit for Asset/Expense, credit for Liability/Equity/Income, flipped for a negative entry) and MUST post any resulting residual to Opening Balance Equity so the entry always balances on its own. Only Opening Balance Equity itself is excluded as an entry target — it is the residual plug, not an enterable row; every other account, including Member Receivable and the tax clearing accounts (Tax Collected 2310, Tax Receivable 2320), is an eligible target so a coordinator migrating from another system can seed real carried-over balances for them.

#### Scenario: entered opening balances don't net to zero
- **WHEN** the sum of entered debit-side and credit-side balances differ
- **THEN** the difference is posted to Opening Balance Equity so the journal entry balances

#### Scenario: opening balances are posted a second time
- **WHEN** an OpeningBalance journal entry already exists and the wizard is used again
- **THEN** the user is warned that posting again adds to the existing balances rather than replacing them

### Bank reconciliation is a tick-off workflow against GL history, never a GL mutation
Reconciling a bank account MUST only record which existing GL transactions are "cleared" via join rows on the reconciliation; ticking or unticking a transaction MUST never alter the underlying Transaction row. Finalisation is only permitted once the statement closing balance minus (opening balance + cleared total) is within a small tolerance, and a finalised reconciliation MUST become permanently unmodifiable.

#### Scenario: a reconciliation is finalised with a remaining difference
- **WHEN** the computed difference exceeds the finalisation tolerance
- **THEN** finalisation is rejected and the reconciliation stays in draft

#### Scenario: a cleared line is toggled after finalisation
- **WHEN** ToggleClearAsync is called against a reconciliation that is already finalised
- **THEN** the request is rejected and no line changes

### Only one draft reconciliation may be open per account, in statement-date order
Starting a new draft reconciliation for an account MUST be refused while that account already has an unfinalised draft, and the new draft's statement date MUST be after the account's most recently finalised statement date.

#### Scenario: a second draft is started while one is already open
- **WHEN** an account already has a draft reconciliation in progress
- **THEN** starting another draft for the same account is rejected

### Reactivation forgiveness writes off fees without ever mutating the Fee record
Forgiving a member's outstanding fee MUST post Debit Bad Debt Expense / Credit Member Receivable for the fee's full amount, with a tax decreasing-adjustment leg added when the fee was taxable (`Fee.TaxCode = Taxable`), and MUST leave the underlying Fee row exactly as it was created.

#### Scenario: a taxable prior-year fee is forgiven
- **WHEN** a fee with a `Taxable` `TaxCode` is selected for forgiveness
- **THEN** the write-off splits into a Bad Debt Expense debit and a Tax Collected (account 2310) debit alongside the Member Receivable credit, so the entry still balances and reverses the tax originally accrued

### Organisation-level financial summaries never double-count the receivable leg of a posting
Dashboard and summary figures for income, expenses, and cash flow MUST be computed strictly from non-system Income/Expense account movements (credits minus debits for income, debits minus credits for expenses) rather than by summing every GL credit or debit indiscriminately, which would double count the Member Receivable leg of a fee or payment pair.

#### Scenario: an annual fee is accrued
- **WHEN** a fee accrual posts a Member Receivable debit and an Income credit
- **THEN** only the Income credit contributes to the organisation's income figure — the Receivable debit is excluded from the summary entirely

### Every finance-mutating action writes an audit trail entry
Every account, fee, payment, journal, deposit, reconciliation, and forgiveness action that changes financial state MUST write an AuditTrailEntry describing the change, so the history of who changed what is independently reconstructable from the audit log.

#### Scenario: an account is archived
- **WHEN** ArchiveAsync succeeds for a user-created account
- **THEN** an audit entry is recorded showing the account's prior name and a Delete action

### Per-account balance failures are isolated so one bad account never breaks the page
When the GL balance calculation fails for a single account, the Chart of Accounts view MUST still render every other account's balance normally; only the failing account's row is marked as errored, with no balance value shown.

#### Scenario: one account's balance calculation throws an exception
- **WHEN** GetActiveAccountBalancesAsync computes balances for all active accounts and one account's lookup fails
- **THEN** that account's row shows an error indicator instead of a balance, and every sibling row still shows its correct balance

## Uncovered

_None — every file in the area was read._

### The Chart of Accounts can be printed as a PDF, grouped by type, with an optional current-balances column

#### Scenario: Printing without balances
- **WHEN** a user on the Chart of Accounts screen clicks "Print Chart of Accounts" with the "Include Current Balances" option off
- **THEN** a PDF opens listing every active account under fixed Assets/Liabilities/Equity/Income/Expenses headings, ordered by account number within each section, showing only account number and name (with a plain-text "(System)"/"(Bank)" indicator where applicable), no balance figures, and no combined grand-total row

#### Scenario: Printing with balances
- **WHEN** the "Include Current Balances" option is on at print time
- **THEN** the same PDF additionally shows each account's current balance (matching the figure the Chart of Accounts screen shows for that account), with a per-account "Error" indicator in place of any balance that could not be calculated, and archived accounts never appear

#### Scenario: Also available from the Reports menu
- **WHEN** a user opens the central Reports menu
- **THEN** "Chart of Accounts" is listed under the Finance section, generates the same grouped/ordered report with the same include-balances option, and exports to CSV matching what was shown on screen
