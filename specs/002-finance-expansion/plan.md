# Finance Module Expansion — Full Accounting, Bank Reconciliation, GST/BAS

> **Status:** Implemented (all tasks T001–T069 complete as of 2026-07-07; 1008 tests green across 5 test projects).
> **Branch:** intended for `ExpandFnance`.

## Context

StageFright's finance module today handles only member fees, member payments, simple non-member income, and bad-debt forgiveness. The "chart of accounts" is implicit: three hard-coded system categories (Cash "0100", Member Receivable "0101", Bad Debt "9900") plus auto-numbered user income/expense categories, all stored as the overloaded `Category` entity with string `GLAccount` codes denormalized onto immutable GL `Transaction` rows. There is no Account entity, no way to record an expense payment, transfer between accounts, post a manual journal, reconcile a bank statement, or produce a balance sheet — and no GST support.

This expansion turns the module into a complete small-NFP accounting system compliant with Australian conventions: a full chart of accounts (asset/liability/equity/income/expense, multiple user-defined bank & cash accounts), expense/transfer/journal/opening-balance workflows, per-account manual bank reconciliation (CSV-import ready), configurable GST with per-fee-type treatment and a BAS summary, and the reports needed for AU annual/tax reporting (Balance Sheet, Income & Expenditure with FY presets, GL detail, reconciliation report, BAS).

**User-confirmed scope decisions:**
- GST is **configurable** via a Settings toggle (`IsGstRegistered`); GST treatment of member fees is **configurable per fee type** (annual / attendance each get a GST code setting).
- Bank reconciliation is **manual tick-off** now; data model must accommodate CSV statement import later without rework.
- **Multiple user-defined bank/cash accounts**; income/expenses/transfers pick the account; reconciliation is per account.
- All four workflows: **expense payments, account transfers, general journals, opening balances wizard**.
- The Settings → Categories tab is **retired**; the new Chart of Accounts page is the single management surface.
- Financial year configurable via Settings month dropdown, default July (1 Jul–30 Jun).

**Non-negotiables that shape everything** (CLAUDE.md): financial records (`Fee`, `Payment`, `Transaction`) are immutable/append-only, corrections via reversing GL pairs; one class per file; `.razor` + `.razor.cs` pairs, no `@code` blocks; no custom JS; custom exceptions at boundaries; exhaustive `Should_X_When_Y` tests; run `dotnet build` + full `dotnet test` per phase.

---

## Core design decisions

### 1. `Category` becomes `Account` (rename/evolve, not a parallel entity)

`Category` already *is* the chart of accounts in disguise (owns the account number; `Transaction.CategoryId` is a required FK on every GL row). Rename across the codebase:

- `Category` → `Account` (`src/StageFright.Core/Entities/Category.cs`), `CategoryType` → `AccountType`, `ICategoryRepository`/`CategoryRepository` → `IAccountRepository`/`AccountRepository`, `ICategoryService`/`CategoryService` (in `Modules/Settings/`, moves to `Modules/Finance/`) → `IAccountService`/`AccountService`, `Transaction.CategoryId` → `Transaction.AccountId`, `GLAccountAssignmentService` → `AccountNumberAssignmentService`.
- `AccountType` enum: `Income, Expense, Asset, Liability, Equity`. **Type is persisted as a string** (`HasConversion<string>` in `src/StageFright.Data/Configurations/CategoryConfiguration.cs` line 14), so existing "Income"/"Expense" values survive the enum rename untouched; only the 3 system rows need Type updates in the migration.
- `Account` gains `bool IsBankAccount` (only settable on Asset accounts; drives "pay from / deposit to / transfer / reconcile" pickers). `GLAccount` property → `AccountNumber`.
- **Historical `Transaction` rows are never updated.** `Transaction.GLAccount` strings ("0100", "1000"…) stay forever as a posting-time snapshot; **all aggregation switches from GLAccount-string filters to `AccountId`** (this is mandatory in Phase 1 or reports break after renumbering).

### 2. Australian-conventional account numbering

