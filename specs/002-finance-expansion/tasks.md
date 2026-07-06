# Tasks: Finance Module Expansion — Full Accounting, Bank Reconciliation, GST/BAS

**Input**: Design documents from `/specs/002-finance-expansion/` (plan.md, spec.md)

**Tests**: Included — CLAUDE.md mandates exhaustive `Should_X_When_Y` coverage before merge; every phase ends green (`dotnet build` + full `dotnet test`, V4–V12 unmodified).

**Organization**: Tasks grouped by user story. US1 (CoA foundation) is the blocking foundation for US2–US5. US2 blocks US3 and US4. US5 needs only US1.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

**Purpose**: Baseline verification — no scaffolding needed (existing solution).

- [X] T001 Verify baseline: `dotnet build` and full `dotnet test` green on branch ExpandFnance before any change; snapshot a copy of TestData/stagefright.db (if present) for later migration verification

---

## Phase 2: Foundational

**Purpose**: None beyond US1 — the Chart of Accounts rename IS the foundation and is tracked as User Story 1.

*(no tasks)*

---

## Phase 3: User Story 1 — Full chart of accounts (Priority: P1) 🎯 MVP

**Goal**: `Category` → `Account` across the codebase, AU numbering, AccountId-based aggregation, Chart of Accounts page, Finance sub-menu, FY start month setting — with all existing balances/reports identical post-migration.

**Independent Test**: Migrate existing DB → member balances / trial balance / reports unchanged; create accounts of all 5 types incl. a second bank account (numbered 1110+); Categories tab gone; sub-menu renders.

### Core renames & new domain types

- [X] T002 [US1] Rename enum `CategoryType` → `AccountType` with members `Income, Expense, Asset, Liability, Equity` in src/StageFright.Core/Enums/AccountType.cs (delete CategoryType.cs); fix all references
- [X] T003 [US1] Rename entity `Category` → `Account` in src/StageFright.Core/Entities/Account.cs: `GLAccount` → `AccountNumber`, add `bool IsBankAccount`; delete Category.cs; fix all references
- [X] T004 [US1] Rename `Transaction.CategoryId`/`Category` nav → `AccountId`/`Account` in src/StageFright.Core/Entities/Transaction.cs (keep `GLAccount` string snapshot untouched)
- [X] T005 [P] [US1] Create static class `SystemAccounts` (well-known GUIDs + numbers: Cash 1100, MemberReceivable 1200, BadDebt 6999, GstCollected 2310, GstPaid 2320, OpeningBalanceEquity 3100, AccumulatedSurplus 3200) in src/StageFright.Core/Modules/Finance/SystemAccounts.cs
- [X] T006 [US1] Add `FinancialYearStartMonth` (int, default 7) to src/StageFright.Core/Entities/Settings.cs and surface it via ISettingsService/SettingsService in src/StageFright.Core/Modules/Settings/SettingsService.cs

### Contracts, repositories, services

- [X] T007 [US1] Rename `ICategoryRepository` → `IAccountRepository` in src/StageFright.Core/Contracts/IAccountRepository.cs; replace `GetNextGLAccountAsync` with `GetNextAccountNumberAsync(AccountType type, bool isBank)` (max-in-range + 1, `IgnoreQueryFilters()`); rename `CategoryRepository` → `AccountRepository` in src/StageFright.Data/Repositories/AccountRepository.cs
- [X] T008 [US1] Rename `ICategoryService` → `IAccountService` in src/StageFright.Core/Contracts/IAccountService.cs; move+rename `CategoryService` → `AccountService` into src/StageFright.Core/Modules/Finance/AccountService.cs with create/edit for all 5 types (name required/unique, bank flag only on Asset, system accounts immutable, archive blocked when referenced)
- [X] T009 [US1] Rename `GLAccountAssignmentService` → `AccountNumberAssignmentService` in src/StageFright.Core/Modules/Finance/AccountNumberAssignmentService.cs, using range-based numbering (user bank accounts from 1110)
- [X] T010 [US1] Replace duplicated account constants with `SystemAccounts` in src/StageFright.Core/Modules/Finance/FeeService.cs, PaymentService.cs, IncomeEntryService.cs, ReactivationForgivenessService.cs and src/StageFright.Core/Modules/Rehearsals/AttendanceService.cs (posting logic otherwise byte-for-byte unchanged)
- [X] T011 [US1] Update src/StageFright.Data/Repositories/GLRepository.cs: member-balance queries switch `t.GLAccount == "0101"` → `t.AccountId == SystemAccounts.MemberReceivableId`; add `GetAccountBalanceAsync(Guid accountId, DateTime asAt)` and `GetAccountMovementsAsync(DateTime from, DateTime to)` (extend src/StageFright.Core/Contracts/IGLRepository.cs)

