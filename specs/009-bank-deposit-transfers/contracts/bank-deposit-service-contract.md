# Contract: IBankDepositService

This documents the internal contract between the new `BankDepositService` (`StageFright.Core/Modules/Finance/`) and its consumer, `BankDepositPage` (`StageFright.UI/Pages/Finance/`). No public/external API is exposed by this application (desktop MAUI Blazor app, no HTTP surface), so this is the closest analog for an application service that a UI page depends on through DI — the same pattern already established by `IAccountTransferService`/`TransferPage` and `IIncomeEntryService`/`RecordIncome`, which this contract replaces/mirrors respectively.

## Interface

```csharp
namespace StageFright.Core.Contracts;

public interface IBankDepositService
{
    Task RecordDepositAsync(RecordBankDepositRequest request, CancellationToken ct = default);
}
```

`RecordBankDepositRequest` is defined in `data-model.md` §"New request model".

## Contract rules

1. **Fixed source (FR-002)**: The source/credit side of every posting is always `SystemAccounts.CashId` — there is no way for a caller to specify a different source; the request model has no `FromAccountId` field.
2. **Destination scope (FR-003)**: `request.ToAccountId` MUST identify an existing `Account` with `IsBankAccount == true` and an id other than `SystemAccounts.CashId`. Violating either throws `EntityNotFoundException` (missing account) or `ValidationException` (not a bank account, or equals Cash on Hand).
3. **Positive amount (FR-004)**: `request.Amount` MUST be `> 0`; zero or negative throws `ValidationException` before any GL write occurs.
4. **Default description (FR-005)**: When `request.Description` is null/whitespace, the posted `Transaction.Description`/`JournalEntry.Description` default to `"Bank deposit — {destination account name}"`. A supplied description is trimmed and used as-is.
5. **Balanced GL pair under a distinct classification (FR-006/FR-011)**: Exactly two `Transaction` rows are created — `DebitAmount = request.Amount` on the destination account, `CreditAmount = request.Amount` on `SystemAccounts.CashId` — grouped under one new `JournalEntry` with `Type = JournalEntryType.BankDeposit`. Both rows share the same `JournalEntryId`, `Date`, and `Description`. Posted via `IGLRepository.AddBalancedSetAsync` inside `IUnitOfWork.ExecuteInTransactionAsync`, so a `GLBalanceException` (mismatched debits/credits — should never occur given the two-row shape here) rolls back the entire operation and nothing partial is persisted.
6. **No sufficient-funds check**: A deposit is recorded even if it would drive Cash on Hand negative — this method never inspects the current Cash on Hand balance before posting (spec Edge Cases).
7. **Audit trail (FR-010)**: Every successful call writes exactly one `IAuditTrailService.LogAsync` entry (`AuditAction.Create`) referencing the new `JournalEntry` id, describing the amount, destination account name, and date.
8. **Historical data untouched (FR-009)**: This method never reads or writes any row with `JournalEntryType.Transfer`. Nothing about existing `Transfer`-typed entries changes as a side effect of calling this method.

## Consumer contract (`BankDepositPage`)

- Calls `IAccountService.GetBankAccountsAsync()` in `OnInitializedAsync` (mirroring today's `TransferPage`), then filters out `SystemAccounts.CashId` to build the destination picker's option list.
- Shows a "Cash on Hand" fixed-source label (not a picker) and a warning directing the user to `/finance/accounts` when the filtered destination list is empty (FR-007), analogous to `TransferPage`'s existing "need at least two bank/cash accounts" warning.
- On submit, builds a `RecordBankDepositRequest { Date, Amount, ToAccountId, Description }` and calls `IBankDepositService.RecordDepositAsync`; on success shows a confirmation message and a "Record Another" reset action, matching `TransferPage`'s existing UX pattern.
- Client-side validation mirrors the service's rules (amount required/positive, destination required) before the call is made, consistent with `TransferPage`'s existing `_errors` dictionary pattern.