| Range | Type | System accounts (seeded, fixed GUIDs) |
|---|---|---|
| 1000–1999 | Assets | 1100 Cash on Hand (ex-0100, IsBankAccount=true), 1200 Member Receivable (ex-0101); user bank accounts from 1110 |
| 2000–2999 | Liabilities | 2310 GST Collected, 2320 GST Paid (new) |
| 3000–3999 | Equity | 3100 Opening Balance Equity, 3200 Accumulated Surplus (new) |
| 4000–4999 | Income | user income, migrated ex-1000+n → 4000+n |
| 6000–6999 | Expenses | user expense, ex-2000+n → 6000+n; Bad Debt ex-9900 → 6999 |

New static class `src/StageFright.Core/Modules/Finance/SystemAccounts.cs` is the single source for well-known GUIDs/numbers — deletes the constants currently duplicated in `FeeService`, `PaymentService`, `IncomeEntryService`, `ReactivationForgivenessService`, and `Modules/Rehearsals/AttendanceService.cs`. Replace count-based `GetNextGLAccountAsync` with `GetNextAccountNumberAsync(AccountType, bool isBank)` = max-in-range + 1 (must use `IgnoreQueryFilters()` — archived accounts still own their numbers).

### 3. GST model

- Enum `GstCode { Gst, GstFree, InputTaxed, BasExcluded }`; nullable `GstCode?` on `Transaction` (historical rows stay null) and on `Fee` (records the code in force at accrual — needed for forgiveness adjustments and BAS).
- Statutory rate in `GstConstants` (0.10m / divisor 11m), not user-editable. `GstCalculator.SplitInclusive(gross)` → `(net, gst)` with `Math.Round(gross/11m, 2, MidpointRounding.AwayFromZero)`; users enter GST-inclusive amounts.
- Two clearing accounts (2310 Collected / 2320 Paid) so BAS 1A/1B are simple account movements. GST recognized at accrual (ATO "accounts method"); stated on the BAS report header.
- Settings: `IsGstRegistered`, `AnnualFeeGstCode`, `AttendanceFeeGstCode` (defaults GstFree). When off: all GST UI hidden, postings are 2-line, codes null.

### 4. Bank reconciliation — header + join table (Transaction rows untouched)