### Data layer & migration

- [X] T012 [US1] Rename `CategoryConfiguration` → `AccountConfiguration` in src/StageFright.Data/Configurations/AccountConfiguration.cs (table Accounts, string conversion for AccountType retained, IsBankAccount default false); update `SeedSystemCategories` → `SeedSystemAccounts` in src/StageFright.Data/StageFrightDbContext.cs (fixed 2026-01-01 seed timestamp; system rows: Cash Asset/1100/bank, Member Receivable Asset/1200, Bad Debt Expense/6999; new rows 3100/3200/2310/2320 IsSystem)
- [X] T013 [US1] Add migration `ConvertCategoriesToAccounts` (dotnet ef migrations add — SQLite-safe: RenameTable Categories→Accounts, RenameColumn Transactions.CategoryId→AccountId + index, RenameColumn GLAccount→AccountNumber, AddColumn IsBankAccount + Settings.FinancialYearStartMonth, UpdateData 3 system rows, raw SQL renumber user rows Income +3000 / Expense +4000 with duplicate assertion, InsertData 4 new system accounts, zero Transactions data updates); manually review generated migration for table rebuilds; bump SchemaVersion

### Reports switch to AccountId (mandatory this phase)

- [X] T014 [US1] Drop GLAccount-string filters in favour of AccountId in src/StageFright.Reports/Providers/TrialBalanceReportProvider.cs (5 sections Asset/Liability/Equity/Income/Expense, default range = current FY), IncomeStatementReportProvider.cs, AccountRegisterReportProvider.cs, MemberAccountSummaryReportProvider.cs and src/StageFright.Core/Modules/Finance/FinanceSummaryService.cs

### UI

- [X] T015 [US1] Implement `MenuItem.SubItems` rendering in src/StageFright.UI/Layout/ShellLayout.razor(+.razor.cs): expandable groups, auto-expand on active child route, starts-with `IsActive` for parents; CSS in src/StageFright.App/wwwroot/css/app.css
- [X] T016 [US1] Create `ChartOfAccountsPage.razor(+.razor.cs)` at `/finance/accounts` in src/StageFright.UI/Pages/Finance/ (type filter + RadzenDataGrid + add/edit form, copied from SettingsCategoryTab pattern)
- [X] T017 [US1] Delete src/StageFright.UI/Pages/Settings/SettingsCategoryTab.razor(+.razor.cs); remove Categories tab from SettingsPage.razor(+.razor.cs) and fix `?tab=` index map; retarget RecordIncome "no categories" link to `/finance/accounts`
- [X] T018 [US1] Add SubItems (Overview `/finance`, Chart of Accounts `/finance/accounts`) to src/StageFright.Core/Modules/Finance/FinanceMenuItemProvider.cs
- [X] T019 [US1] Add FY start month dropdown (existing month-name dropdown pattern) to src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor(+.razor.cs)
- [X] T020 [US1] Rename DI registrations in src/StageFright.App/MauiProgram.cs (IAccountRepository/IAccountService/AccountNumberAssignmentService)

### Tests

- [X] T021 [US1] Fix rename fallout across all 5 test projects (tests/StageFright.Core.Tests, Data.Tests, UI.Tests, Integration.Tests, Reports.Tests)
- [X] T022 [P] [US1] New `AccountServiceTests` (number ranges incl. bank 1110+, system-account protection, bank-flag rules, archive-blocked-when-referenced) in tests/StageFright.Core.Tests/
- [X] T023 [P] [US1] `AccountRepository` integration tests (max+1 after archive via IgnoreQueryFilters) in tests/StageFright.Data.Tests/
- [X] T024 [US1] Migration integration test in tests/StageFright.Data.Tests/: apply old schema + old-shape data, run migration, assert row counts, Σdebits=Σcredits unchanged, member balances identical, renumber mapping correct, Transaction.GLAccount strings untouched
- [X] T025 [P] [US1] bUnit ShellLayout sub-menu tests in tests/StageFright.UI.Tests/
- [X] T026 [US1] Verify checkpoint: `dotnet build` + full `dotnet test` green; V4–V12 unmodified

