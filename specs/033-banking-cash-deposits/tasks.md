# Tasks: Banking Rework (spec 033)

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md)

> **Scale note.** There are 54 tasks across all five source projects, every culture of four resource sets, and about 50 test files that still say "Cash on Hand". The resource files, `MauiProgram.cs` and the debug seeder would otherwise be edited by every story. So the whole back end, all translated text, DI and seed data sit in Phase 2, and each story phase owns only its own pages and tests. Watch Phase 2 Wave 5: it reshapes `RecordPaymentRequest` and `RecordIncomeRequest`, which the seeder and both forms call.

Format: `- [ ] **T###** [P?] [US#] Description · path`. `[P]` means the task is independent within its wave.

---

## Phase 1: Setup

- [x] **T001** Run `dotnet build -t:Rebuild` and `dotnet test` on the branch as it stands, and record the warning count and any failures as the baseline · (no file)

---

## Phase 2: Foundational (blocks every story)

Files: every source file in plan.md's Project Structure except the UI pages owned below, plus all `*.resx` changes, `MauiProgram.cs`, `DebugDataSeeder.cs`, and the Core/Data/Reports test files named here.

**Wave 1 — independent (different files):**
- [x] **T002** [P] Create `ReceivedItem` per data-model §3 (no soft-delete; doc comment states the financial exemption) · src/StageFright.Core/Entities/ReceivedItem.cs
- [x] **T003** [P] Create `BankDeposit` per data-model §4 · src/StageFright.Core/Entities/BankDeposit.cs
- [x] **T004** [P] Create `BankDepositLine` per data-model §5 · src/StageFright.Core/Entities/BankDepositLine.cs
- [x] **T005** [P] Append `BankDepositReversal` and `CashWithdrawal` to `JournalEntryType` with doc comments, and reword the `BankDeposit` doc (no longer "from Cash on Hand") · src/StageFright.Core/Enums/JournalEntryType.cs
- [x] **T006** [P] Add `UndepositedFundsId` (`…0008`) and `UndepositedFundsNumber` ("1105") to `SystemAccounts`, and reword the `CashId` doc to Petty Cash · src/StageFright.Core/Modules/Finance/SystemAccounts.cs
- [x] **T007** [P] Add a read-only `MoneyFormatter.Symbol`, with tests for AUD and a configured non-AUD currency · src/StageFright.Core/Localization/MoneyFormatter.cs, tests/StageFright.Core.Tests/Localization/MoneyFormatterTests.cs
- [x] **T008** [P] Create the static `ReceiptRouting` (`Destination`, `CreatesReceivedItem`) per research §6, with a unit test for each `PaymentMethod` value · src/StageFright.Core/Modules/Finance/ReceiptRouting.cs, tests/StageFright.Core.Tests/Modules/Finance/ReceiptRoutingTests.cs
- [x] **T009** [P] Add every new key, fully translated in de-DE, en-US ("check" wording), es-ES, fr-FR, it-IT, ja-JP and pl-PL, then regenerate qps-ploc with `scripts/generate-pseudo-locale.py`:
  - Banking, deposit, withdrawal and detail page text, plus payment-method field text (`FinanceResource`);
  - `Nav_Finance_Banking`, replacing `Nav_Finance_RecordBankDeposit` (`NavigationResource`);
  - every rule refusal in research §9 to §11 and data-model §1 (`ValidationResource`);
  - `Enum_JournalEntryType_BankDepositReversal` and `Enum_JournalEntryType_CashWithdrawal` (`EnumsResource`);
  - "Cash on Hand" reworded to "Petty Cash" in existing values; obsolete `Finance_BankDeposit_*` keys removed.

  Files: src/StageFright.UI/Resources/Strings/FinanceResource*.resx, src/StageFright.Core/Modules/Localization/Resources/{Navigation,Validation,Enums}Resource*.resx

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — independent:**
- [x] **T010** [P] Add EF configurations: the exactly-one-source check constraint, the filtered unique indexes, the restrict FKs and the (deposit, item) unique index · src/StageFright.Data/Configurations/{ReceivedItem,BankDeposit,BankDepositLine}Configuration.cs
- [x] **T011** [P] Add the repository contracts. `IReceivedItemRepository` has add, get-awaiting with payer data, and get-by-ids. `IBankDepositRepository` has add, get-by-id with lines, history, and set-reversal. `IBankReconciliationRepository` gains `IsTransactionInFinalisedAsync` and `GetDraftLineReconciliationIdAsync` · src/StageFright.Core/Contracts/IReceivedItemRepository.cs, IBankDepositRepository.cs, IBankReconciliationRepository.cs
- [x] **T012** [P] Add the read models, the two request types and `IBankingService` (overview, cash available, deposit, withdrawal, reverse, history, detail) per data-model §6 · src/StageFright.Core/Modules/Finance/{BankingOverview,AwaitingDepositItem,BankingHistoryEntry,DepositDetail,RecordDepositRequest,RecordCashWithdrawalRequest}.cs, src/StageFright.Core/Contracts/IBankingService.cs

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — independent:**
- [x] **T013** [P] Add DbSets, rename row `…0001` to "Petty Cash" and seed the `…0008`/1105 row in `SeedSystemAccounts`, keeping the fixed 2026-01-01 timestamp · src/StageFright.Data/StageFrightDbContext.cs
- [x] **T014** [P] Implement `ReceivedItemRepository` and `BankDepositRepository`, plus the two new reconciliation queries. Translate raw exceptions to `DataAccessException` · src/StageFright.Data/Repositories/{ReceivedItemRepository,BankDepositRepository,BankReconciliationRepository}.cs

