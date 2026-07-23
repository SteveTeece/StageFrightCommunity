
description: "Task list template for feature implementation"
---

# Tasks: Bank Deposit Recording

**Input**: Design documents from `/specs/009-bank-deposit-transfers/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included and REQUIRED, not optional — this project's constitution (§11, "Non-Negotiable Coverage Rule") and CLAUDE.md mandate exhaustive automated coverage of every reachable code path before merge, overriding the default "tests are optional" behavior.

**Organization**: Tasks are grouped by user story (US1 = P1 Record a bank deposit, US2 = P2 One clear workflow instead of two, US3 = P3 Historical transfers remain accurate in reports) to enable independent implementation and testing of each story, per spec.md.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- File paths are exact and relative to the repository root

## Path Conventions

Single MAUI Blazor Hybrid solution (`StageFrightCommunity.slnx`): `src/StageFright.Core/`, `src/StageFright.UI/`, `src/StageFright.App/`, `tests/StageFright.Core.Tests/`, `tests/StageFright.UI.Tests/`, `tests/StageFright.Integration.Tests/` — see plan.md Project Structure.

---

## Phase 1: Setup

**Purpose**: Confirm a clean baseline before making changes. No new project, package, or scaffolding is needed — every dependency (EF Core, Radzen.Blazor, Blazor.Bootstrap, Serilog, xUnit, NSubstitute, bUnit) is already installed and used by the sibling `AccountTransferService`/`IncomeEntryService`/`TransferPage` code this feature replaces or mirrors.

- [X] T001 Run `dotnet build` and `dotnet test` from the repo root and confirm both are green before starting, establishing the baseline this feature must not regress

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The one shared prerequisite every later phase depends on.

- [X] T002 Add a `BankDeposit` member to `JournalEntryType` in `src/StageFright.Core/Enums/JournalEntryType.cs` (insert between `Transfer` and `GeneralJournal`), with an XML doc comment ("Cash on Hand deposited into a bank account") per data-model.md — no EF migration needed, `JournalEntryConfiguration` already persists `Type` via `HasConversion<string>()`

**Checkpoint**: Foundation ready — User Story 1 can now begin. (User Story 3 also only needs this; User Story 2 additionally needs User Story 1 complete — see Dependencies below.)

---

## Phase 3: User Story 1 - Record a bank deposit of collected cash (Priority: P1) 🎯 MVP

**Goal**: A treasurer can record cash collected and physically deposited at the bank, decreasing Cash on Hand and increasing the chosen bank account by the exact amount, via a dedicated `/finance/bank-deposit` workflow.

**Independent Test**: Record a deposit of a set amount from Cash on Hand into a nominated bank account and confirm Cash on Hand decreases and the bank account balance increases by that amount, with no other accounts affected — works regardless of whether the old Transfer workflow has been retired yet (User Story 2).

### Tests for User Story 1 (write first; confirm they fail before implementation)

- [X] T003 [P] [US1] Write `BankDepositServiceTests` in `tests/StageFright.Core.Tests/Modules/Finance/BankDepositServiceTests.cs`, mirroring `AccountTransferServiceTests.cs`'s NSubstitute-based structure but without any from-account cases: zero amount, negative amount, destination account not found, destination not a bank account, destination equals `SystemAccounts.CashId`, successful Debit-destination/Credit-Cash-on-Hand posting under a `JournalEntryType.BankDeposit` journal entry, default description (`"Bank deposit — {destination account name}"` when blank), unit-of-work wrapping, and audit log write
- [X] T004 [P] [US1] Write `V17_BankDepositTests.cs` in `tests/StageFright.Integration.Tests/Scenarios/V17_BankDepositTests.cs` against a real SQLite in-memory DB (mirror `V14_ExpensesTransfersTests.cs`'s `IAsyncLifetime` setup): assert a successful deposit creates one `JournalEntry{Type=BankDeposit}` with a balanced two-row GL pair, moves `GLRepository.GetAccountBalanceAsync` balances correctly for both Cash on Hand and the destination account, and that zero-amount/non-bank-destination/destination-equals-cash submissions throw and persist nothing
- [X] T005 [P] [US1] Write `BankDepositPageTests` in `tests/StageFright.UI.Tests/Pages/Finance/BankDepositPageTests.cs` (new bUnit file — `TransferPage` had no prior bUnit coverage): loading state, warning banner when the only bank account is Cash on Hand (no eligible destination), successful submit showing the confirmation message + "Record Another", and each client-side validation message (amount required/positive, destination required)

### Implementation for User Story 1

- [X] T006 [P] [US1] Create `RecordBankDepositRequest` in `src/StageFright.Core/Modules/Finance/RecordBankDepositRequest.cs` per data-model.md: `DateTime Date`, `decimal Amount`, `Guid ToAccountId`, `string? Description` — no `FromAccountId` field (source is always Cash on Hand)
- [X] T007 [P] [US1] Create `IBankDepositService` contract in `src/StageFright.Core/Contracts/IBankDepositService.cs` per contracts/bank-deposit-service-contract.md: single `Task RecordDepositAsync(RecordBankDepositRequest request, CancellationToken ct = default)` method
- [X] T008 [US1] Implement `BankDepositService` in `src/StageFright.Core/Modules/Finance/BankDepositService.cs` (depends on T006, T007): validate `Amount > 0` → `ValidationException`, destination account exists → `EntityNotFoundException`, destination `IsBankAccount` → `ValidationException`, destination id `!= SystemAccounts.CashId` → `ValidationException`; default blank `Description` to `"Bank deposit — {destination account name}"`; inside `IUnitOfWork.ExecuteInTransactionAsync`, create one `JournalEntry{Type=JournalEntryType.BankDeposit}` and post a balanced two-row `Transaction` pair (Debit destination account / Credit `SystemAccounts.CashId`) via `IGLRepository.AddBalancedSetAsync`; write one `IAuditTrailService.LogAsync` entry — mirror `AccountTransferService`'s dependency wiring (`IAccountRepository`, `IGLRepository`, `IJournalEntryRepository`, `IAuditTrailService`, `IUnitOfWork`)
- [X] T009 [US1] Register `IBankDepositService` → `BankDepositService` in `src/StageFright.App/MauiProgram.cs`'s `RegisterCoreServices`, as a new line alongside the existing `IAccountTransferService` registration (line ~186) — do not remove the old line yet (depends on T008)
- [X] T010 [P] [US1] Create `BankDepositModel` in `src/StageFright.UI/Pages/Finance/BankDepositModel.cs`, mirroring `TransferModel.cs` minus the source field: `Date` (defaults to `DateTime.Today`), `Amount`, `ToAccountId`, `Description`
- [X] T011 [US1] Create `BankDepositPage.razor.cs` in `src/StageFright.UI/Pages/Finance/BankDepositPage.razor.cs` (depends on T009, T010): inject `IBankDepositService` and `IAccountService`; in `OnInitializedAsync` load bank accounts via `GetBankAccountsAsync()` and filter out `SystemAccounts.CashId` for the destination option list; client-side validation mirroring the service's rules (amount > 0, destination selected); `SaveAsync` builds a `RecordBankDepositRequest` and calls `RecordDepositAsync`; `RecordAnother` resets the form — mirror `TransferPage.razor.cs`'s structure and error-dictionary pattern
- [X] T012 [US1] Create `BankDepositPage.razor` in `src/StageFright.UI/Pages/Finance/BankDepositPage.razor` (depends on T011): `@page "/finance/bank-deposit"`, `<PageTitle>Record Bank Deposit</PageTitle>`, a fixed "From: Cash on Hand" label (not a picker), a destination `InputSelect` bound to the filtered bank-account list, amount/date/description fields, and a warning banner (linking to `/finance/accounts`) when the filtered destination list is empty (FR-007) — mirror `TransferPage.razor`'s markup and Bootstrap utility classes
- [X] T013 [US1] Add a new `MenuItem { Title = "Record Bank Deposit", Route = "/finance/bank-deposit" }` to the Finance sub-items in `src/StageFright.Core/Modules/Finance/FinanceMenuItemProvider.cs`, alongside (not replacing) the existing "Transfers" entry, so the new workflow is reachable without yet retiring the old one (depends on T012)
- [ ] T014 [US1] Manually run the quickstart.md Story 1 steps against a running `dotnet run --project src/StageFright.App/` instance

**Checkpoint**: User Story 1 is fully functional and independently testable — bank deposits can be recorded end-to-end via `/finance/bank-deposit`. This is the MVP. The old Transfer workflow still exists untouched at this point.

---

## Phase 4: User Story 2 - One clear workflow instead of two overlapping ones (Priority: P2)

**Goal**: Retire the generic "Transfer between any two accounts" workflow so exactly one bank-deposit entry point remains in the Finance nav, while the Journal Entry page keeps handling arbitrary any-account movements unchanged.

**Independent Test**: Confirm the page previously labeled "Transfer" now presents only the bank-deposit-specific workflow (fixed cash source, bank destination picker) — built in User Story 1 — while the ability to move funds between two arbitrary accounts remains available solely through the existing Journal Entry page.

### Tests for User Story 2 (write first; confirm they fail before implementation)

- [X] T015 [P] [US2] Update `FinanceMenuItemProviderTests` in `tests/StageFright.Core.Tests/Modules/Finance/FinanceMenuItemProviderTests.cs`: replace the `[InlineData("Transfers")]` theory case with `[InlineData("Record Bank Deposit")]`, and add a route assertion (`Route == "/finance/bank-deposit"`) mirroring the existing `GetMenuItems_ChartOfAccounts_StillRoutesToFinanceAccounts` test
- [X] T016 [P] [US2] Delete `tests/StageFright.Core.Tests/Modules/Finance/AccountTransferServiceTests.cs` — its subject class is being deleted in this phase; equivalent coverage already exists in `BankDepositServiceTests.cs` (T003)
- [X] T017 [P] [US2] Update `tests/StageFright.Integration.Tests/Scenarios/V14_ExpensesTransfersTests.cs`: remove the `--- Transfers ---` region's `BuildTransferService()` helper and its four `RecordTransfer_*` test methods (subject class is being deleted), and rework `ExpenseThenTransfer_LedgerDebitsEqualCredits_AndEachEntryBalances` to post a bank deposit via `BankDepositService` instead of a transfer, keeping the mixed-entry-type ledger-balance assertion (Σdebits = Σcredits across an expense + a deposit)

### Implementation for User Story 2

- [X] T018 [P] [US2] Delete `src/StageFright.Core/Modules/Finance/AccountTransferService.cs`, `src/StageFright.Core/Contracts/IAccountTransferService.cs`, and `src/StageFright.Core/Modules/Finance/RecordTransferRequest.cs`
- [X] T019 [P] [US2] Delete `src/StageFright.UI/Pages/Finance/TransferPage.razor`, `src/StageFright.UI/Pages/Finance/TransferPage.razor.cs`, and `src/StageFright.UI/Pages/Finance/TransferModel.cs`
- [X] T020 [P] [US2] Update `src/StageFright.App/MauiProgram.cs`: remove the `services.AddScoped<IAccountTransferService, AccountTransferService>();` line (depends on T018)
- [X] T021 [P] [US2] Update `src/StageFright.App/Seeding/DebugDataSeeder.cs`: replace the `IAccountTransferService` field/constructor parameter and the cash-sweep call (around line 491, `RecordTransferAsync(new RecordTransferRequest { ... FromAccountId = SystemAccounts.CashId, ToAccountId = bankAccount.Id, ... })`) with `IBankDepositService`/`RecordDepositAsync(new RecordBankDepositRequest { Date, Amount, ToAccountId = bankAccount.Id, Description })` — a direct drop-in since the seeder already only ever used `SystemAccounts.CashId` as the source (depends on T018)
- [X] T022 [US2] Update `src/StageFright.Core/Modules/Finance/FinanceMenuItemProvider.cs`: remove the old `Title = "Transfers", Route = "/finance/transfers"` sub-item, leaving the "Record Bank Deposit" entry added in T013 as the sole entry in that nav slot, and update the class's doc-comment accordingly (depends on T013, T018, T019)
- [ ] T023 [US2] Manually run the quickstart.md Story 2 steps (bank-deposit-specific form, Journal Entry still supports arbitrary account-to-account moves, no-eligible-destination warning, blank-description default) against a running `dotnet run --project src/StageFright.App/` instance

**Checkpoint**: User Stories 1 AND 2 both work — bank deposits are recorded via a single dedicated workflow, and Journal Entry is unaffected.

---

## Phase 5: User Story 3 - Historical transfers remain accurate in reports (Priority: P3)

**Goal**: Pre-existing `Transfer`-typed journal entries and their GL rows continue to display correctly in financial reports after the refactor ships — no reclassification or migration of historical data occurs.

**Independent Test**: Generate an Account Register report covering a date range that includes a pre-refactor `Transfer`-typed entry and confirm it displays unchanged (same accounts, amounts, dates, descriptions) — independent of whether User Story 1 or 2 has been implemented, since report providers read `Transaction`/`Account` rows directly and never branch on `JournalEntryType`.

### Tests for User Story 3

- [X] T024 [P] [US3] Add a test method to `tests/StageFright.Integration.Tests/Scenarios/V6_AccountingReportsTests.cs`: seed one `JournalEntry{Type=JournalEntryType.Transfer}` with its balanced `Transaction` pair directly against the DB (representing pre-refactor historical data — no service call, no reclassification), separately post one `JournalEntry{Type=JournalEntryType.BankDeposit}` pair, then run `AccountRegisterReportProvider`/`TrialBalanceReportProvider` (reusing the file's existing `BuildAccountRegisterProvider`/`BuildTrialBalanceProvider` helpers) and assert: the historical `Transfer` entry appears with its original accounts/amounts/date unchanged, the new `BankDeposit` entry appears correctly, and Trial Balance still balances across both entry types together

### Implementation for User Story 3

**None required.** Per research.md §1, no report provider or repository branches on `JournalEntryType` — this story is verification-only.

- [ ] T025 [US3] Manually run the quickstart.md Story 3 steps (Account Register historical accuracy, Trial Balance still balances) against a running `dotnet run --project src/StageFright.App/` instance, ideally against a database containing `Transfer`-typed entries seeded before this feature's changes

**Checkpoint**: All three user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final verification across all three stories together.

- [X] T026 Run `dotnet build` and the full `dotnet test` (all five test projects, without `--no-build`) from the repo root and confirm everything is green, per this repo's build/test verification rule
- [ ] T027 Re-run the full quickstart.md (all three stories together) once more to confirm no regression where the changes interact (e.g. recording a deposit via `/finance/bank-deposit`, then confirming Account Register/Trial Balance reflect it alongside untouched historical `Transfer` entries)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — run first.
- **Foundational (Phase 2)**: Depends on Setup completion. `T002` (enum member) blocks all of User Story 1 (new writes need `JournalEntryType.BankDeposit` to exist) and is read by User Story 3's regression test.
- **User Story 1 (Phase 3)**: Depends only on Foundational (T002).
- **User Story 2 (Phase 4)**: Depends on **User Story 1 being complete** (T003–T014) — it retires the `TransferPage`/`AccountTransferService` that User Story 1's `BankDepositPage`/`BankDepositService` replace, and finalizes the menu entry User Story 1 added in T013. This is a genuine sequential dependency, unlike a typical file-disjoint P1/P2 pair.
- **User Story 3 (Phase 5)**: Depends only on Foundational (T002) — it seeds its own historical `Transfer` row directly and never calls `BankDepositService`/`BankDepositPage`, so it can be implemented and run in parallel with User Story 1 and/or User Story 2 by a second developer, or at any point after Phase 2.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: No dependency on User Story 2 or 3. Fully self-contained new addition (new service + contract + request model + page, additive menu entry).
- **User Story 2 (P2)**: Depends on User Story 1 (see above). No dependency on User Story 3.
- **User Story 3 (P3)**: No dependency on User Story 1 or 2 — self-contained report-regression test using directly-seeded data.

### Within Each User Story

- Tests (T003–T005 for US1, T015–T017 for US2, T024 for US3) are written first and must fail before their corresponding implementation tasks land.
- Within US1: request model (T006) and contract (T007) before the service (T008); service before DI registration (T009); registration and the view model (T010) before the page code-behind (T011); code-behind before the markup (T012); page before the menu entry (T013).
- Within US2: the old-code deletions (T018, T019) can happen in either order or in parallel, but both must land before the `MauiProgram.cs`/`DebugDataSeeder.cs` updates that depend on the deleted types no longer existing (T020, T021) and before the final menu cleanup (T022, which also depends on T013 from US1).

### Parallel Opportunities

- T003, T004, T005 (US1 tests, different files) can run in parallel.
- T006 and T007 (US1 request model and contract, different files) can run in parallel; T010 (US1 view model) can run in parallel with T006–T009 since the page model has no dependency on the service.
- T015, T016, T017 (US2 test updates, different files) can run in parallel.
- T018 and T019 (US2 deletions — Core service files vs. UI page files) can run in parallel.
- T020 and T021 (US2 `MauiProgram.cs` and `DebugDataSeeder.cs` updates, different files, both only depend on T018) can run in parallel.
- T024 (US3) can run in parallel with all of User Story 1 and User Story 2, since it shares no files and no runtime dependency with either.

---

## Parallel Example: User Story 1

```bash
# Launch all three US1 tests together:
Task: "Write BankDepositServiceTests in tests/StageFright.Core.Tests/Modules/Finance/BankDepositServiceTests.cs"
Task: "Write V17_BankDepositTests in tests/StageFright.Integration.Tests/Scenarios/V17_BankDepositTests.cs"
Task: "Write BankDepositPageTests in tests/StageFright.UI.Tests/Pages/Finance/BankDepositPageTests.cs"

