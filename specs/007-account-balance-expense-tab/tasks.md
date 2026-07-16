---

description: "Task list template for feature implementation"
---

# Tasks: Chart of Accounts Balance Column & Record Expense Tab

**Input**: Design documents from `/specs/007-account-balance-expense-tab/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included and REQUIRED, not optional — this project's constitution (§11, "Non-Negotiable Coverage Rule") and CLAUDE.md mandate exhaustive automated coverage of every reachable code path before merge, overriding the default "tests are optional" behavior.

**Organization**: Tasks are grouped by user story (US1 = P1 Balance column, US2 = P2 Record Expense tab) to enable independent implementation and testing of each story, per spec.md.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2)
- File paths are exact and relative to the repository root

## Path Conventions

Single MAUI Blazor Hybrid solution (`StageFrightCommunity.slnx`): `src/StageFright.Core/`, `src/StageFright.UI/`, `src/StageFright.App/`, `tests/StageFright.Core.Tests/`, `tests/StageFright.UI.Tests/`, `tests/StageFright.Integration.Tests/` — see plan.md Project Structure.

---

## Phase 1: Setup

**Purpose**: Confirm a clean baseline before making changes. No new project, package, or scaffolding is needed — every dependency (EF Core, Radzen.Blazor, Blazor.Bootstrap, Serilog, xUnit, NSubstitute, bUnit) is already installed and used by sibling code this feature mirrors.

- [X] T001 Run `dotnet build` and `dotnet test` from the repo root and confirm both are green before starting, establishing the baseline this feature must not regress

---

## Phase 2: Foundational

**Purpose**: Blocking prerequisites shared by all user stories.

**None required.** Per research.md and data-model.md, User Story 1 (new `AccountBalanceService`/`IAccountBalanceService`/`AccountBalance`) and User Story 2 (Finance Overview tab + menu edit) touch entirely disjoint files and introduce no shared new infrastructure, entities, or migrations. Both stories depend only on pre-existing code (`IGLRepository`, `IAccountRepository`, `IAccountService`, `ExpensePaymentPage`, the `Tabs`/`Tab` components) that already works today. Proceed directly to Phase 3.

---

## Phase 3: User Story 1 - Balance column in Chart of Accounts (Priority: P1) 🎯 MVP

**Goal**: Every account row in the Chart of Accounts (Active and Archived) shows its current GL-derived balance, sortable, currency-formatted, matching Trial Balance/Balance Sheet figures, with per-row error isolation if a balance can't be computed.

**Independent Test**: Open the Chart of Accounts screen and confirm a Balance value (or inline error indicator) is shown for every listed account, matching the figures on the Trial Balance report for the same accounts — no dependency on User Story 2.

### Tests for User Story 1 (write first; confirm they fail before implementation)

- [X] T002 [P] [US1] Write `AccountBalanceServiceTests` in `tests/StageFright.Core.Tests/Modules/Finance/AccountBalanceServiceTests.cs` covering: correct balance for an account with activity, zero balance for an account with no activity, debit-normal sign (Asset/Expense) vs credit-normal sign (Liability/Equity/Income), and per-account exception isolation (`HasError=true`/`Balance=null` on the failing row only, other rows unaffected) — mirror `tests/StageFright.Core.Tests/Modules/Finance/MemberBalanceServiceTests.cs`'s structure and NSubstitute usage
- [X] T003 [P] [US1] Write acceptance test `V16_ChartOfAccountsBalanceTests` in `tests/StageFright.Integration.Tests/Scenarios/V16_ChartOfAccountsBalanceTests.cs` against a real SQLite in-memory DB (mirror `V7_AccountManagementTests.cs`'s `IAsyncLifetime` setup), asserting `AccountBalanceService` balances agree with `BalanceSheetReportProvider`/`TrialBalanceReportProvider` figures for the same accounts after posting sample GL activity (FR-004/SC-003)

### Implementation for User Story 1

- [X] T004 [P] [US1] Create `AccountBalance` view model in `src/StageFright.Core/Modules/Finance/AccountBalance.cs` per data-model.md (`AccountId`, `AccountNumber`, `Name`, `Type`, `IsSystem`, `IsBankAccount`, `decimal? Balance`, `bool HasError`) — mirror `src/StageFright.Core/Modules/Finance/MemberBalance.cs`
- [X] T005 [P] [US1] Create `IAccountBalanceService` contract in `src/StageFright.Core/Contracts/IAccountBalanceService.cs` per contracts/account-balance-service-contract.md (`GetActiveAccountBalancesAsync`, `GetArchivedAccountBalancesAsync`) — mirror `src/StageFright.Core/Contracts/IMemberBalanceService.cs`
- [X] T006 [US1] Implement `AccountBalanceService` in `src/StageFright.Core/Modules/Finance/AccountBalanceService.cs` (depends on T004, T005): for each account from `IAccountRepository.GetAllAsync`/`GetArchivedAsync`, call `IGLRepository.GetAccountBalanceAsync(account.Id, DateTime.UtcNow, ct)` inside a per-account try/catch, apply the credit-normal sign flip (research.md §2) for `Liability`/`Equity`/`Income`, log any per-account failure via Serilog and set `HasError=true`, order results by `AccountNumber`
- [X] T007 [US1] Register `IAccountBalanceService` → `AccountBalanceService` in `src/StageFright.App/MauiProgram.cs`'s `RegisterCoreServices`, alongside the existing `IMemberBalanceService`/`IAccountService` registrations (depends on T006)
- [X] T008 [US1] Update `src/StageFright.UI/Pages/Finance/ChartOfAccountsPage.razor.cs`: inject `IAccountBalanceService`, replace the `_accounts`/`_archivedAccounts` `List<Account>` fields with `List<AccountBalance>` populated from `GetActiveAccountBalancesAsync`/`GetArchivedAccountBalancesAsync` in `LoadAccountsAsync`, and adjust `FilteredAccounts`/rename/archive/restore calls to read the underlying `AccountId`/`Type` from `AccountBalance` (depends on T007)
- [X] T009 [US1] Update `src/StageFright.UI/Pages/Finance/ChartOfAccountsPage.razor`: change both `RadzenDataGrid`s' `TItem` to `AccountBalance`, add a `RadzenDataGridColumn Property="Balance" Title="Balance" FormatString="{0:C}"` column to each grid (enabling sort per FR-006), and render an inline error indicator (e.g. `—` with a `title` tooltip) via a `<Template>` on that column wherever `HasError` is true, leaving every other cell in the row unaffected (FR-012) (depends on T008)
- [X] T010 [P] [US1] Update `tests/StageFright.UI.Tests/Pages/Finance/ChartOfAccountsPageTests.cs`: add a `IAccountBalanceService` substitute, assert the Balance column renders currency values for active and archived rows, assert sorting by Balance reorders rows, and assert the inline error indicator renders for a row with `HasError=true` without blanking the rest of that row or any other row (depends on T009)
- [X] T011 [US1] Manually run the Story 1 steps in quickstart.md (Trial Balance parity, zero-activity account, archived-account balance, column sort, fresh-load recompute, static/no drill-through) against a running `dotnet run --project src/StageFright.App/` instance

**Checkpoint**: User Story 1 is fully functional and independently testable — the Chart of Accounts shows balances end-to-end. This is the MVP.

---

## Phase 4: User Story 2 - Record Expense tab on Finance Overview (Priority: P2)

**Goal**: "Record Expense" is reachable as a tab on the Finance Overview screen (positioned after "Record Income", before "Apply Annual Fees") with identical form/validation/behavior, no longer appears as a standalone Finance-menu sub-item, and its direct route (`/finance/expenses`) keeps working.

**Independent Test**: Open the Finance Overview screen, select the new Record Expense tab, and successfully record an expense entirely from that screen — independent of whether the Balance column (User Story 1) has been implemented.

### Tests for User Story 2 (write first; confirm they fail before implementation)

- [X] T012 [P] [US2] Create `FinancePageTests` in `tests/StageFright.UI.Tests/Pages/Finance/FinancePageTests.cs` (new bUnit test file — none exists today): assert the tab order is Outstanding, Record Member Payment, Record Income, Record Expense, Apply Annual Fees; assert selecting the Record Expense tab renders the expense form; assert selecting it triggers `NavigationManager.NavigateTo("/finance?tab=record-expense", ...)`; assert `?tab=record-expense` in the initial URL selects that tab on load (`DefaultTabIndex`)
- [X] T013 [P] [US2] Create `FinanceMenuItemProviderTests` in `tests/StageFright.Core.Tests/Modules/Finance/FinanceMenuItemProviderTests.cs` (new test file — none exists today): assert `GetMenuItems()`'s Finance sub-items no longer include a `Title == "Record Expense"` entry, and that `Chart of Accounts`/`Transfers`/`Journal Entries`/`Reconciliation`/`Opening Balances` are still present

### Implementation for User Story 2

- [X] T014 [US2] Add a new `<Tab Title="Record Expense" OnShown="@(() => NavToTab("record-expense"))">` to `src/StageFright.UI/Pages/Finance/FinancePage.razor`, positioned after the "Record Income" tab and before "Apply Annual Fees", rendering `<ExpensePaymentPage />` directly in its `<Content>` (no new component, no extraction — per research.md §5)
- [X] T015 [US2] Update `src/StageFright.UI/Pages/Finance/FinancePage.razor.cs`'s `OnInitialized` `DefaultTabIndex` switch: insert `"record-expense" => 3`, shift `"annual-fees"` from `3` to `4` (depends on T014)
- [X] T016 [P] [US2] Remove the `new MenuItem { Title = "Record Expense", Route = "/finance/expenses", DisplayOrder = 2 }` entry from `GetMenuItems()` in `src/StageFright.Core/Modules/Finance/FinanceMenuItemProvider.cs`, and update its class doc-comment to reflect the new tab list (Outstanding / Record Member Payment / Record Income / Record Expense / Apply Annual Fees)
- [X] T017 [US2] Manually run the Story 2 steps in quickstart.md (tab present and positioned, full functionality on the tab, menu no longer lists it, direct route `/finance/expenses` still works standalone, tab state survives reload/back-forward) against a running `dotnet run --project src/StageFright.App/` instance

**Checkpoint**: User Stories 1 AND 2 both work independently — Chart of Accounts shows balances, and Record Expense is reachable both as a Finance Overview tab and via its direct route.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Final verification across both stories together.

- [X] T018 Run `dotnet build` and the full `dotnet test` (all five test projects, without `--no-build`) from the repo root and confirm everything is green, per this repo's build/test verification rule
- [X] T019 [P] Re-run the full quickstart.md (both stories together) once more to confirm no regression where the two changes interact (e.g. recording an expense via the new tab, then confirming the affected account's Balance updates on next Chart of Accounts load — FR-005)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — run first.
- **Foundational (Phase 2)**: None — skipped (see note above).
- **User Stories (Phase 3, 4)**: Both depend only on Setup (T001) completing. They touch fully disjoint files (`StageFright.Core/Modules/Finance/Account*`, `StageFright.Core/Contracts/IAccountBalanceService.cs`, `ChartOfAccountsPage.*` for US1; `FinancePage.*`, `FinanceMenuItemProvider.cs` for US2) and can proceed in either order or in parallel.
- **Polish (Phase 5)**: Depends on both User Story 1 and User Story 2 being complete.

### User Story Dependencies

- **User Story 1 (P1)**: No dependency on User Story 2. Fully self-contained (new service + view model + `ChartOfAccountsPage` changes).
- **User Story 2 (P2)**: No dependency on User Story 1. Fully self-contained (`FinancePage` tab + `FinanceMenuItemProvider` edit, reusing the existing `ExpensePaymentPage` component unchanged).

### Within Each User Story

- Tests (T002/T003 for US1, T012/T013 for US2) are written first and must fail before their corresponding implementation tasks land.
- Within US1: view model (T004) and contract (T005) before service (T006); service before DI registration (T007); registration before the page's data-loading change (T008); data-loading before the grid/column markup change (T009); UI change before UI test update (T010).
- Within US2: the new tab markup (T014) before the tab-index mapping update (T015), since T015 depends on the tab actually existing at a given position. The menu edit (T016) has no file overlap with T014/T015 and can happen at any point relative to them.

### Parallel Opportunities

- T002 and T003 (US1 tests, different files) can run in parallel.
- T004 and T005 (US1 view model and contract, different files) can run in parallel.
- T012 and T013 (US2 tests, different files) can run in parallel.
- T016 (US2 menu edit) can run in parallel with T014/T015 (US2 tab edit) — different files.
- Once Setup (T001) is done, all of User Story 1's tasks and all of User Story 2's tasks can proceed in parallel as two independent workstreams, since the two stories share no files.

---

## Parallel Example: User Story 1

```bash
# Launch both US1 tests together:
Task: "Write AccountBalanceServiceTests in tests/StageFright.Core.Tests/Modules/Finance/AccountBalanceServiceTests.cs"
Task: "Write V16_ChartOfAccountsBalanceTests in tests/StageFright.Integration.Tests/Scenarios/V16_ChartOfAccountsBalanceTests.cs"