**⟶ Wait for Wave 3 to finish, then:**

**Wave 4 — independent:**
- [x] **T015** [P] Generate the `AddBankingRework` migration and check it holds only the three tables, the `UpdateData` rename and the `InsertData` row, with a reversible `Down`. Add a SQLite test that the migrated database has 1105 as a system non-bank asset, that 1100 is named "Petty Cash" with its number and rows unchanged (FR-002, US2-1), and that the tables exist · src/StageFright.Data/Migrations/<ts>_AddBankingRework.cs, tests/StageFright.Data.Tests/Migrations/AddBankingReworkMigrationTests.cs
- [x] **T016** [P] Add SQLite integration tests for both new repositories and the reconciliation queries. Awaiting is derived (a reversed deposit makes its items awaiting again), the payer resolves to member, organisation (archived), description or opening balance, and finalised versus draft lines are told apart · tests/StageFright.Data.Tests/Repositories/{ReceivedItemRepository,BankDepositRepository,BankReconciliationRepositoryFinalised}Tests.cs

**⟶ Wait for Wave 4 to finish, then:**

**Wave 5 — independent services (different files):**
- [x] **T017** [P] Route `PaymentService.RecordAsync` through `ReceiptRouting`. Every money-side line goes to the destination. Cheque and other add one `ReceivedItem` for `Payment.Amount` with its `Reference`. Card and EFT require a valid bank account other than Petty Cash. Add `BankAccountId` and `Reference` to `RecordPaymentRequest`. Unit-test every method, a missing or invalid bank, and a multi-fee payment that gives one item (edge case) · src/StageFright.Core/Modules/Finance/PaymentService.cs, RecordPaymentRequest.cs, tests/StageFright.Core.Tests/Modules/Finance/PaymentServiceTests.cs
- [x] **T018** [P] Route `IncomeEntryService.RecordIncomeAsync` by `PaymentMethod`. Replace `DepositAccountId` with `PaymentMethod`, `BankAccountId` and `Reference`. The item stores the gross under exclusive tax. The organisation link stays on the entry (FR-025). Audit against the journal entry. Unit-test each method under inclusive and exclusive tax · src/StageFright.Core/Modules/Finance/IncomeEntryService.cs, RecordIncomeRequest.cs, tests/StageFright.Core.Tests/Modules/Finance/IncomeEntryServiceTests.cs
- [x] **T019** [P] Exclude Undeposited Funds from `GetJournalAccountsAsync` and refuse it in `RecordJournalAsync` (FR-001), with tests · src/StageFright.Core/Modules/Finance/GeneralJournalService.cs, tests/StageFright.Core.Tests/Modules/Finance/GeneralJournalServiceTests.cs
- [x] **T020** [P] Make `OpeningBalanceService` create one "opening balance" `ReceivedItem` for a positive Undeposited Funds amount and refuse a negative one. Test both, plus the setup-wizard path through `SetupService` · src/StageFright.Core/Modules/Finance/OpeningBalanceService.cs, tests/StageFright.Core.Tests/Modules/Finance/OpeningBalanceServiceTests.cs, tests/StageFright.Data.Tests/Settings/SetupServiceIntegrationTests.cs
- [x] **T021** [P] Implement `BankingService` per research §4, §5 and §8 to §11:
  - the overview, and cash available as the lower of the deposit-date and current balances;
  - a deposit with one bank line and no zero lines, with in-transaction awaiting re-checks;
  - a withdrawal;
  - a reversal with the already-reversed, finalised and draft rules and the original date;
  - history and detail;
  - audit for each posting.

  Unit-test every branch · src/StageFright.Core/Modules/Finance/BankingService.cs, tests/StageFright.Core.Tests/Modules/Finance/BankingServiceTests.cs
