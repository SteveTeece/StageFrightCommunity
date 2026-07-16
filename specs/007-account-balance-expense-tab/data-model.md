# Phase 1 Data Model: Chart of Accounts Balance Column & Record Expense Tab

No new persisted entities, database tables, or EF Core migrations are introduced by this feature. It adds one new read-only, in-memory view-model type and reuses existing entities/repositories.

## Existing entities used (unchanged)

### Account (`StageFright.Core/Entities/Account.cs`)
Already has everything the Balance column needs to display alongside the balance: `Id`, `Name`, `AccountNumber`, `Type` (`AccountType`: Asset/Liability/Equity/Income/Expense), `IsSystem`, `IsBankAccount`, `IsDeleted`. No fields are added to this entity — balance is computed, not stored (per spec Key Entities: "no new attributes are stored on the entity itself").

### Transaction (`StageFright.Core/Entities/Transaction.cs`)
Immutable GL debit/credit rows already keyed by `AccountId`. Source data for the balance calculation; unchanged.

## New view model

### AccountBalance (new: `StageFright.Core/Modules/Finance/AccountBalance.cs`)

Read-only, in-memory aggregate — analogous to the existing `MemberBalance` view model — combining an `Account`'s display fields with its computed balance for grid binding.

| Field | Type | Notes |
|---|---|---|
| `AccountId` | `Guid` | `Account.Id` |
| `AccountNumber` | `string` | `Account.AccountNumber`, for the "No." column |
| `Name` | `string` | `Account.Name` |
| `Type` | `AccountType` | `Account.Type`, for the "Type" column and sign convention |
| `IsSystem` | `bool` | `Account.IsSystem`, drives the existing "System" badge / read-only actions |
| `IsBankAccount` | `bool` | `Account.IsBankAccount`, drives the existing "Bank" badge |
| `Balance` | `decimal?` | Computed: `Σdebits − Σcredits` as of now, sign-flipped for credit-normal types (Liability/Equity/Income). `null` when `HasError` is true. |
| `HasError` | `bool` | `true` when the balance could not be computed for this account (FR-012); `Balance` is `null` in that case and the UI renders an inline error indicator instead of a currency value. |

**Validation rules**: None — this is a derived, read-only projection; it carries no user input and is never persisted.

**State transitions**: None — recomputed fresh on every Chart of Accounts page load (FR-005); no cached or stored state.

## Service contract

### IAccountBalanceService (new: `StageFright.Core/Contracts/IAccountBalanceService.cs`)

```csharp
public interface IAccountBalanceService
{
    /// <summary>
    /// Returns a balance row for every active (non-archived) account, ordered by
    /// AccountNumber. A per-account calculation failure is isolated to that row
    /// (HasError=true, Balance=null) and does not affect any other row.
    /// </summary>
    Task<IReadOnlyList<AccountBalance>> GetActiveAccountBalancesAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a balance row for every archived account, ordered by AccountNumber.
    /// Same per-row error isolation as GetActiveAccountBalancesAsync.
    /// </summary>
    Task<IReadOnlyList<AccountBalance>> GetArchivedAccountBalancesAsync(CancellationToken ct = default);
}
```

Implemented by `AccountBalanceService` in `StageFright.Core/Modules/Finance/`, depending on `IAccountRepository` (existing) and `IGLRepository` (existing — `GetAccountBalanceAsync`). Registered in `MauiProgram.RegisterCoreServices` alongside the sibling `IMemberBalanceService`/`IAccountService` registrations.

## Relationships

```
Account (existing, unchanged)
   │  1:1 (computed, not FK)
   ▼
AccountBalance (new view model)
   │  Type drives
   ▼
sign convention (Asset/Expense = debit-normal; Liability/Equity/Income = credit-normal)
```

No changes to `Transaction`, `Fee`, `Payment`, GL posting logic, or any migration. The Record Expense tab change (User Story 2) introduces no data model changes at all — it is purely a UI navigation/composition change reusing the existing `ExpensePaymentPage` component, `RecordExpenseRequest`, and `IExpensePaymentService` unchanged.