# Launch the independent new-file tasks together:
Task: "Create RecordBankDepositRequest in src/StageFright.Core/Modules/Finance/RecordBankDepositRequest.cs"
Task: "Create IBankDepositService contract in src/StageFright.Core/Contracts/IBankDepositService.cs"
Task: "Create BankDepositModel in src/StageFright.UI/Pages/Finance/BankDepositModel.cs"
```

## Parallel Example: User Story 2

```bash
# Launch all three US2 test updates together:
Task: "Update FinanceMenuItemProviderTests in tests/StageFright.Core.Tests/Modules/Finance/FinanceMenuItemProviderTests.cs"
Task: "Delete AccountTransferServiceTests.cs in tests/StageFright.Core.Tests/Modules/Finance/"
Task: "Update V14_ExpensesTransfersTests.cs Transfers region in tests/StageFright.Integration.Tests/Scenarios/"

# Launch both deletions together:
Task: "Delete AccountTransferService/IAccountTransferService/RecordTransferRequest in src/StageFright.Core/"
Task: "Delete TransferPage.razor/.razor.cs/TransferModel.cs in src/StageFright.UI/Pages/Finance/"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001).
2. Complete Phase 2: Foundational (T002).
3. Complete Phase 3: User Story 1 (T003–T014).
4. **STOP and VALIDATE**: Run quickstart.md Story 1 steps and the full test suite; confirm deposits post correctly end-to-end.
5. Deploy/demo if ready — this alone delivers SC-001/SC-002.