- [x] **T022** [P] Backup `1.4.0`, per data-model §7:
  - bump `BackupSchema`;
  - add envelope members 29, 31 and 32 and their DTOs;
  - add snapshot read, map and count support;
  - upsert in order after Transactions;
  - rename Petty Cash for files older than 1.4.0.

  Add SQLite round-trip tests for a 1.4.0 file and for restoring 1.2.0 and 1.3.0 files (FR-022, SC-006) · src/StageFright.Core/BackupSchema.cs, src/StageFright.Core/Modules/Settings/BackupService.cs, Backup/BackupEnvelope.cs, Backup/BackupSnapshot.cs, Backup/{ReceivedItem,BankDeposit,BankDepositLine}BackupDto.cs, src/StageFright.Data/Repositories/BackupRepository.cs, tests/StageFright.Integration.Tests/Scenarios/V9_BackupRestoreTests.cs
- [x] **T023** [P] Report tests showing historical `Transfer` and `BankDeposit` entries, plus the new `BankDepositReversal` and `CashWithdrawal` entries, render correctly in the General Ledger, Account Register, Trial Balance and Balance Sheet, with 1105 under Assets (FR-021) · tests/StageFright.Reports.Tests/Providers/BankingJournalTypesReportTests.cs

**⟶ Wait for Wave 5 to finish, then:**

**Wave 6 — independent:**
- [x] **T024** [P] Register `IReceivedItemRepository`, `IBankDepositRepository` and `IBankingService`, and remove the `IBankDepositService` registration · src/StageFright.App/MauiProgram.cs
- [x] **T025** [P] Rewrite the seeder's banking flows per research §15, keeping the books balanced over two years (FR-024):
  - cheque, card and EFT member payments;
  - the EFT council grant and the cheque bowling-club sponsorship;
  - periodic deposits of cheques plus cash above the float;
  - concert float withdrawals;
  - one reversed deposit that is then re-recorded;
  - an Undeposited Funds opening balance.

  · src/StageFright.App/Seeding/DebugDataSeeder.cs
