# Feature Specification: Finance Module Expansion — Full Accounting, Bank Reconciliation, GST/BAS

**Feature Branch**: `ExpandFnance`
**Created**: 2026-07-05
**Status**: Approved (derived from plan.md; scope decisions user-confirmed)
**Input**: Expand the finance module from member-fee bookkeeping into a complete small-NFP accounting system compliant with Australian conventions.

## Overview

StageFright's finance module today handles only member fees, member payments, simple non-member income, and bad-debt forgiveness, with an implicit chart of accounts (three hard-coded system categories plus auto-numbered user income/expense categories). This expansion delivers: a full chart of accounts (asset/liability/equity/income/expense, multiple user-defined bank & cash accounts), expense/transfer/journal/opening-balance workflows, per-account manual bank reconciliation (CSV-import ready), configurable GST with per-fee-type treatment and a BAS summary, and the reports needed for AU annual/tax reporting (Balance Sheet, Income & Expenditure with FY presets, General Ledger detail, reconciliation report, BAS).

## User Scenarios & Testing

### User Story 1 — Full chart of accounts (Priority: P1)

As a treasurer, I manage a complete chart of accounts (Assets 1000s, Liabilities 2000s, Equity 3000s, Income 4000s, Expenses 6000s) including multiple user-defined bank/cash accounts, from a single Chart of Accounts page, with all existing finance behaviour and reports intact.

**Why this priority**: Every other story depends on the Account entity, AU numbering, and AccountId-based aggregation. Existing reports must keep working after renumbering.

**Independent test**: Migrate an existing database; verify member balances, trial balance totals, and existing reports match pre-migration values; create accounts of each type incl. a second bank account and verify numbering (bank accounts from 1110); verify Finance sub-menu renders; Settings → Categories tab is gone and Chart of Accounts page is the single management surface.

**Acceptance scenarios**:
1. **Given** a pre-expansion database, **When** the app starts and auto-migrates, **Then** Cash becomes 1100 (Asset, bank), Member Receivable 1200 (Asset), Bad Debt 6999 (Expense), user income categories renumber 1000+n→4000+n, user expense 2000+n→6000+n, and new system accounts 2310/2320/3100/3200 exist; historical Transaction rows are untouched.
2. **Given** the Chart of Accounts page, **When** I add an Asset account flagged as bank, **Then** it is numbered 1110+ and appears in bank-account pickers.
3. **Given** any report (Trial Balance, Income Statement, Account Register, Member Account Summary), **When** run post-migration, **Then** totals equal pre-migration values (aggregation by AccountId, not account-number strings).

### User Story 2 — Money-out workflows & posting engine (Priority: P2)

As a treasurer, I record expense payments, transfers between bank accounts, general journals, and one-off opening balances, each posting balanced multi-line GL sets grouped under an immutable JournalEntry header.

**Independent test**: Record an expense (DR Expense/CR Bank), a transfer (DR To/CR From), a balanced multi-line journal, and run the opening-balances wizard; Trial Balance remains balanced; imbalanced sets are rejected and rolled back.

**Acceptance scenarios**:
1. **Given** a bank and expense account, **When** I record an expense payment, **Then** a JournalEntry with DR Expense/CR Bank is committed atomically and audit-logged.
2. **Given** a journal with Σdebits ≠ Σcredits, **When** I try to save, **Then** the UI blocks it and the service throws GLBalanceException with full rollback.
3. **Given** the opening-balances wizard, **When** I enter balances as at FY start, **Then** one line per account posts at its normal side with the residual plugged to 3100 Opening Balance Equity; Member Receivable and GST accounts are excluded.
4. **Given** a general journal, **When** a line targets Member Receivable 1200, **Then** it is blocked (protects per-member balance integrity).

### User Story 3 — Bank reconciliation (Priority: P3)

As a treasurer, I reconcile each bank account to its statement by ticking off transactions, finalising only when the difference is $0.00; finalised reconciliations are immutable.

**Independent test**: Start a draft reconciliation, tick transactions until difference ≤ $0.005, finalise, print the reconciliation report; verify a finalised rec cannot be edited or deleted, drafts can be deleted, one draft per account, and a transaction is cleared by at most one reconciliation.

**Acceptance scenarios**:
1. **Given** a bank account with unreconciled transactions, **When** I start a reconciliation, **Then** opening balance snapshots the previous finalised rec's closing balance (else 0) and unreconciled transactions are listed.
2. **Given** a draft with |difference| > 0.005, **When** I try to finalise, **Then** it is blocked; at ≤ 0.005 finalise succeeds and the rec becomes immutable (ReconciliationException on mutation).
3. **Given** a finalised reconciliation, **When** I run the Bank Reconciliation report, **Then** it shows cleared payments/deposits, unpresented items ≤ statement date, and a $0.00 difference footer.

### User Story 4 — GST & BAS (Priority: P4)

As a treasurer of a GST-registered NFP, I toggle GST on, choose per-fee-type GST treatment, enter GST-inclusive amounts that split net/GST to clearing accounts (2310 Collected / 2320 Paid), and produce a BAS summary. With GST off, everything behaves exactly as before.

**Independent test**: Toggle GST on; post $110 income and a $110 expense with code Gst; BAS shows 1A=$10, 1B=$10, 9=$0; toggle off and verify postings are byte-identical to pre-GST behaviour and GST UI is hidden.