- `BankReconciliation`: Id, AccountId (must be IsBankAccount), StatementDate, StatementClosingBalance, OpeningBalance (snapshot of previous finalised rec's closing, else 0), `ReconciliationStatus { Draft, Finalised }`, FinalisedAt?, Notes?, soft-delete (drafts deletable; finalised immutable — enforced via new `ReconciliationException`).
- `ReconciliationLine`: Id, ReconciliationId, TransactionId, CreatedAt; unique index (ReconciliationId, TransactionId). A transaction may be cleared by at most one non-deleted reconciliation (service-enforced).
- Difference = StatementClosingBalance − (OpeningBalance + Σcleared debits − Σcleared credits); finalise requires |diff| ≤ 0.005.
- CSV-import seam: a future `StatementLine` entity slots in beside `ReconciliationLine` with zero rework (document in XML docs; build nothing now).

### 5. Navigation — implement `MenuItem.SubItems` rendering

`src/StageFright.Plugins.Contracts/MenuItem.cs` already has `SubItems` (currently ignored by the sidebar). Implement expandable groups in `src/StageFright.UI/Layout/ShellLayout.razor`/`.razor.cs` — pure Blazor expand state, auto-expand when a child route is active, `IsActive` gains a starts-with variant for parents; CSS in `src/StageFright.App/wwwroot/css/app.css`. Finance menu: **Finance** → Overview `/finance`, Chart of Accounts `/finance/accounts`, Record Expense `/finance/expenses`, Transfers `/finance/transfers`, Journal Entries `/finance/journal`, Reconciliation `/finance/reconciliation`, Opening Balances `/finance/opening-balances`. Existing FinancePage tabs (Balances / Record Member Payment / Record Income / Apply Annual Fees) stay unchanged.

### 6. Journal entry header

New immutable `JournalEntry` entity (no soft-delete): Id, Date, `JournalEntryType { Income, ExpensePayment, Transfer, GeneralJournal, OpeningBalance }`, Reference?, Payee?, Memo?, CreatedAt. `Transaction` gains nullable `JournalEntryId` FK. Fee/Payment flows keep using their existing FeeId/PaymentId links (no header). `IGLRepository` gains `AddBalancedSetAsync(lines)` — ≥2 lines, one non-zero side each, Σdebits == Σcredits else `GLBalanceException`; `AddPairAsync` retained, delegating to it.

---

## Phases (each ends green: `dotnet build` + full `dotnet test`; scenarios V4–V12 must pass unmodified)

```
Phase 1: CoA foundation ──┬── Phase 2: workflows ──┬── Phase 3: reconciliation
                          │                        └── Phase 4: GST + BAS
                          └── Phase 5: statements/reports (needs only Phase 1)
```

### Phase 1 — Chart of Accounts foundation (biggest; mostly compiler-verified renames)

**Migration `ConvertCategoriesToAccounts`** (SQLite-safe: renames/adds/updates only — verify EF doesn't emit a table rebuild):
1. `RenameTable` Categories→Accounts; `RenameColumn` Transactions.CategoryId→AccountId (+ rename index); `RenameColumn` Accounts.GLAccount→AccountNumber.
2. `AddColumn` Accounts.IsBankAccount (default 0), Settings.FinancialYearStartMonth (default 7).
3. Seed model updates (`SeedSystemCategories` → `SeedSystemAccounts` in `src/StageFright.Data/StageFrightDbContext.cs`, keep the fixed `2026-01-01` seed timestamp) emit `UpdateData` for the 3 system rows: Cash → Type="Asset", "1100", IsBankAccount=1; Member Receivable → "Asset"/"1200"; Bad Debt → "6999".
4. `migrationBuilder.Sql` renumbers user rows: Income `printf('%04d', CAST(AccountNumber AS INTEGER)+3000)`, Expense `+4000` (Type stored as string — filter on `Type='Income'`/`'Expense' AND IsSystem=0`).
5. `InsertData` new system accounts: 3100 Opening Balance Equity, 3200 Accumulated Surplus, 2310 GST Collected, 2320 GST Paid (all IsSystem).
6. Zero statements touch Transactions data. Bump SchemaVersion.

**Code:** the renames from Decision 1; `SystemAccounts` class replaces duplicated constants (posting logic in FeeService/PaymentService/AttendanceService/etc. otherwise byte-for-byte unchanged); `src/StageFright.Data/Repositories/GLRepository.cs` member-balance queries switch `t.GLAccount == "0101"` → `t.AccountId == SystemAccounts.MemberReceivableId`, add `GetAccountBalanceAsync(accountId, asAt)` + `GetAccountMovementsAsync(from, to)`; `AccountService` supports create/edit for all 5 types with validation (name required/unique, bank flag only on assets, system accounts immutable, archive blocked when referenced).

**Reports fixed in this phase (mandatory):** TrialBalance (5 sections, group by AccountId, default range = current FY), IncomeStatement, AccountRegister, MemberAccountSummary, `FinanceSummaryService` — all drop GLAccount-string filters.

**UI:** `ChartOfAccountsPage.razor(+cs)` at `/finance/accounts` — type filter + RadzenDataGrid + add/edit form, copied from the `SettingsCategoryTab` pattern (which is then deleted; remove the Categories tab from `SettingsPage.razor(.cs)` and fix its `?tab=` index map; `RecordIncome`'s "no categories" link retargets `/finance/accounts`). ShellLayout sub-menu support. `FinanceMenuItemProvider` gains SubItems (Overview, Chart of Accounts for now). Settings General tab: FY start month dropdown (reuse existing month-name dropdown pattern). Use the `new-component` skill for scaffolding pairs; follow the SettingsPage `Shown`-flag lazy-render gotcha for any new tabbed UI.

**DI:** rename registrations in `src/StageFright.App/MauiProgram.cs`.

**Tests:** rename fallout across all 5 test projects; new `AccountServiceTests` (number ranges incl. bank 1110+, system protection, bank-flag rules); `AccountRepository` integration (max+1 after archive, IgnoreQueryFilters); **migration integration test** — apply old schema + old-shape data, migrate, assert: row counts, Σdebits=Σcredits unchanged, member balances identical, renumber mapping correct, Transaction.GLAccount strings untouched; bUnit ShellLayout sub-menu tests; updated report tests.

### Phase 2 — Posting engine + money-out workflows

**Migration `AddJournalEntries`:** JournalEntries table + Transactions.JournalEntryId (nullable FK, Restrict) + index.

**Services** (all in `Modules/Finance/`, all inside `IUnitOfWork.ExecuteInTransactionAsync`, audit-logged, `ValidationException` on bad input). Posting recipes (GST-off form):

| Workflow | GL lines |
|---|---|
| Expense payment (`ExpensePaymentService`) | DR Expense / CR Bank (chosen account) |
| Transfer (`AccountTransferService`; from≠to, both IsBankAccount) | DR To / CR From |
| General journal (`GeneralJournalService`; N lines) | user lines verbatim; Member Receivable 1200 blocked in v1 (protects per-member balance integrity) |
| Opening balances (`OpeningBalanceService`) | one line per account at its normal side; plug residual to 3100 Opening Balance Equity; excludes 1200 + GST accounts; warn if an OpeningBalance journal already exists |

`IncomeEntryService.RecordIncomeAsync` gains `DepositAccountId` (defaults 1100). `IGLRepository.AddBalancedSetAsync` + `IJournalEntryRepository`.

**UI:** `ExpensePaymentPage` `/finance/expenses` (bank + expense dropdowns, date, payee, description, amount — copy `RecordIncome` EditForm pattern); `TransferPage` `/finance/transfers`; `JournalEntryPage` `/finance/journal` (dynamic line list in C#, debit-clears-credit per row, running totals + out-of-balance badge, Save gated on balanced); `OpeningBalancesWizard` `/finance/opening-balances` (3 steps: as-at date defaulting to FY start → account grid with live plug preview → confirm). Menu sub-items added.

**Tests:** AddBalancedSetAsync integration (balanced multi-line commits; imbalanced/1-line/both-sides rejected + rolled back); every service path; bUnit journal balance indicator + wizard; new Integration scenario V13 (expenses + transfers); V4–V12 regression.

### Phase 3 — Bank reconciliation

**Migration `AddBankReconciliation`:** BankReconciliation + ReconciliationLine tables, unique (ReconciliationId, TransactionId) index; `ReconciliationException` in `src/StageFright.Core/Exceptions/`.

**Repo/service:** `IBankReconciliationRepository` (create draft w/ chained OpeningBalance, lines add/remove draft-only, cleared-IDs lookup, finalise, soft-delete draft); `GLRepository.GetUnreconciledByAccountAsync`; `BankReconciliationService` — start (IsBankAccount, date > last finalised, one draft per account), toggle-clear, live Difference, finalise gated |diff| ≤ 0.005, finalised recs immutable/undeletable. `AccountService.ArchiveAsync` additionally blocks bank accounts with draft recs.

**UI:** `ReconciliationListPage` `/finance/reconciliation` (account picker, history grid, new/resume draft); `ReconciliationWorkspace` `/finance/reconciliation/{Id:guid}` — summary cards (statement / cleared / difference), RadzenDataGrid with persistent checkbox ticks, Finalise + Delete-draft.

**Report:** `BankReconciliationReportProvider` (`bank-reconciliation`, Finance, order 50): account/statement header, cleared payments, cleared deposits, unpresented items ≤ statement date, difference $0.00 footer.

### Phase 4 — GST + BAS (parallel with Phase 3)

**Migration `AddGst`:** Settings.IsGstRegistered (default 0), Settings.AnnualFeeGstCode / AttendanceFeeGstCode (nullable string, default GstFree semantics), Transactions.GstCode (nullable string), Fees.GstCode (nullable string).

**Core:** `GstCode`, `GstConstants`, `GstCalculator` (+ exhaustive rounding tests). Posting variants when registered:

| Operation | Lines (gross $110, code Gst) |
|---|---|
| Income | DR Bank 110 / CR Income 100 (Gst) / CR GST Collected 10 |
| Expense | DR Expense 100 (Gst) / DR GST Paid 10 / CR Bank 110 |
| Annual/attendance fee accrual (per-fee-type setting; Fee.GstCode stamped at creation) | DR Member Receivable 110 / CR Income 100 (Gst) / CR GST Collected 10 (attendance paid-at-creation: DR Bank instead) |
| Forgiveness of a taxable fee (bad-debt decreasing adjustment) | DR Bad Debt 100 / DR GST Collected 10 / CR Member Receivable 110 (proportions from Fee.GstCode; GST-free fees unchanged) |
| GstFree/InputTaxed → 2-line with code stamped; transfers/journals/opening → BasExcluded/null |

Payment FIFO allocation is **unchanged** (GST recognized at accrual; payments only clear the receivable).

**UI:** Settings General — GST toggle with confirm dialog + per-fee-type GST code dropdowns (visible only when registered); `RecordIncome`/`ExpensePaymentPage` gain GST code dropdown + "includes GST of $X" hint when registered (bUnit-test the hidden state when off).

**Report:** `BasSummaryReportProvider` (`bas-summary`, order 60; explains itself when GST off): filters default to current quarter; Sales (G1 incl-GST, G3 GST-free), Purchases (G11), Summary (1A = net CR movement of 2310, 1B = net DR movement of 2320, 9 = 1A−1B); header notes accruals-basis reporting.

**Tests:** GstCalculator rounding table; 3-line sets balance to the cent for awkward grosses; per-fee-type matrix (registered × fee code); forgiveness GST adjustment; toggle-off regression (postings byte-identical to Phase 2); BAS from a fixture ledger; Integration scenario V15.

### Phase 5 — AU statements & reports (needs only Phase 1; parallel with 2–4)

- **`BalanceSheetReportProvider`** ("Statement of Financial Position", `balance-sheet`, order 25; As-at date filter defaulting FY end): Assets / Liabilities / Equity sections from inception-to-date AccountId balances; Equity includes computed **Accumulated Surplus** = Σ(income CR−DR) − Σ(expense DR−CR) inception→date (no year-end close process — document); footer asserts Assets = Liabilities + Equity.
- **`IncomeStatementReportProvider`** → "Statement of Income & Expenditure": FY presets (This FY / Last FY / custom from FinancialYearStartMonth) + optional prior-year comparison column; surplus/(deficit) total.
- **`GeneralLedgerReportProvider`** (`general-ledger`, order 35; account-or-all + date filters): per-account section with opening balance, dated lines with running balance, closing balance.
- All registered in MauiProgram; they auto-appear on /reports. Tests: balance-sheet equality on fixtures incl. legacy pre-migration rows; FY boundary tests (default July and non-default); running balances; comparison column.

---

## Explicitly NOT changing

- `Fee`/`Payment` entities' immutability, FIFO payment allocation, member-balance definition, aging buckets, dashboard tiles' outputs, backup/restore, setup wizard, reports pipeline infrastructure, plugin contract shapes (SubItems merely gets rendered).
- Historical `Transaction` rows: zero UPDATEs in any migration; `GLAccount` strings stay as posted.
- `AddPairAsync` contract (retained, delegates to `AddBalancedSetAsync`).

## Risks / watch-outs

- Phase 1 rename is wide but compiler-verified; single PR with the migration integration test as the safety net. Manually review the generated migration — SQLite renames/adds only; if EF emits a table rebuild, inspect FK PRAGMA handling before accepting.
- MAUI WebView gotcha: lazy-render (`Shown` flags + StateHasChanged) for any new tabbed UI to avoid concurrent DbContext access.
- Renumber collisions are theoretically possible (>999 categories of a type) — migration includes a defensive duplicate assertion.

## Verification (per phase)

1. `dotnet build` and full `dotnet test` (no `--no-build`) — all 5 test projects green, V4–V12 scenarios unmodified.
2. Migration check: copy a pre-expansion `TestData/stagefright.db`, run the app (auto-migrates), confirm member balances, trial balance totals, and existing reports match pre-migration values.
3. Manual E2E per phase (run app via `dotnet run --project src/StageFright.App/`): Phase 1 — create accounts of each type incl. a second bank account, verify numbering + sub-menu; Phase 2 — record expense/transfer/journal/opening balances, confirm Trial Balance stays balanced; Phase 3 — reconcile to $0.00 difference and finalise; Phase 4 — toggle GST, post $110 income/expense, check BAS 1A=$10/1B=$10; Phase 5 — Balance Sheet balances and cross-foots to Income & Expenditure surplus.