- [x] **T026** [P] Change "Cash on Hand" to "Petty Cash" in code comments, future GL descriptions and the test assertions and fixtures that name it. Leave out every test file a story phase owns (PaymentFormTests, RecordIncomeTests, FinanceMenuItemProviderTests, and the three US1 deletes); those phases fix their own assertions · src/**/*.cs comments (AttendanceService, Account, AccountNumberAssignmentService, SetupService, StageFrightDbContext summary), tests/**/*.cs

**Checkpoint:** the solution builds with every change to the back end, schema, backup, translated text and seed data in place, and all Phase 2 tests pass.

---

## Phase 3: User Story 1 — Cheques wait in a holding account (P1, MVP)

Files: src/StageFright.UI/Pages/Finance/{BankingPage.razor,BankingPage.razor.cs,PaymentForm.razor,PaymentForm.razor.cs,PaymentFormModel.cs,RecordIncome.razor,RecordIncome.razor.cs,RecordIncomeModel.cs}, src/StageFright.Core/Modules/Finance/FinanceMenuItemProvider.cs, removal of BankDepositPage.razor(.cs), BankDepositModel.cs, BankDepositService.cs, RecordBankDepositRequest.cs, src/StageFright.Core/Contracts/IBankDepositService.cs.

### Tests (write first; they fail until the implementation below)
- [x] **T027** [P] [US1] Journey through the services on SQLite:
  - A cheque member payment drops the member's balance, raises Undeposited Funds and changes no bank account (FR-003, US1-1, SC-001).
  - A cheque income entry lands in its income account and in Undeposited Funds (FR-004, US1-2).
  - Card and EFT go straight to the chosen bank and are never listed (US1-4).
  - Two cheques are listed with date, payer, reference and amount, and their total (FR-006, FR-008, US1-3).
  - An organisation-linked cheque shows the organisation's name and keeps its link (FR-025).

  · tests/StageFright.Integration.Tests/Scenarios/V33_ChequesAwaitingDepositTests.cs
- [x] **T028** [P] [US1] bUnit, PaymentForm:
  - The bank picker appears only for card or EFT and is preselected when there is one bank account.
  - The reference box appears only for cheque or other, and is disabled after save.
  - The amount prefix uses the configured currency symbol.

  · tests/StageFright.UI.Tests/Pages/Finance/PaymentFormTests.cs
- [x] **T029** [P] [US1] bUnit, RecordIncome:
  - The payment method takes the place of "Deposit to", followed by the bank picker and the reference box.
  - The organisation picker stays above the description (FR-004).
  - The no-bank warning appears only for card or EFT, and cash or cheque still saves.

  · tests/StageFright.UI.Tests/Pages/Finance/RecordIncomeTests.cs
- [x] **T030** [P] [US1] bUnit, BankingPage:
  - It shows the awaiting grid's columns and total, and the Petty Cash chip, including a negative balance (FR-007, FR-008).
  - With no bank account, it shows the message linking to `/finance/accounts`.
  - The old route `/finance/bank-deposit` renders the Banking page.
  - The history grid lists entries.

  · tests/StageFright.UI.Tests/Pages/Finance/BankingPageTests.cs
- [x] **T031** [P] [US1] The menu's Banking sub-item sits at `DisplayOrder` 3 and routes to `/finance/banking` (FR-007) · tests/StageFright.Core.Tests/Modules/Finance/FinanceMenuItemProviderTests.cs

### Implementation
**Wave 1 — independent:**
- [x] **T032** [P] [US1] Add to PaymentForm:
  - an `IAccountService` injection;
  - the bank picker (bank accounts other than Petty Cash, preselected when there is one);
  - the reference box, disabled after save;
  - the `MoneyFormatter.Symbol` prefix.

  · src/StageFright.UI/Pages/Finance/PaymentForm.razor, PaymentForm.razor.cs, PaymentFormModel.cs
