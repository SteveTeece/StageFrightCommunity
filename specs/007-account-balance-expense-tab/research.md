# Phase 0 Research: Chart of Accounts Balance Column & Record Expense Tab

## 1. Balance calculation source and method

**Decision**: Compute each account's balance via `IGLRepository.GetAccountBalanceAsync(accountId, asAt, ct)`, called once per account (`asAt = DateTime.UtcNow`), inside a new `AccountBalanceService` in `StageFright.Core/Modules/Finance/`.

**Rationale**:
- `GetAccountBalanceAsync` already exists (`src/StageFright.Data/Repositories/GLRepository.cs:91`) and returns exactly `Σdebits − Σcredits` for one account as of a point in time — the same inception-to-date figure already used by `BalanceSheetReportProvider` (`src/StageFright.Reports/Providers/BalanceSheetReportProvider.cs:117`). Reusing it guarantees FR-004 (agreement with existing reports) with zero new GL logic.
- `BalanceSheetReportProvider.SectionAsync` already loops over every account in a type and calls `GetAccountBalanceAsync` once per account (an N+1 query per report render). The spec's edge case explicitly asks for parity with "existing account-balance calculations elsewhere in the system", so matching this established pattern — rather than introducing a new bulk-query path — is the consistent choice, not a regression.
- A single bulk query (e.g. grouping all transactions by `AccountId` with no date filter, mirroring `GetAccountMovementsAsync`) was considered for performance, but rejected: it can only fail or succeed as a whole, which would prevent the per-row error isolation FR-012 requires (see §3). The existing per-account pattern already provides that isolation for free.

**Alternatives considered**:
- New bulk `GetAccountBalancesAsync(asAt)` method returning `IReadOnlyDictionary<Guid, decimal>` in one query — more efficient, but fails FR-012's per-row isolation and duplicates a second "as of now" code path alongside the existing per-account one. Rejected for this feature; can be revisited later if the per-account loop proves too slow in practice (no evidence of that today — Trial Balance and Balance Sheet already use the same pattern in production).

## 2. Sign convention for the displayed Balance

**Decision**: Reuse the `creditNormal` flip already implemented in `BalanceSheetReportProvider.SectionAsync` (`src/StageFright.Reports/Providers/BalanceSheetReportProvider.cs:118`): `displayed = creditNormal ? -netDebit : netDebit`.
- Debit-normal (display raw `netDebit`): **Asset**, **Expense**.
- Credit-normal (display `-netDebit`): **Liability**, **Equity**, **Income**.

**Rationale**: This is the only sign convention already established in the codebase for a per-account "current balance" figure, and it directly explains the spec's edge case ("an Income or Liability account showing a net debit" is the abnormal case — normal for those types is a credit position, which under this convention displays as a positive number). Introducing a different convention for the Chart of Accounts would make its Balance column disagree in sign with the Balance Sheet for the same account, undermining FR-004/SC-003.

**Alternatives considered**:
- Always display raw `netDebit` (no flip) — rejected because Liability/Equity/Income accounts would routinely show as negative numbers in ordinary operation, which is confusing and inconsistent with the Balance Sheet's presentation of the same accounts.

## 3. Per-row error isolation (FR-012)

**Decision**: `AccountBalanceService` computes each account's balance inside a per-account `try/catch`. On success the row carries a `decimal Balance`; on failure the row carries `HasError = true` and no numeric value, and the exception is logged (Serilog) with the account id — the loop continues to the next account rather than aborting.

**Rationale**: Matches constitution §5 (custom exceptions must be logged, failures must degrade gracefully) and directly implements FR-012 / SC-002 (every row shows either a value or an inline error indicator, never blank, and one bad account never blanks the grid). The per-account loop from §1 makes this isolation a small addition (a try/catch per iteration) rather than a structural change.

**Alternatives considered**:
- Wrap the whole load in one try/catch — rejected, this fails the entire grid for one account's problem, which is the exact failure mode FR-012 rules out.

## 4. View model and grid binding

**Decision**: Add a small view-model class `AccountBalance` (Account id, name, number, type, IsBankAccount, IsSystem, `decimal? Balance`, `bool HasError`) returned by `AccountBalanceService.GetAllAccountBalancesAsync()` (active) and a parallel call for archived accounts. `ChartOfAccountsPage` binds its two `RadzenDataGrid`s to `List<AccountBalance>` instead of `List<Account>`, adding a `RadzenDataGridColumn Property="Balance" Title="Balance" FormatString="{0:C}"` column.

