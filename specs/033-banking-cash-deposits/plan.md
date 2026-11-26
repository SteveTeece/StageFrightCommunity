# Implementation Plan: Banking Rework (Undeposited Funds, Petty Cash, Deposits, Cash Withdrawals)

**Branch**: `033-banking-cash-deposits` | **Date**: 2026-11-26 | **Spec**: [spec.md](spec.md) | **Issue**: #368

> **Scale note.** This change spans all five source projects, about 60 files, plus every culture of four resource sets and roughly 20 test files that name "Cash on Hand". Watch three seams most: how money is routed in `PaymentService` and `IncomeEntryService`, the backup restore order with its Petty Cash rename fix-up, and the rewrite of the debug seeder's cash sweep.

## Summary

Money received is no longer always posted to Cash on Hand. Cash goes to Petty Cash (the renamed 1100 account). Cheques and "other" go to a new Undeposited Funds system account (1105), each tracked as a received item awaiting deposit. Card and electronic transfer go straight to a chosen bank account. A new Banking screen replaces Record Bank Deposit. It lists the items awaiting deposit, records a deposit (selected items plus a cash amount) as one balanced journal entry with a single bank line, records cash withdrawals into Petty Cash, and reverses a deposit with an opposite entry dated on the original date. Three new entities (`ReceivedItem`, `BankDeposit`, `BankDepositLine`) and two new `JournalEntryType` values carry this. One migration renames the account and seeds the new one, and the backup format goes to `1.4.0`. No new dependency is added.

## Project Structure

```text
src/StageFright.Core/
  Entities/            ReceivedItem.cs, BankDeposit.cs, BankDepositLine.cs (new)
  Enums/               JournalEntryType.cs (+ BankDepositReversal, CashWithdrawal)
  Contracts/           IBankingService.cs, IReceivedItemRepository.cs, IBankDepositRepository.cs (new)
                       IBankReconciliationRepository.cs, IUnitOfWork.cs (changed); IBankDepositService.cs (removed)
  Localization/        MoneyFormatter.cs (+ Symbol)
  Modules/Finance/     BankingService.cs, ReceiptRouting.cs, RecordDepositRequest.cs,
                       RecordCashWithdrawalRequest.cs, BankingOverview.cs, AwaitingDepositItem.cs,
                       BankingHistoryEntry.cs, DepositDetail.cs (new)
                       SystemAccounts.cs, PaymentService.cs, RecordPaymentRequest.cs, IncomeEntryService.cs,
                       RecordIncomeRequest.cs, GeneralJournalService.cs, OpeningBalanceService.cs,
                       FinanceMenuItemProvider.cs (changed)
                       BankDepositService.cs, RecordBankDepositRequest.cs (removed)
  Modules/Settings/    BackupService.cs, Backup/BackupEnvelope.cs, Backup/BackupSnapshot.cs,
                       Backup/*BackupDto.cs (3 new)
  BackupSchema.cs      1.3.0 -> 1.4.0
src/StageFright.Data/
  StageFrightDbContext.cs, Configurations/ (3 new), Repositories/ (2 new, BackupRepository,
  BankReconciliationRepository, UnitOfWork changed), Migrations/<ts>_AddBankingRework.cs (new)
src/StageFright.UI/Pages/Finance/
  BankingPage, DepositPage, CashWithdrawalPage, DepositDetailPage (+ models, new)
  PaymentForm, RecordIncome (changed); BankDepositPage (removed)
src/StageFright.App/   MauiProgram.cs (registrations), Seeding/DebugDataSeeder.cs (rewritten banking flows)
Resources              FinanceResource, ValidationResource, NavigationResource, EnumsResource (all cultures)
tests/                 Core, Data, UI, Integration, Reports, Localization test projects
```

**Structure decision:** the work follows the existing Finance module slice. Repositories stay central in `StageFright.Data`, and the UI lives in `StageFright.UI/Pages/Finance`. `BankingService` replaces `BankDepositService` outright rather than living beside it.

## Call paths

```calls A member payment routes by payment method
  PaymentForm.SaveAsync() @ src/StageFright.UI/Pages/Finance/PaymentForm.razor.cs:59
~   PaymentService.RecordAsync() @ src/StageFright.Core/Modules/Finance/PaymentService.cs:45
+     ReceiptRouting.Destination() **new** @ src/StageFright.Core/Modules/Finance/ReceiptRouting.cs
      GLRepository.AddBalancedSetAsync() @ src/StageFright.Data/Repositories/GLRepository.cs:33
+     ReceivedItemRepository.AddAsync() **new** @ src/StageFright.Data/Repositories/ReceivedItemRepository.cs
```
note: `IncomeEntryService.RecordIncomeAsync` (line 86) takes the same routing, and `OpeningBalanceService` adds the one opening-balance item.

