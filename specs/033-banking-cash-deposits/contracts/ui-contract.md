# UI and Service Contract: Banking Rework (spec 033)

## 1. Routes and navigation

| Route | Component | Notes |
|---|---|---|
| `/finance/banking` | `Pages/Finance/BankingPage` | Replaces the old screen. Finance menu item `Nav_Finance_Banking` at DisplayOrder 3, in the slot of the old `Nav_Finance_RecordBankDeposit` item. |
| `/finance/bank-deposit` | `BankingPage` (second `@page`) | Legacy address kept so bookmarks keep working (spec assumption). |
| `/finance/banking/deposit` | `DepositPage` | Save → `/finance/banking`. Cancel → `/finance/banking`. A *Back to Banking* link (`Finance_Banking_BackToBanking`) is shown in every state (issue #396). |
| `/finance/banking/deposits/{id:guid}` | `DepositDetailPage` | Unknown Id → `Finance_Banking_DepositNotFound` message plus a back link. |
| `/finance/banking/withdrawal` | `CashWithdrawalPage` | Save or Cancel → `/finance/banking`. A *Back to Banking* link (`Finance_Banking_BackToBanking`) is shown in every state (issue #396). |

`BankDepositPage.razor`, `BankDepositPage.razor.cs` and `BankDepositModel.cs` are deleted.

## 2. Screen layouts

### 2.1 BankingPage

1. **Summary row**, three compact cards: *Undeposited Funds* (Σ awaiting), *Petty Cash* (the current balance, shown negative when negative) and *Cash available* (today's figure, FR-010).
2. **Actions**: *Record deposit* and *Record cash withdrawal*. When `BankingOverview.DepositBankAccounts` is empty, `NoBankAccountNotice` (with a link to `/finance/accounts`) replaces both buttons (edge case "No bank account set up").
3. **Awaiting deposit** `RadzenDataGrid<AwaitingDepositItem>` (`AllowSorting AllowPaging PageSize="15" class="rz-shadow-0"`). Columns: Received, Payer, Reference, Amount, with a footer total. Empty text: `Finance_Banking_NothingAwaiting`.
4. **History** `RadzenDataGrid<BankingHistoryEntry>`. Columns: Date, Type (Deposit/Withdrawal), Bank account, Description (a withdrawal's purpose, US4), Total, Status (Recorded/Reversed; withdrawals show "—"). A deposit row click opens its detail. Sorted newest first.

### 2.2 DepositForm

The fields run top to bottom:
- **Date**: defaults to today. Changing it reloads the cash available.
- **Bank account**: a select of `DepositBankAccounts`, preselected when there is exactly one.
- **Cheques and other items**: a grid with a checkbox column whose `HeaderTemplate` is "select all". Columns: Received, Payer, Reference, Amount.
- **Cash amount**: help text reads `Finance_Banking_CashAvailableHelp` (`{CashAvailable}`). The field is disabled when the cash available is ≤ 0.
- **Totals panel**: Cash, Cheques, Deposit total (FR-011).
- **Save / Cancel**.

Save is disabled while the total is 0. The cash box updates the totals on every keystroke. Service errors are shown inline in an alert; the page does not pre-check the cash against the cash available, the service is the authority. Below the form, every state (including the no-bank notice and a load error) has a *Back to Banking* link (`Finance_Banking_BackToBanking`), as on DepositDetailPage (issue #396).

### 2.3 DepositDetailPage

The header shows Date, Bank account, Status, Cash (`CashAmount`) and Total, then a grid of included items (Received, Payer, Reference, Amount) with its sum. When `DepositDetail.CanReverse` is true, a *Reverse deposit* button asks for confirmation inline: it is replaced by `Finance_Banking_ReverseConfirm` with *Yes, reverse it* (`Finance_Banking_ReverseConfirmButton`) and *Cancel*. Nothing is posted until the second click. On success the page shows `Finance_Banking_DepositReversed`, reloads, and shows the Reversed status with the reversal date (`Finance_Banking_ReversedOn`). When the deposit cannot be reversed (already reversed, cleared in a finalised reconciliation, or in a closed period), there is no button and `CannotReverseReason` is shown instead. A legacy deposit shows `Finance_Banking_LegacyDepositNote` and has no button. A refusal at confirm time shows its message and reloads the page; any other failure shows `Finance_Banking_ReverseError`. Every state has a *Back to Banking* link (`Finance_Banking_BackToBanking`).

### 2.4 CashWithdrawalForm

The fields are Bank account (a select, preselected when there is only one), Date, Amount and Purpose (optional, max 200 characters), followed by Save / Cancel.

Save checks that a bank account is chosen and the amount is above zero, and shows the message under the field without calling the service. A blank purpose is sent as `Finance_Banking_WithdrawalDefaultDescription`, so the history shows a translated description. Service refusals and the closed-period message appear in an inline alert; any other failure shows `Finance_Banking_WithdrawalSaveError`. A successful save returns to `/finance/banking`. With no bank account, `NoBankAccountNotice` replaces the form. Every state has a *Back to Banking* link (`Finance_Banking_BackToBanking`) below the form (issue #396).

### 2.5 PaymentForm (member payment) changes

The existing *Method* select stays where it is. Directly after it:
- **Bank account**: shown only for Card or ElectronicTransfer, and required. Preselected when there is one eligible account. When there is none, the field shows `NoBankAccountNotice` and Save is disabled for these methods.
- **Cheque reference**: shown only for Check or Other. Optional, max 50 characters.

### 2.6 RecordIncome changes (FR-004)

The field order becomes: Date · Amount · Income account · **Payment method** (in the old *Deposit to* slot) · **Bank account** (Card/EFT only) · **Cheque reference** (Cheque/Other only) · Tax treatment (when tax applies) · Organisation (spec 032, unchanged) · Description. The *Deposit to* picker and the `Finance_RecordIncome_DepositToLabel` key are removed.

## 3. Service contracts (`StageFright.Core/Contracts/`)

```csharp
public interface IBankingQueryService
{
    Task<BankingOverview> GetOverviewAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AwaitingDepositItem>> GetAwaitingItemsAsync(CancellationToken ct = default);
    // min(Petty Cash balance at depositDate, current Petty Cash balance) — FR-010
    Task<decimal> GetCashAvailableAsync(DateTime depositDate, CancellationToken ct = default);
    Task<IReadOnlyList<BankingHistoryEntry>> GetHistoryAsync(CancellationToken ct = default);
    // Key is the BankDeposit Id, or the JournalEntry Id for a legacy deposit; null when neither exists.
    Task<BankDepositDetail?> GetDepositAsync(Guid id, CancellationToken ct = default);
}

public interface IBankDepositService
{
    Task<BankDeposit> RecordDepositAsync(RecordBankDepositRequest request, CancellationToken ct = default);
    Task ReverseDepositAsync(Guid bankDepositId, CancellationToken ct = default);
}

public interface ICashWithdrawalService
{
    Task<JournalEntry> RecordWithdrawalAsync(RecordCashWithdrawalRequest request, CancellationToken ct = default);
}

public interface IReceivedItemRepository
{
    Task<ReceivedItem> AddAsync(ReceivedItem item, CancellationToken ct = default);
    // Includes Payment.Member and JournalEntry.ExternalOrganisation (IgnoreQueryFilters), oldest first.
    Task<IReadOnlyList<ReceivedItem>> GetAwaitingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReceivedItem>> GetAwaitingByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<decimal> GetAwaitingTotalAsync(CancellationToken ct = default);
}

public interface IBankDepositRepository
{
    Task<BankDeposit> AddAsync(BankDeposit deposit, CancellationToken ct = default); // with Lines
    // Includes Lines → ReceivedItem (+payer navigations), BankAccount, JournalEntry.Transactions.
    Task<BankDeposit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BankDeposit>> GetAllAsync(CancellationToken ct = default);
    // Write-once; throws DataIntegrityException if already reversed.
    Task MarkReversedAsync(Guid id, Guid reversalJournalEntryId, DateTime reversedAt, CancellationToken ct = default);
}

// IBankReconciliationRepository addition
Task<IReadOnlyList<(Guid ReconciliationId, Guid TransactionId, ReconciliationStatus Status)>>
    GetClearingLinesAsync(IReadOnlyCollection<Guid> transactionIds, CancellationToken ct = default);
```

`IJournalEntryRepository` gains `GetByTypesAsync(IReadOnlyCollection<JournalEntryType> types, CancellationToken ct)`. It returns entries with their `Transactions` and `Account`, and is used for withdrawals and legacy deposits in the history.

### Request and read models (`Modules/Finance/`)

| Type | Members |
|---|---|
| `RecordBankDepositRequest` | `DateTime Date`, `Guid BankAccountId`, `IReadOnlyList<Guid> ReceivedItemIds`, `decimal CashAmount` |
| `RecordCashWithdrawalRequest` | `DateTime Date`, `Guid BankAccountId`, `decimal Amount`, `string? Purpose` |
| `RecordPaymentRequest` (+) | `Guid? BankAccountId`, `string? Reference` |
| `RecordIncomeRequest` (±) | − `DepositAccountId`; + `PaymentMethod PaymentMethod` (default `Cash`), `Guid? BankAccountId`, `string? Reference` |
| `AwaitingDepositItem` | `Guid Id`, `DateTime ReceivedDate`, `ReceivedItemSource Source`, `string? PayerName`, `string? Reference`, `decimal Amount` |
| `BankingOverview` | `IReadOnlyList<AwaitingDepositItem> AwaitingItems`, `decimal UndepositedTotal`, `decimal PettyCashBalance`, `decimal CashAvailableToday`, `IReadOnlyList<Account> DepositBankAccounts` |
| `BankingHistoryEntry` | `Guid Id` (BankDeposit Id or JournalEntry Id), `BankingHistoryEntryType Type`, `DateTime Date`, `string BankAccountName`, `decimal Total`, `bool IsReversed`, `bool IsLegacy`, `string? Description` |
| `BankDepositDetail` | `Guid Id`, `DateTime Date`, `string BankAccountName`, `decimal CashAmount`, `IReadOnlyList<AwaitingDepositItem> Items`, `decimal Total`, `bool IsReversed`, `DateTime? ReversedAt`, `bool IsLegacy` |

`ReceiptDestinationResolver.ResolveAsync(PaymentMethod method, Guid? bankAccountId, CancellationToken ct) → Task<Account>` is defined in research §4.

## 4. Backup members

These are given in [data-model.md](../data-model.md) §7: envelope 29, 31 and 32. `JournalEntryBackupDto` is unchanged. `BackupSchema.CurrentSchemaVersion = "1.4.0"`.

## 5. Localization keys

Every neutral key ships translated into de-DE, en-US, es-ES, fr-FR, it-IT, ja-JP and pl-PL. `qps-ploc` is regenerated.

**NavigationResource**: + `Nav_Finance_Banking` ("Banking"). − `Nav_Finance_RecordBankDeposit`.

**EnumsResource**: + `Enum_JournalEntryType_BankDepositReversal` ("Bank deposit reversal"), `Enum_JournalEntryType_CashWithdrawal` ("Cash withdrawal"), `Enum_ReceivedItemSource_MemberPayment` ("Member payment"), `Enum_ReceivedItemSource_IncomeEntry` ("Income"), `Enum_ReceivedItemSource_OpeningBalance` ("Opening balance"), `Enum_BankingHistoryEntryType_Deposit` ("Deposit") and `Enum_BankingHistoryEntryType_Withdrawal` ("Withdrawal"). The existing `Enum_JournalEntryType_BankDeposit` is reworded to "Bank deposit" if it still says Cash on Hand.

**FinanceResource**: + the `Finance_Banking_*` keys below. − every `Finance_BankDeposit_*` key and `Finance_RecordIncome_DepositToLabel`.

| Key | en-AU value |
|---|---|
| `Finance_Banking_PageTitle` | Banking |
| `Finance_Banking_UndepositedTotal` | Undeposited Funds |
| `Finance_Banking_PettyCashBalance` | Petty Cash |
| `Finance_Banking_CashAvailable` | Cash available |
| `Finance_Banking_AwaitingHeading` | Awaiting deposit |
| `Finance_Banking_NothingAwaiting` | No cheques or other items are awaiting deposit. |
| `Finance_Banking_HistoryHeading` | Deposits and withdrawals |
| `Finance_Banking_NoHistory` | No deposits or withdrawals have been recorded. |
| `Finance_Banking_RecordDeposit` | Record deposit |
| `Finance_Banking_RecordWithdrawal` | Record cash withdrawal |
| `Finance_Banking_Col_Received` / `_Payer` / `_Reference` / `_Amount` / `_Date` / `_Type` / `_BankAccount` / `_Total` / `_Status` | Received / Payer / Reference / Amount / Date / Type / Bank account / Total / Status |
| `Finance_Banking_Status_Recorded` / `_Reversed` | Recorded / Reversed |
| `Finance_Banking_NoBankAccountHtml` | This needs a bank account other than Petty Cash. Add one in the `<a href="/finance/accounts">`Chart of Accounts`</a>` first. |
| `Finance_Banking_DepositTitle` | Record bank deposit |
| `Finance_Banking_ItemsHeading` | Cheques and other items |
| `Finance_Banking_CashAmountLabel` | Cash amount |
| `Finance_Banking_CashAvailableHelp` | Cash available: {CashAvailable} |
| `Finance_Banking_TotalCash` / `_TotalCheques` / `_TotalDeposit` | Cash / Cheques / Deposit total |
| `Finance_Banking_DepositDetailTitle` | Bank deposit |
| `Finance_Banking_DepositNotFound` | That deposit could not be found. |
| `Finance_Banking_ReverseButton` | Reverse deposit |
| `Finance_Banking_ReverseConfirm` | Reverse this deposit? Its cheques return to awaiting deposit and its cash returns to Petty Cash. |
| `Finance_Banking_ReversedOn` | Reversed on {Date} |
| `Finance_Banking_LegacyDepositNote` | Recorded before cheque tracking was added; it cannot be reversed here. |
| `Finance_Banking_WithdrawalTitle` | Record cash withdrawal |
| `Finance_Banking_PurposeLabel` | Purpose |
| `Finance_Banking_WithdrawalDefaultDescription` | Cash withdrawal |
| `Finance_Banking_DepositSaved` / `_WithdrawalSaved` / `_DepositReversed` | Deposit recorded. / Withdrawal recorded. / Deposit reversed. |
| `Finance_Banking_SelectAllAriaLabel` / `_SelectRowAriaLabel` / `_DepositSaveError` | Select all items awaiting deposit / Select item from {Payer} received {Date} / Failed to record deposit: {Detail} |
| `Finance_Banking_WithdrawalSaveError` | Failed to record withdrawal: {Detail} |
| `Finance_Banking_ReverseConfirmButton` / `_BackToBanking` / `_ReverseError` | Yes, reverse it / Back to Banking / Failed to reverse deposit: {Detail} |
| `Finance_Payment_BankAccountLabel` | Bank account |
| `Finance_Payment_ReferenceLabel` | Cheque reference |
| `Finance_RecordIncome_PaymentMethodLabel` | Payment method |

**ValidationResource**: + the keys listed in [data-model.md](../data-model.md) §7. − `Validation_BankDeposit_AmountPositive`, `Validation_BankDeposit_TargetMustBeBank`, `Validation_BankDeposit_TargetNotCashOnHand` and `Validation_Income_DepositAccountMustBeBank`. Sample values:
- `Validation_Deposit_CashExceedsAvailable`: "The cash amount is more than the cash available ({CashAvailable})."
- `Validation_Deposit_ReversalBlockedByFinalisedReconciliation`: "This deposit is part of a finalised bank reconciliation and cannot be reversed."

Before deleting any key, the implementer greps for remaining uses.

## 6. Exception taxonomy and UI mapping

| Exception | Raised by | UI handling |
|---|---|---|
| `ValidationException` | resolver, deposit, withdrawal, opening balance and journal services | Inline alert with the localized message. The form stays open. |
| `EntityNotFoundException` | a missing account, item or deposit | Detail: the not-found message. Forms: an inline alert. |
| `ValidationException` (`Validation_Deposit_ReversalBlockedByFinalisedReconciliation`) | reversal blocked by a finalised reconciliation | The inline confirmation closes, an alert shows the message, and the page reloads without the button. |
| `ClosedPeriodException` | `ClosedPeriodGuard` through the GL | `Validation_ClosedPeriod_PostingRejected`, as on the other finance forms. |
| `GLBalanceException` | the GL (a defensive check that should be unreachable) | A generic save-failed alert, logged. |
| `DataIntegrityException` | `MarkReversedAsync` double stamp, which is a race | Treated as "already reversed": the page reloads. |
| `DataAccessException` | repositories | A generic save-failed alert, logged. |
| Any other exception | an unexpected framework failure | Logged. The alert shows `Finance_Banking_UnexpectedErrorDetail` as its `{Detail}`, never the exception's own text, through `ErrorDetail.For` (`StageFright.UI/Shared/`). StageFright exceptions keep their message as the detail. |

If the cash available fails to reload after a date change, the deposit page sets it to zero and clears the cash amount, so the cash box locks rather than keep the previous date's figure.