- [x] **T033** [P] [US1] Change RecordIncome:
  - a payment-method select in the old "Deposit to" slot, then a conditional bank picker and reference;
  - a per-method no-bank warning;
  - the `MoneyFormatter.Symbol` prefix.

  · src/StageFright.UI/Pages/Finance/RecordIncome.razor, RecordIncome.razor.cs, RecordIncomeModel.cs
- [x] **T034** [P] [US1] Create BankingPage with `@page "/finance/banking"` and `@page "/finance/bank-deposit"`:
  - summary chips;
  - the awaiting `RadzenDataGrid` (standard settings);
  - buttons to `/finance/banking/deposit` and `/finance/banking/withdrawal`;
  - the history grid, whose rows link to `/finance/banking/deposits/{id}`;
  - the no-bank message.

  · src/StageFright.UI/Pages/Finance/BankingPage.razor, BankingPage.razor.cs
- [x] **T035** [P] [US1] Replace the Record Bank Deposit sub-item with Banking at `DisplayOrder` 3 · src/StageFright.Core/Modules/Finance/FinanceMenuItemProvider.cs

**⟶ Wait for Wave 1 to finish, then:**
- [x] **T036** [US1] Delete BankDepositPage, BankDepositModel, BankDepositService, IBankDepositService and RecordBankDepositRequest, plus BankDepositPageTests, BankDepositServiceTests and V17_BankDepositTests, whose coverage moves to V35 · src/StageFright.UI/Pages/Finance/BankDepositPage.*, src/StageFright.Core/Modules/Finance/BankDepositService.cs, tests/StageFright.UI.Tests/Pages/Finance/BankDepositPageTests.cs, tests/StageFright.Core.Tests/Modules/Finance/BankDepositServiceTests.cs, tests/StageFright.Integration.Tests/Scenarios/V17_BankDepositTests.cs

**Checkpoint:** cheques are held and listed on the Banking screen, and card and EFT receipts reach the chosen bank. This works and can be tested on its own.

---

## Phase 4: User Story 2 — Cash takings go into Petty Cash (P1)

Files: tests/StageFright.Integration.Tests/Scenarios/V34_PettyCashTests.cs. The story's behaviour is delivered by Phase 2 (rename and routing) and US1 (forms). This phase proves it end to end.

### Tests
- [ ] **T037** [US2] On SQLite:
  - The chart of accounts shows "Petty Cash" with number 1100, its full history and an unchanged balance (FR-002, US2-1).
  - A cash member payment, a rehearsal attendance cash fee and a cash raffle income each raise Petty Cash, and none of them is listed as awaiting deposit (FR-003, FR-004, FR-005, US2-2, US2-3).
  - Petty Cash is still offered as a pay-from account for an expense (FR-002).

  · tests/StageFright.Integration.Tests/Scenarios/V34_PettyCashTests.cs

**Checkpoint:** cash receipts land in Petty Cash, and that is verified across every entry point.

---

## Phase 5: User Story 3 — Make a bank deposit (P1)

Files: src/StageFright.UI/Pages/Finance/{DepositPage.razor,DepositPage.razor.cs,DepositModel.cs,DepositItemSelectionGrid.razor,DepositItemSelectionGrid.razor.cs}, tests named below.

### Tests
- [ ] **T038** [P] [US3] On SQLite:
  - **Posting:** a deposit of selected cheques plus cash raises the bank by the total, lowers Undeposited Funds by the cheques and Petty Cash by the cash (FR-009, FR-012, US3-1). The unselected cheque stays listed, and an included cheque is not offered again (US3-5).
  - **Reconciliation:** a deposit of 10 receipts is one reconciliation line (FR-013, SC-003, US3-2).
  - **Refusals (FR-010, US3-3, US3-4):** each is refused with its message:
    - no cheques and no cash;
    - cash above the deposit-date balance, and cash above the current balance;
    - a cheque already deposited by a concurrent deposit;
    - a deposit date before a cheque's receipt date;
    - a closed period (FR-018).
  - **Integrity:** the trial balance stays balanced (SC-002), and an audit entry is written (FR-019).

  · tests/StageFright.Integration.Tests/Scenarios/V35_BankDepositTests.cs