**Acceptance scenarios**:
1. **Given** GST registered, **When** I record $110 income coded Gst, **Then** GL posts DR Bank 110 / CR Income 100 / CR GST Collected 10 (rounding AwayFromZero on gross/11).
2. **Given** a taxable annual fee, **When** it accrues, **Then** Fee.GstCode is stamped and GL posts DR Member Receivable 110 / CR Income 100 / CR GST Collected 10; forgiveness posts the GST decreasing adjustment (DR Bad Debt 100 / DR GST Collected 10 / CR Member Receivable 110).
3. **Given** GST not registered, **When** I use any finance UI, **Then** no GST controls appear, postings are 2-line, and GstCode stays null.
4. **Given** a quarter of activity, **When** I run the BAS Summary report, **Then** G1/G3/G11 and 1A/1B/9 are computed from account movements on an accruals basis (stated in header).

### User Story 5 — AU statements & reports (Priority: P5)

As a treasurer, I produce a Statement of Financial Position (balance sheet), a Statement of Income & Expenditure with financial-year presets and prior-year comparison, and a General Ledger detail report, aligned to a configurable FY start month (default July).

**Independent test**: On a fixture ledger (incl. legacy pre-migration rows), Balance Sheet balances (Assets = Liabilities + Equity incl. computed Accumulated Surplus); Income & Expenditure honours FY presets and comparison column; GL report shows opening balance, running balance, closing balance per account.

**Acceptance scenarios**:
1. **Given** any as-at date, **When** I run the Balance Sheet, **Then** Assets/Liabilities/Equity sections derive from inception-to-date AccountId balances, Equity includes computed Accumulated Surplus, and the footer asserts A = L + E.
2. **Given** FY start month July, **When** I pick "Last FY", **Then** the Income & Expenditure covers 1 Jul–30 Jun of the prior FY with surplus/(deficit) total; a non-default start month shifts boundaries accordingly.
3. **Given** an account with activity, **When** I run the General Ledger report for a range, **Then** its section shows opening balance, dated lines with running balance, and closing balance.

## Requirements

### Functional
- **FR-101**: `Category` is renamed/evolved to `Account` with `AccountType { Income, Expense, Asset, Liability, Equity }`, `AccountNumber`, `IsBankAccount` (Asset-only); system accounts are immutable; archive blocked while referenced.
- **FR-102**: AU numbering ranges (1000s/2000s/3000s/4000s/6000s); next number = max-in-range + 1 including archived accounts; user bank accounts from 1110.
- **FR-103**: All aggregation keys on `AccountId`; historical `Transaction.GLAccount` strings are posting-time snapshots and are never updated.
- **FR-104**: `SystemAccounts` static class is the single source of well-known account GUIDs/numbers.
- **FR-105**: Immutable `JournalEntry` header (`Income, ExpensePayment, Transfer, GeneralJournal, OpeningBalance`); `Transaction.JournalEntryId` nullable FK; `IGLRepository.AddBalancedSetAsync` enforces ≥2 lines, one non-zero side each, Σdebits = Σcredits else `GLBalanceException`; `AddPairAsync` delegates to it.
- **FR-106**: Expense payment, account transfer (both ends IsBankAccount, from≠to), general journal (Member Receivable blocked), opening balances (plug to 3100; excludes 1200 and GST accounts; warns on rerun) — all inside `IUnitOfWork.ExecuteInTransactionAsync`, audit-logged, `ValidationException` on bad input.
- **FR-107**: `BankReconciliation` (Draft/Finalised; finalised immutable via `ReconciliationException`; drafts soft-deletable; one draft per account) + `ReconciliationLine` (unique per rec+transaction; a transaction cleared by at most one non-deleted rec). Difference = StatementClosingBalance − (OpeningBalance + Σcleared debits − Σcleared credits); finalise gated |diff| ≤ 0.005. Data model accommodates future CSV statement import without rework.
- **FR-108**: GST: `GstCode { Gst, GstFree, InputTaxed, BasExcluded }` nullable on Transaction and Fee; statutory rate constants (0.10 / divisor 11); `GstCalculator.SplitInclusive` rounds AwayFromZero; Settings `IsGstRegistered`, `AnnualFeeGstCode`, `AttendanceFeeGstCode` (default GstFree); accrual-basis recognition; payment FIFO allocation unchanged.
- **FR-109**: Reports: Bank Reconciliation, BAS Summary, Balance Sheet ("Statement of Financial Position"), Income Statement retitled "Statement of Income & Expenditure" with FY presets + comparison, General Ledger — all via the existing `IReportProvider` → `ReportData` pipeline.
- **FR-110**: Settings gains `FinancialYearStartMonth` (default 7); Settings → Categories tab is retired; `/finance/accounts` is the single CoA surface; `ShellLayout` renders `MenuItem.SubItems` as expandable groups with auto-expand on active child route.

### Non-functional / constraints (CLAUDE.md non-negotiables)
- Financial records (`Fee`, `Payment`, `Transaction`, `JournalEntry`) immutable/append-only; corrections via reversing GL pairs; zero UPDATEs to Transactions data in migrations.
- One class per file; `.razor` + `.razor.cs` pairs, no `@code` blocks; no custom JS; custom exceptions at boundaries; exhaustive `Should_X_When_Y` tests; each phase ends with green `dotnet build` + full `dotnet test`; existing integration scenarios V4–V12 pass unmodified.

## Success Criteria

- **SC-001**: Post-migration, member balances, trial balance totals, and all existing reports match pre-migration values exactly.
- **SC-002**: Trial Balance remains balanced after any combination of new workflows.
- **SC-003**: A reconciliation can be taken to $0.00 difference and finalised; finalised recs are immutable.
- **SC-004**: With GST on, $110 income + $110 expense yields BAS 1A=$10, 1B=$10; with GST off, postings are byte-identical to pre-GST behaviour.
- **SC-005**: Balance Sheet balances and cross-foots to the Income & Expenditure surplus on fixture and legacy data.
- **SC-006**: All five test projects green; V4–V12 unmodified and passing.
