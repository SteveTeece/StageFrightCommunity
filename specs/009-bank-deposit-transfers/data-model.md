# Phase 1 Data Model: Bank Deposit Recording

No new persisted entities or database tables are introduced. One existing enum gains a member (no EF Core migration required — see research.md §1); one existing request/response-style model is replaced by its bank-deposit equivalent.

## Existing entities used (unchanged)

### Account (`StageFright.Core/Entities/Account.cs`)
Unchanged. `IsBankAccount` continues to distinguish bank/cash accounts for the destination picker; `SystemAccounts.CashId`/`SystemAccounts.CashNumber` (`StageFright.Core/Modules/Finance/SystemAccounts.cs`) continue to identify the fixed Cash on Hand source.

### Transaction (`StageFright.Core/Entities/Transaction.cs`)
Unchanged. A bank deposit posts exactly two rows here (Debit destination bank account / Credit Cash on Hand), same shape as every other GL pair in this system.

### JournalEntry (`StageFright.Core/Entities/JournalEntry.cs`)
Unchanged shape. Its `Type` field gains one new legal value.

## Modified enum

### JournalEntryType (`StageFright.Core/Enums/JournalEntryType.cs`)

| Member | Meaning | Status |
|---|---|---|
| `Income` | Non-member income deposited to a bank account | Unchanged |
| `ExpensePayment` | Money paid out of a bank account against an expense account | Unchanged |
| `Transfer` | Movement of funds between two bank accounts | Unchanged — historical rows only; no new rows use this value after this feature ships |
| `BankDeposit` | Cash on Hand deposited into a bank account | **NEW** — used for every deposit recorded via `BankDepositService` from this feature forward |
| `GeneralJournal` | Manually entered multi-line general journal | Unchanged |
| `OpeningBalance` | One-off opening balances posted by the opening balances wizard | Unchanged |

Persisted as a string (`HasConversion<string>()` in `JournalEntryConfiguration`), so this addition needs no migration.

## New request model

### RecordBankDepositRequest (new: `StageFright.Core/Modules/Finance/RecordBankDepositRequest.cs`)

Replaces `RecordTransferRequest` — same shape minus the source-account field, since the source is always Cash on Hand (research.md §3).

| Field | Type | Notes |
|---|---|---|
| `Date` | `DateTime` | Required. UTC date of the deposit. |
| `Amount` | `decimal` | Required, must be `> 0` (FR-004). |
| `ToAccountId` | `Guid` | Required. Id of the destination bank account. Must be a bank account (`IsBankAccount == true`) and must not equal `SystemAccounts.CashId` (FR-003). |
| `Description` | `string?` | Optional. Blank/whitespace defaults to `"Bank deposit — {destination account name}"` (FR-005). |

**Validation rules** (enforced by `BankDepositService.RecordDepositAsync`, in order):
1. `Amount <= 0m` → `ValidationException`.
2. `ToAccountId` not found among existing accounts → `EntityNotFoundException`.
3. Destination account `IsBankAccount == false` → `ValidationException`.
4. Destination account id `== SystemAccounts.CashId` → `ValidationException`.

**State transitions**: None — this is a write-once request object; the resulting `JournalEntry`/`Transaction` rows are immutable per constitution §3.4/§3.6.

## Service contract

### IBankDepositService (new: `StageFright.Core/Contracts/IBankDepositService.cs`)

```csharp
public interface IBankDepositService
{
    /// <summary>
    /// Records a bank deposit with a matching GL pair under a BankDeposit journal entry:
    /// Debit the destination bank account / Credit Cash on Hand. The destination account
    /// must be a bank account other than Cash on Hand.
    /// Throws <see cref="Core.Exceptions.ValidationException"/> on bad input,
    /// <see cref="Core.Exceptions.EntityNotFoundException"/> if the destination doesn't exist.
    /// </summary>
    Task RecordDepositAsync(RecordBankDepositRequest request, CancellationToken ct = default);
}
```

Implemented by `BankDepositService` in `StageFright.Core/Modules/Finance/`, depending on `IAccountRepository`, `IGLRepository`, `IJournalEntryRepository`, `IAuditTrailService`, `IUnitOfWork` — the identical dependency set `AccountTransferService` already used. Registered in `MauiProgram.RegisterCoreServices` in place of the retired `IAccountTransferService` registration.

## Relationships

```
BankDepositPage (UI)
   │  binds BankDepositModel, calls IBankDepositService.RecordDepositAsync
   ▼
BankDepositService
   │  reads Account (destination + SystemAccounts.CashId), writes:
   ├──▶ JournalEntry { Type = BankDeposit }
   └──▶ Transaction ×2 (Debit destination / Credit Cash on Hand), via IGLRepository.AddBalancedSetAsync
```

No changes to `Fee`, `Payment`, `Account`, GL balance-calculation logic, `GeneralJournalService`, or any report provider. The Journal Entry workflow (`GeneralJournalService`/`JournalEntryPage`) is entirely untouched and remains the place to move funds between any two arbitrary accounts (spec Assumptions).