- [ ] **T039** [P] [US3] bUnit, DepositPage:
  - The running total splits into cheques and cash on every tick and keystroke (FR-011).
  - A single bank account is preselected, and the cash available is shown.
  - The cash box is disabled when the cash available is zero or less.
  - Refusal and closed-period messages are shown.
  - The no-bank message is shown.

  · tests/StageFright.UI.Tests/Pages/Finance/DepositPageTests.cs
- [ ] **T040** [P] [US3] bUnit, DepositItemSelectionGrid: the select-all header template, and a selection-changed total · tests/StageFright.UI.Tests/Pages/Finance/DepositItemSelectionGridTests.cs

### Implementation
**Wave 1 — independent:**
- [ ] **T041** [P] [US3] Create the item picker grid, modelled on `OutstandingFeeSelectionGrid` · src/StageFright.UI/Pages/Finance/DepositItemSelectionGrid.razor, DepositItemSelectionGrid.razor.cs
- [ ] **T042** [P] [US3] Create the deposit form model · src/StageFright.UI/Pages/Finance/DepositModel.cs

**⟶ Wait for Wave 1 to finish, then:**
- [ ] **T043** [US3] Create DepositPage at `/finance/banking/deposit`, with the running total, the cash available, validation and the save-and-return-to-Banking flow · src/StageFright.UI/Pages/Finance/DepositPage.razor, DepositPage.razor.cs

**Checkpoint:** the receive-then-deposit cycle works end to end, and US1 plus US3 form the MVP.

---

## Phase 6: User Story 4 — Withdraw a cash float (P2)

Files: src/StageFright.UI/Pages/Finance/{CashWithdrawalPage.razor,CashWithdrawalPage.razor.cs,CashWithdrawalModel.cs}, tests named below.

### Tests
- [ ] **T044** [P] [US4] On SQLite:
  - A withdrawal lowers the bank and raises Petty Cash, and shows in history with its purpose (FR-014, US4-1).
  - A zero or negative amount, or no bank account, is refused (US4-2).
  - The withdrawal is one reconciliation line (US4-3).
  - A closed period is refused (FR-018), and an audit entry is written (FR-019).
  - A float banked back through a deposit leaves Petty Cash where expected (FR-017, verification step 4).

  · tests/StageFright.Integration.Tests/Scenarios/V36_CashWithdrawalTests.cs
- [ ] **T045** [P] [US4] bUnit, CashWithdrawalPage: required fields, a single bank account preselected, the no-bank message, and the success and error states · tests/StageFright.UI.Tests/Pages/Finance/CashWithdrawalPageTests.cs

### Implementation
- [ ] **T046** [US4] Create CashWithdrawalPage at `/finance/banking/withdrawal` and its model · src/StageFright.UI/Pages/Finance/CashWithdrawalPage.razor, CashWithdrawalPage.razor.cs, CashWithdrawalModel.cs

**Checkpoint:** floats can be withdrawn and banked back.

---

## Phase 7: User Story 5 — Review and reverse deposits (P3)

Files: src/StageFright.UI/Pages/Finance/{DepositDetailPage.razor,DepositDetailPage.razor.cs}, tests named below.

### Tests
- [ ] **T047** [P] [US5] On SQLite:
  - **History:** it lists date, type, bank, total and status (FR-015, US5-1).
  - **Detail:** its cash plus cheques equals the total (US5-2).
  - **Reversal (FR-016, SC-005, US5-3):** the bank, Undeposited Funds and Petty Cash return to their exact pre-deposit balances. The cheques are awaiting again, the reversal is dated on the deposit's date, and it is shown as reversed.
  - **Refusals:** a second reversal is refused (US5-4); a closed-period deposit is refused (FR-018, US5-5); a deposit cleared in a finalised reconciliation is refused with its message (US5-6).
  - **Allowed cases:** a deposit cleared only in a draft reconciliation is reversed, and its draft line removed (US5-7). Reversing when the cash was spent leaves Petty Cash negative (edge case).
  - **Integrity:** organisation history totals are unchanged by the deposit and the reversal (FR-025); the trial balance stays balanced; an audit entry is written.

  · tests/StageFright.Integration.Tests/Scenarios/V37_DepositReversalTests.cs