# Then launch the two independent new-file tasks together:
Task: "Create AccountBalance view model in src/StageFright.Core/Modules/Finance/AccountBalance.cs"
Task: "Create IAccountBalanceService contract in src/StageFright.Core/Contracts/IAccountBalanceService.cs"
```

## Parallel Example: User Story 2

```bash
# Launch both US2 tests together:
Task: "Create FinancePageTests in tests/StageFright.UI.Tests/Pages/Finance/FinancePageTests.cs"
Task: "Create FinanceMenuItemProviderTests in tests/StageFright.Core.Tests/Modules/Finance/FinanceMenuItemProviderTests.cs"

# Menu edit can run alongside the tab edit:
Task: "Remove Record Expense MenuItem entry in src/StageFright.Core/Modules/Finance/FinanceMenuItemProvider.cs"
Task: "Add Record Expense tab in src/StageFright.UI/Pages/Finance/FinancePage.razor"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001).
2. Complete Phase 3: User Story 1 (T002–T011).
3. **STOP and VALIDATE**: Run quickstart.md Story 1 steps and the full test suite; confirm the Balance column works end-to-end and matches Trial Balance/Balance Sheet.
4. Deploy/demo if ready — this alone delivers SC-001/SC-002/SC-003.

### Incremental Delivery