**Checkpoint**: CoA foundation complete — US2 and US5 can start.

---

## Phase 4: User Story 2 — Posting engine + money-out workflows (Priority: P2)

**Goal**: JournalEntry header, balanced multi-line GL sets, expense/transfer/journal/opening-balance workflows with pages.

**Independent Test**: Record expense, transfer, balanced journal, opening balances; Trial Balance stays balanced; imbalanced sets rejected + rolled back.

### Domain & data

- [X] T027 [P] [US2] Create immutable `JournalEntry` entity (no soft-delete) in src/StageFright.Core/Entities/JournalEntry.cs and `JournalEntryType` enum (`Income, ExpensePayment, Transfer, GeneralJournal, OpeningBalance`) in src/StageFright.Core/Enums/JournalEntryType.cs; add nullable `JournalEntryId` FK to Transaction
- [X] T028 [US2] Add `JournalEntryConfiguration` in src/StageFright.Data/Configurations/JournalEntryConfiguration.cs, DbSet in StageFrightDbContext, migration `AddJournalEntries` (JournalEntries table + Transactions.JournalEntryId nullable FK Restrict + index)
- [X] T029 [US2] Add `AddBalancedSetAsync(IReadOnlyList<GLLine> lines, ...)` to src/StageFright.Core/Contracts/IGLRepository.cs + src/StageFright.Data/Repositories/GLRepository.cs (≥2 lines, one non-zero side each, Σdebits == Σcredits else GLBalanceException); retain `AddPairAsync` delegating to it
- [X] T030 [P] [US2] Create `IJournalEntryRepository` in src/StageFright.Core/Contracts/IJournalEntryRepository.cs + `JournalEntryRepository` in src/StageFright.Data/Repositories/JournalEntryRepository.cs

### Services (all in Modules/Finance/, inside IUnitOfWork.ExecuteInTransactionAsync, audit-logged, ValidationException on bad input)

- [X] T031 [P] [US2] `IExpensePaymentService`/`ExpensePaymentService` (DR Expense / CR chosen Bank) in src/StageFright.Core/Modules/Finance/ExpensePaymentService.cs + request model
- [X] T032 [P] [US2] `IAccountTransferService`/`AccountTransferService` (from≠to, both IsBankAccount; DR To / CR From) in src/StageFright.Core/Modules/Finance/AccountTransferService.cs
- [X] T033 [P] [US2] `IGeneralJournalService`/`GeneralJournalService` (N user lines verbatim; Member Receivable 1200 blocked) in src/StageFright.Core/Modules/Finance/GeneralJournalService.cs
- [X] T034 [P] [US2] `IOpeningBalanceService`/`OpeningBalanceService` (one line per account at normal side, plug residual to 3100, exclude 1200 + GST accounts, warn if OpeningBalance journal exists) in src/StageFright.Core/Modules/Finance/OpeningBalanceService.cs
- [X] T035 [US2] `IncomeEntryService.RecordIncomeAsync` gains `DepositAccountId` (defaults Cash 1100) in src/StageFright.Core/Modules/Finance/IncomeEntryService.cs

### UI