- [ ] **T048** [P] [US5] bUnit, DepositDetailPage:
  - Items and cash are listed with their total.
  - Reverse asks for confirmation in two steps.
  - No Reverse is offered once reversed.
  - Refusal reasons and the closed-period message are shown.

  · tests/StageFright.UI.Tests/Pages/Finance/DepositDetailPageTests.cs

### Implementation
- [ ] **T049** [US5] Create DepositDetailPage at `/finance/banking/deposits/{id:guid}`, with the inline two-step reverse and a return to Banking · src/StageFright.UI/Pages/Finance/DepositDetailPage.razor, DepositDetailPage.razor.cs

**Checkpoint:** deposits can be reviewed and corrected without editing any financial record.

---

## Phase 8: Polish

**Wave 1 — independent:**
- [ ] **T050** [P] Update CLAUDE.md:
  - the entity count goes from 22 to 25, plus the new entities and their financial exemption;
  - Finance / GL integrity gains Petty Cash 1100 and Undeposited Funds 1105, and describes the Banking flow;
  - the backup version becomes `1.4.0`;
  - the spec 032 line on organisation links lists the new types.

  · CLAUDE.md
- [ ] **T051** [P] Replace spec 032's "pending spec 033" notes with the final wording: FR-012 and SC-003, data-model §2 and §3, ui-contract §5.3 · specs/032-external-organisations/spec.md, data-model.md, contracts/ui-contract.md
- [ ] **T052** [P] Write spec 030's `1.4.0` note and mark spec 009's FR-001 to FR-003 and FR-006 as superseded by spec 033 · specs/030-backup-restore/contracts/backup-envelope-schema.md, specs/009-bank-deposit-transfers/spec.md

**⟶ Wait for Wave 1 to finish, then:**
- [ ] **T053** Validate against the Success Criteria:
  - run a full `dotnet build -t:Rebuild`, with no new warnings over T001;
  - run the full `dotnet test`, including the Localization guard tests (FR-023);
  - map SC-001, SC-002, SC-003, SC-005 and SC-006 to their passing tests.

  · (no file)
- [ ] **T054** Verify in the running app with the seed data over CDP:
  - verification steps 3 and 4;
  - a deposit of 5 cheques plus cash recorded in under a minute (SC-004);
  - step 5: a language switch, checking translated screens and configured-currency amounts.

  · (no file)

---

## Dependencies & Execution Order

- **Phases:** Setup, then Foundational, then the stories, then Polish. US1 comes before US3 (deposits need listed items and the Banking page); US3 comes before US5 (reversal needs deposits). US2 and US4 depend only on Foundational and US1, so they can run beside US3.
- **Phase 2:**
  - Wave 1 (T002 to T009) blocks Wave 2 (T010 to T012).
  - Wave 2 blocks Wave 3 (T013 and T014).
  - Wave 3 blocks Wave 4 (T015 and T016).
  - Wave 4 blocks Wave 5 (T017 to T023).
  - Wave 5 blocks Wave 6 (T024 to T026).
- **US1:** tests T027 to T031, then Wave 1 (T032 to T035), then T036.
- **US2:** T037.
- **US3:** tests T038 to T040, then Wave 1 (T041 and T042), then T043.
- **US4:** tests T044 and T045, then T046.
- **US5:** tests T047 and T048, then T049.
- **Polish:** Wave 1 (T050 to T052), then T053, then T054.