1. Setup → baseline confirmed green.
2. Add User Story 1 → test independently → demo (MVP!).
3. Add User Story 2 → test independently → demo.
4. Polish (Phase 5) → final combined regression pass.

### Solo Developer Strategy

Since both stories are file-disjoint, they can be implemented in either order without any rebasing/merge friction — e.g. finish User Story 1 completely (T002–T011, checkpoint), commit, then start User Story 2 (T012–T017) fresh, or interleave them if convenient. Recommended order: **US1 first** (higher priority per spec.md, and it's the more novel piece of work), then US2 (smaller, mostly composition/wiring), then Phase 5 polish.

---

## Notes

- [P] tasks = different files, no dependencies on each other.
- [Story] label maps each task to US1 or US2 for traceability back to spec.md.
- Every reachable code path introduced here (service success, per-account failure, sign flip per `AccountType`, tab wiring, menu removal, direct-route survival) has a corresponding test task per constitution §11 — do not skip T002/T003/T010/T012/T013.
- Commit after each checkpoint (end of Phase 3, end of Phase 4, end of Phase 5) per this repo's commit-workflow rule (CLAUDE.md: stage and commit all changed/new files at the end of a task with a descriptive message).
- Run `dotnet build` and `dotnet test` (without `--no-build`) after any non-trivial group of changes, not just at T001/T018 — catch regressions early.
