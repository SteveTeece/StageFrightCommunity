# Contract: IAccountBalanceService

This documents the internal contract between the new `AccountBalanceService` (`StageFright.Core/Modules/Finance/`) and its consumer, `ChartOfAccountsPage` (`StageFright.UI/Pages/Finance/`). No public/external API is exposed by this application (desktop MAUI Blazor app, no HTTP surface), so this is the closest analog for an application service that a UI page depends on through DI — the same pattern already established by `IMemberBalanceService`/`MemberBalanceList`.

## Interface

```csharp
namespace StageFright.Core.Contracts;

public interface IAccountBalanceService
{
    Task<IReadOnlyList<AccountBalance>> GetActiveAccountBalancesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AccountBalance>> GetArchivedAccountBalancesAsync(CancellationToken ct = default);
}
```

`AccountBalance` is defined in `data-model.md` §"New view model".

## Contract rules

1. **Ordering**: Both methods return rows ordered by `AccountNumber` ascending — the same order `ChartOfAccountsPage` already applies to its `Account` lists today (`OrderBy(a => a.AccountNumber)`), so the page's existing sort behavior is unaffected before any user-driven grid sort is applied.
2. **Completeness (SC-002)**: Every non-archived account MUST appear in `GetActiveAccountBalancesAsync`'s result, and every archived account MUST appear in `GetArchivedAccountBalancesAsync`'s result — with either a numeric `Balance` or `HasError = true`. No account is ever silently omitted because its balance calculation failed.
3. **Per-row isolation (FR-012)**: A failure computing one account's balance (any exception from the underlying `IGLRepository.GetAccountBalanceAsync` call) MUST be caught per-account, logged via Serilog with the account id and operation name, and reflected as `HasError = true` / `Balance = null` on that row only. It MUST NOT prevent the method from returning results for every other account.
4. **Sign convention (research.md §2)**: `Balance` is `Σdebits − Σcredits` as of `DateTime.UtcNow`, sign-flipped (`-value`) for credit-normal types (`Liability`, `Equity`, `Income`); left as-is for debit-normal types (`Asset`, `Expense`).
5. **No caching**: Each call recomputes from the GL fresh (FR-005 — "reflect the most recently posted transactions each time the Chart of Accounts screen is loaded"). The service MUST NOT cache results across calls.
6. **Parity with reports (FR-004/SC-003)**: Because the calculation delegates to the same `IGLRepository.GetAccountBalanceAsync` used by `BalanceSheetReportProvider`, a given account's `Balance` here MUST always equal that account's balance-sheet figure computed `asAt` the same instant — this is enforced by construction (shared repository method), not by a separate reconciliation step.

## Consumer contract (`ChartOfAccountsPage`)

- Calls `GetActiveAccountBalancesAsync` and `GetArchivedAccountBalancesAsync` once in `OnInitializedAsync` (mirroring today's `LoadAccountsAsync` calling `AccountService.GetAllAsync`/`GetArchivedAsync`), replacing the two `List<Account>` fields with `List<AccountBalance>`.
- Binds each `RadzenDataGrid` to the resulting `List<AccountBalance>` and adds a `Balance` column with `Property="Balance"` (enables `AllowSorting`, FR-006) and `FormatString="{0:C}"` (FR-003).
- Renders an inline error indicator (e.g. `—` with a title/tooltip) in the Balance cell wherever `HasError == true`, via a `<Template>` on that column, leaving every other cell in the row (account number, name, type, actions) rendering normally (FR-012).
- Existing type-filter, rename, archive, and restore behavior is unaffected — those operate on the same underlying `Account` fields now reachable through `AccountBalance`.