```calls Recording a deposit
+ DepositPage.SaveAsync() **new** @ src/StageFright.UI/Pages/Finance/DepositPage.razor.cs
+   BankingService.RecordDepositAsync() **new** @ src/StageFright.Core/Modules/Finance/BankingService.cs
+     BankDepositRepository.AddAsync() **new** @ src/StageFright.Data/Repositories/BankDepositRepository.cs
      GLRepository.AddBalancedSetAsync() @ src/StageFright.Data/Repositories/GLRepository.cs:33
```
note: Replaces `BankDepositPage.SaveAsync` (line 46) and `BankDepositService.RecordDepositAsync` (line 39), which are removed; FR-018 is enforced inside `AddBalancedSetAsync`.

```calls Reversing a deposit
+ DepositDetailPage.ReverseAsync() **new** @ src/StageFright.UI/Pages/Finance/DepositDetailPage.razor.cs
+   BankingService.ReverseDepositAsync() **new** @ src/StageFright.Core/Modules/Finance/BankingService.cs
+     BankReconciliationRepository.IsTransactionInFinalisedAsync() @ src/StageFright.Data/Repositories/BankReconciliationRepository.cs:105
      BankReconciliationRepository.RemoveLineAsync() @ src/StageFright.Data/Repositories/BankReconciliationRepository.cs:105
      GLRepository.AddBalancedSetAsync() @ src/StageFright.Data/Repositories/GLRepository.cs:33
```
note: Withdrawals (`BankingService.RecordWithdrawalAsync`) and backup (`BackupRepository.UpsertSnapshotAsync`, line 60) follow the same posting and upsert patterns; see research §7.

## States

```states A received item
Awaiting: Listed on the Banking screen; counted in Undeposited Funds. (start) shows banking
Deposited: Included in a recorded deposit. shows deposit
Awaiting -> Deposited: included in a saved deposit
Deposited -> Awaiting: its deposit is reversed
grid:
Awaiting | Deposited
```
note: The state is derived from deposit lines whose deposit is not reversed. It is never stored on the item.

```states A bank deposit
Recorded: Posted; its cheques are deposited. (start)
Reversed: An opposite entry posted on the same date; cannot be reversed again. (final)
Recorded -> Reversed: reverse (refused if closed period or finalised reconciliation)
grid:
Recorded | Reversed
```

## Screens

```screen banking Banking
title: Banking (new)
row:
  chip: Petty Cash balance (new) (1)
  chip: Awaiting deposit total (new)
row:
  button: Record deposit (new) (2)
  button: Record cash withdrawal (new)
text: Cheques awaiting deposit (new)
list: Received | Payer | Reference | Amount
text: Deposits and withdrawals (new)
list: Date | Type | Bank account | Total | Status (3)
```
1: **Negative shows as negative.** Uses `MoneyFormatter.Format`, never hidden.
2: **No bank account.** Both buttons are replaced by the "add an account" message linking to the chart of accounts.
3: **History rows.** A deposit row opens its detail page, which offers Reverse while the deposit is recorded.

```screen deposit Record deposit
title: Record deposit (new)
row:
  field: Date (new)
  field: Bank account (new) (1)
list: Select | Received | Payer | Reference | Amount (2)
row:
  field: Cash amount (new) (3)
  text: Cash available (new)
row:
  text: Cheques total, Cash, Deposit total (new) (4)
  button: Save deposit (new)
```
1: **Preselected** when only one bank account other than Petty Cash exists.
2: **Select-all** lives in the grid's header template, per the data grid standard.
3: **Disabled** when the cash available is zero or less.
4: **Running total** updates on every tick and keystroke (FR-011).

## Constitution Check

| Principle | Assessment |
|---|---|
| 3.1 Clean code / simple over clever | PASS. One service, one routing function, derived status instead of stored flags. |
| 3.2 SOLID | PASS. `IBankingService` owns banking; payment and income services keep their own posting and only gain routing. |
| 3.3 Separation of concerns | PASS. Core holds the rules, Data the repositories, UI the pages. |
| 3.4 Soft delete | PASS, under the financial exemption. `ReceivedItem`, `BankDeposit` and `BankDepositLine` are financial records like `Payment`. They carry no soft-delete fields and are never deleted. |
| 3.5 Member and financial data preservation | PASS. History is not reclassified (FR-020). Account 1100 keeps its number and rows. |
| 3.6 Financial corrections pattern | PASS. A reversal is a new opposite journal entry. `BankDeposit` gains only its one-time reversal link. |
| 4.1 Layered architecture, module slices | PASS. The new code sits in the Finance slice. |
| 4.6 Navigation menu | PASS. Banking replaces the sub-item at `DisplayOrder` 3. |
| 4.7 Blazor component patterns | PASS. Every new `.razor` has a `.razor.cs`; grids are `RadzenDataGrid`. |
| 5 Custom exceptions | PASS. Rule failures throw `ValidationException` with translated text; data errors are wrapped in `DataAccessException`. |
| 6 Logging and audit | PASS. Each deposit, reversal and withdrawal writes an audit entry against its journal entry (FR-019). |
| 7.3 Prohibited | PASS. No JavaScript, no new package. |
| 8 Plug-in architecture | PASS. No `StageFright.Plugins.Contracts` change. |
| 11 Testing | PASS, planned. Every rule has unit tests; workflows have SQLite integration tests; pages have bUnit tests (see tasks). |

Re-checked after Phase 1 design: no change, no violations.