### Incremental Delivery

1. Setup + Foundational → baseline confirmed green, enum in place.
2. Add User Story 1 → test independently → demo (MVP!). The old Transfer page still exists at this point.
3. Add User Story 2 → test independently → demo. The old Transfer page/service is now gone; one workflow remains.
4. Add User Story 3 → test independently → demo (can be done any time after Foundational, even before US1/US2).
5. Polish (Phase 6) → final combined regression pass.

### Solo Developer Strategy

Recommended order: **US1 first** (it's the core new capability and the higher-priority story), then **US2** (retirement/cleanup, which structurally depends on US1 existing), with **US3** interleaved at any convenient point since it has no file or runtime overlap with either — then Phase 6 polish.

---

## Notes

- [P] tasks = different files, no dependencies on each other.
- [Story] label maps each task to US1, US2, or US3 for traceability back to spec.md.
- Every reachable code path introduced or changed here (service validation ×4, successful posting, default description, audit log, UI loading/warning/validation/success states, menu label, old-file retirement, historical-data report parity) has a corresponding test task per constitution §11 — do not skip T003–T005, T015–T017, or T024.
- Commit after each checkpoint (end of Phase 3, end of Phase 4, end of Phase 5, end of Phase 6) per this repo's commit-workflow rule (CLAUDE.md: stage and commit all changed/new files at the end of a task with a descriptive message).
- Run `dotnet build` and `dotnet test` (without `--no-build`) after any non-trivial group of changes, not just at T001/T026 — catch regressions early, especially around T018–T022 where old and new code are swapped out.