- [X] T036 [P] [US2] `ExpensePaymentPage.razor(+.razor.cs)` at `/finance/expenses` (bank + expense dropdowns, date, payee, description, amount — RecordIncome EditForm pattern) in src/StageFright.UI/Pages/Finance/
- [X] T037 [P] [US2] `TransferPage.razor(+.razor.cs)` at `/finance/transfers` in src/StageFright.UI/Pages/Finance/
- [X] T038 [P] [US2] `JournalEntryPage.razor(+.razor.cs)` at `/finance/journal` (dynamic C# line list, debit-clears-credit per row, running totals + out-of-balance badge, Save gated on balanced) in src/StageFright.UI/Pages/Finance/
- [X] T039 [P] [US2] `OpeningBalancesWizard.razor(+.razor.cs)` at `/finance/opening-balances` (3 steps: as-at date defaulting FY start → account grid with live plug preview → confirm) in src/StageFright.UI/Pages/Finance/
- [X] T040 [US2] Add menu sub-items (Record Expense, Transfers, Journal Entries, Opening Balances) to FinanceMenuItemProvider.cs; register new services/repos in src/StageFright.App/MauiProgram.cs

### Tests

- [X] T041 [US2] `AddBalancedSetAsync` integration tests (balanced multi-line commits; imbalanced / 1-line / both-sides rejected + rolled back) in tests/StageFright.Data.Tests/
- [X] T042 [P] [US2] Service tests for every path of ExpensePayment/AccountTransfer/GeneralJournal/OpeningBalance services in tests/StageFright.Core.Tests/
- [X] T043 [P] [US2] bUnit tests: journal balance indicator + opening-balances wizard in tests/StageFright.UI.Tests/
- [X] T044 [US2] New Integration scenario V14 (expenses + transfers) in tests/StageFright.Integration.Tests/ (V13 already used by CommitteeResetAgmBanner); V4–V12 regression
- [X] T045 [US2] Verify checkpoint: `dotnet build` + full `dotnet test` green

**Checkpoint**: Posting engine complete — US3 and US4 can start (parallel).

---

## Phase 5: User Story 3 — Bank reconciliation (Priority: P3)

**Goal**: Per-account manual reconciliation with draft/finalise lifecycle and report.

**Independent Test**: Draft rec → tick to $0.00 → finalise (immutable); one draft per account; transaction cleared by at most one rec.

- [X] T046 [P] [US3] Entities `BankReconciliation` + `ReconciliationLine` in src/StageFright.Core/Entities/, `ReconciliationStatus` enum in src/StageFright.Core/Enums/ReconciliationStatus.cs, `ReconciliationException` in src/StageFright.Core/Exceptions/ReconciliationException.cs (CSV-import seam documented in XML docs)
- [X] T047 [US3] Configurations + DbSets + migration `AddBankReconciliation` (unique (ReconciliationId, TransactionId) index) in src/StageFright.Data/
- [X] T048 [US3] `IBankReconciliationRepository`/`BankReconciliationRepository` (create draft w/ chained OpeningBalance, lines add/remove draft-only, cleared-IDs lookup, finalise, soft-delete draft) + `GLRepository.GetUnreconciledByAccountAsync` in src/StageFright.Data/Repositories/
- [X] T049 [US3] `IBankReconciliationService`/`BankReconciliationService` in src/StageFright.Core/Modules/Finance/ — start (IsBankAccount, date > last finalised, one draft per account), toggle-clear, live Difference, finalise gated |diff| ≤ 0.005, finalised immutable/undeletable; `AccountService.ArchiveAsync` also blocks bank accounts with draft recs
- [X] T050 [P] [US3] `ReconciliationListPage.razor(+.razor.cs)` at `/finance/reconciliation` (account picker, history grid, new/resume draft) in src/StageFright.UI/Pages/Finance/
- [X] T051 [P] [US3] `ReconciliationWorkspace.razor(+.razor.cs)` at `/finance/reconciliation/{Id:guid}` (summary cards statement/cleared/difference, RadzenDataGrid persistent ticks, Finalise + Delete-draft) in src/StageFright.UI/Pages/Finance/
- [X] T052 [P] [US3] `BankReconciliationReportProvider` (`bank-reconciliation`, Finance, order 50) in src/StageFright.Reports/Providers/
- [X] T053 [US3] Menu sub-item + DI registrations in MauiProgram.cs
- [X] T054 [US3] Tests: repository + service paths (draft lifecycle, finalise gating, immutability, single-draft, single-rec-per-transaction), bUnit workspace, report provider in tests/; verify checkpoint build + full test green

---

## Phase 6: User Story 4 — GST + BAS (Priority: P4)

**Goal**: Configurable GST with per-fee-type treatment, 3-line postings to clearing accounts, BAS summary report.

**Independent Test**: GST on → $110 income + expense → BAS 1A=$10/1B=$10; GST off → postings byte-identical to Phase 2, UI hidden.

- [X] T055 [P] [US4] `GstCode` enum in src/StageFright.Core/Enums/GstCode.cs, `GstConstants` (0.10m, divisor 11m) + `GstCalculator.SplitInclusive` (AwayFromZero) in src/StageFright.Core/Modules/Finance/
- [X] T056 [US4] Migration `AddGst`: Settings.IsGstRegistered (default 0), Settings.AnnualFeeGstCode/AttendanceFeeGstCode (nullable string, GstFree default semantics), Transactions.GstCode (nullable string), Fees.GstCode (nullable string); entity + configuration updates
- [X] T057 [US4] Posting variants when registered: IncomeEntryService (DR Bank / CR Income / CR 2310), ExpensePaymentService (DR Expense / DR 2320 / CR Bank), FeeService annual+attendance accrual (per-fee-type setting, Fee.GstCode stamped; attendance paid-at-creation DR Bank), ReactivationForgivenessService (DR Bad Debt / DR 2310 / CR 1200 proportions from Fee.GstCode), AttendanceService — transfers/journals/opening stay BasExcluded/null; payment FIFO allocation unchanged
- [X] T058 [P] [US4] Settings General tab: GST toggle with confirm dialog + per-fee-type GST code dropdowns (visible only when registered) in src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor(+.razor.cs)
- [X] T059 [P] [US4] RecordIncome + ExpensePaymentPage gain GST code dropdown + "includes GST of $X" hint when registered in src/StageFright.UI/Pages/Finance/
- [X] T060 [P] [US4] `BasSummaryReportProvider` (`bas-summary`, order 60; self-explains when GST off; current-quarter defaults; G1/G3/G11, 1A/1B/9 from 2310/2320 movements; accruals-basis header) in src/StageFright.Reports/Providers/ + MauiProgram registration
- [X] T061 [US4] Tests: GstCalculator rounding table, 3-line sets balance to the cent, per-fee-type matrix (registered × fee code), forgiveness GST adjustment, toggle-off regression (byte-identical postings), BAS fixture ledger, Integration scenario V15 in tests/; verify checkpoint build + full test green

---

## Phase 7: User Story 5 — AU statements & reports (Priority: P5)

**Goal**: Balance Sheet, retitled Income & Expenditure with FY presets/comparison, General Ledger detail.

**Independent Test**: Fixture + legacy data → Balance Sheet balances (A = L + E incl. computed Accumulated Surplus); FY presets honour FinancialYearStartMonth; GL running balances correct.

- [X] T062 [P] [US5] `BalanceSheetReportProvider` ("Statement of Financial Position", `balance-sheet`, order 25; as-at filter default FY end; computed Accumulated Surplus; A = L + E footer) in src/StageFright.Reports/Providers/
- [X] T063 [P] [US5] Rework `IncomeStatementReportProvider` → "Statement of Income & Expenditure" (FY presets This FY / Last FY / custom from FinancialYearStartMonth, optional prior-year comparison column, surplus/(deficit) total) in src/StageFright.Reports/Providers/IncomeStatementReportProvider.cs
- [X] T064 [P] [US5] `GeneralLedgerReportProvider` (`general-ledger`, order 35; account-or-all + date filters; opening balance, running balance lines, closing balance per account section) in src/StageFright.Reports/Providers/
- [X] T065 [US5] Register new providers in src/StageFright.App/MauiProgram.cs
- [X] T066 [US5] Tests: balance-sheet equality on fixtures incl. legacy pre-migration rows, FY boundary tests (July default + non-default), running balances, comparison column in tests/StageFright.Reports.Tests/; verify checkpoint build + full test green

---

## Phase 8: Polish & Final Verification

- [ ] T067 Full-solution verification: `dotnet build` + full `dotnet test` (no --no-build), all 5 test projects green, V4–V12 scenarios unmodified
- [ ] T068 Migration end-to-end check: copy pre-expansion TestData/stagefright.db, run app (auto-migrates), confirm member balances / trial balance totals / existing reports match pre-migration values
- [ ] T069 Update specs/002-finance-expansion/plan.md status to implemented

---

## Dependencies & Execution Order

- **US1 (P1)** blocks everything (entity rename + AccountId aggregation).
- **US2 (P2)** needs US1; blocks US3 (reconciliation clears transactions incl. new workflows) and US4 (GST posting variants extend US2 services).
- **US3 (P3)** and **US4 (P4)** are parallel after US2.
- **US5 (P5)** needs only US1.
- Migrations are strictly ordered: ConvertCategoriesToAccounts → AddJournalEntries → AddBankReconciliation → AddGst.

## Implementation Strategy

Sequential by priority (single developer): US1 → US2 → US3 → US4 → US5 → Polish. Each checkpoint requires green build + full test suite before proceeding. [P] tasks within a story touch different files and may be batched.