**Rationale**: This exactly mirrors the existing `MemberBalance` view model (`src/StageFright.Core/Modules/Finance/MemberBalance.cs`) used by `MemberBalanceList.razor`, which already binds a `RadzenDataGrid` to a computed `Balance` property with `Property="Balance"` for built-in client-side sorting (FR-006) and `FormatString="{0:C}"` for currency formatting (FR-003). No new grid pattern is introduced.

**Alternatives considered**:
- Add a transient, `[NotMapped]` `Balance` property directly onto the `Account` entity — rejected: it would blur a domain entity with a UI/report concern and contradicts the existing precedent of using a dedicated view-model class (`MemberBalance`) for exactly this situation.
- Keep binding to `List<Account>` and compute balances in a side dictionary looked up per-cell in a `<Template>` — rejected: `RadzenDataGrid` sorting operates on bound properties, so a dictionary lookup in a template would not participate in `AllowSorting` (FR-006) without extra custom sort logic.

## 5. Record Expense tab integration

**Decision**: Add a new `<Tab Title="Record Expense" OnShown="@(() => NavToTab("record-expense"))">` to `FinancePage.razor`, rendering the existing `<ExpensePaymentPage />` component directly (no extraction, no new component file). Update `FinancePage.razor.cs`'s `DefaultTabIndex` switch to insert `"record-expense" => 3` before `"annual-fees" => 4` (shifted from 3).

**Rationale**: `ExpensePaymentPage` (`src/StageFright.UI/Pages/Finance/ExpensePaymentPage.razor`) already carries `@page "/finance/expenses"`. In Blazor, a component decorated with `@page` remains a normal component and can be embedded as a child anywhere (the route attribute only adds routing metadata) — so it can be dropped straight into the new tab's `<Content>` without any refactor. This satisfies FR-009 (identical form/validation/behavior, since it's literally the same component) and FR-010 (the direct route keeps working, since nothing about the page's own routing changes) with the smallest possible diff.

**Alternatives considered**:
- Extract the form into a shared `RecordExpense.razor`/`.razor.cs` component (no `@page`) used by both a slimmed-down `ExpensePaymentPage` wrapper and the new tab — this is the pattern `RecordIncome` already uses, but `RecordIncome` was never independently routable, so it needed the split to be embeddable at all. `ExpensePaymentPage` doesn't have that problem, so the extra split would be pure duplication for no behavioral gain. Rejected per "simple over clever" / no premature abstraction (CLAUDE.md).

## 6. Finance menu change (FR-007)

**Decision**: Remove the `new MenuItem { Title = "Record Expense", Route = "/finance/expenses", DisplayOrder = 2 }` entry from `FinanceMenuItemProvider.GetMenuItems()` (`src/StageFright.Core/Modules/Finance/FinanceMenuItemProvider.cs:28`). The route `/finance/expenses` itself is untouched — only the menu's sub-item list changes.

**Rationale**: Directly implements FR-007/SC-005. Since `ExpensePaymentPage` keeps its own `@page` directive (§5), removing the menu entry cannot break FR-010 (the route stays directly reachable — it's simply no longer advertised in the nav menu).

## 7. Test coverage approach

**Decision**:
- `AccountBalanceServiceTests` (unit, `StageFright.Core.Tests`) mirroring `MemberBalanceServiceTests` — success, zero-activity account, per-account exception isolation, sign convention per `AccountType`.
- `ChartOfAccountsPageTests` (bUnit) additions — Balance column renders for active/archived rows, sorts, shows inline error indicator for a failed row.
- New `FinancePageTests` (bUnit) — tab order includes "Record Expense" between "Record Income" and "Apply Annual Fees", selecting it renders the expense form, `NavToTab` updates the query string, reload/back-forward preserves the selected tab (via `TabQuery`/`DefaultTabIndex`).
- Extend or add `FinanceMenuItemProviderTests` confirming "Record Expense" is absent from sub-items.
- Existing `V14_ExpensesTransfersTests` and any Trial-Balance/Balance-Sheet acceptance tests remain the parity oracle for FR-004/SC-003 (Balance column figures must match).

**Rationale**: Every new/changed code path (service success, per-account failure, sign flip per type, tab wiring, menu removal, direct-route survival) gets deterministic coverage per constitution §11, following the project's established `Should_[ExpectedBehavior]_When_[Condition]` naming and existing sibling test files as templates.
