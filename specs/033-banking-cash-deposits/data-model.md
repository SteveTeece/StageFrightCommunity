# Data Model: Banking Rework (spec 033)

One migration, `AddBankingRework`, does four things:
- creates three tables (`ReceivedItems`, `BankDeposits`, `BankDepositLines`);
- renames account `…0001` to "Petty Cash";
- seeds the Undeposited Funds account;
- appends two `JournalEntryType` values. These are stored as strings, so no column changes.

`Payment`, `JournalEntry` and `Transaction` keep every column they have. The entity count goes from 22 to 25. All three new entities are financial records: they have no soft-delete fields and are never deleted.

## 1. Accounts

| Id | Number | Name | Type | IsSystem | IsBankAccount | Change |
|---|---|---|---|---|---|---|
| `…0001` | 1100 | Petty Cash | Asset | true | true | `Name` was "Cash on Hand" (`UpdateData`) |
| `…0008` | 1105 | Undeposited Funds | Asset | true | false | new (`HasData` + `InsertData`), `SortOrder` after Member Receivable |

`SystemAccounts` gains `UndepositedFundsId` and `UndepositedFundsNumber = "1105"`. Undeposited Funds is:
- excluded from `GeneralJournalService.GetJournalAccountsAsync` and refused in `RecordJournalAsync`, with the new `Validation_Journal_NoUndepositedFunds`;
- accepted by opening balances only for a positive amount, otherwise `Validation_OpeningBalance_UndepositedFundsNegative`.

"Bank account other than Petty Cash" means `IsBankAccount && Id != SystemAccounts.CashId`.

## 2. `JournalEntryType`

Two values are appended: `BankDepositReversal` and `CashWithdrawal`. Existing values keep their meaning. Neither new type carries `ExternalOrganisationId` (spec 033 FR-025). Both need an `Enum_JournalEntryType_*` label in `EnumsResource`, in every culture.

| Posting | Type | Lines (a zero line is never written) |
|---|---|---|
| Deposit | `BankDeposit` | DR bank (total) · CR 1105 (cheque total) · CR 1100 (cash amount) |
| Reversal | `BankDepositReversal` | the exact opposite of its deposit, on the deposit's date |
| Withdrawal | `CashWithdrawal` | DR 1100 (amount) · CR bank (amount); `Description` = purpose |

## 3. `ReceivedItem` (new)

A cheque or "other" receipt waiting to be banked, or the single opening-balance amount on Undeposited Funds.

| Field | Type | Rule |
|---|---|---|
| `Id` | `Guid` | PK |
| `Date` | `DateTime` | the receipt's posting date |
| `Amount` | `decimal` | > 0; the gross debited to 1105 for this receipt |
| `Method` | `PaymentMethod` | `Check` or `Other`. The opening-balance item is `Other`. |
| `Reference` | `string?` | max 50, trimmed, null when blank; set once |
| `PaymentId` | `Guid?` | FK `Payments`, restrict; set for a member payment |
| `JournalEntryId` | `Guid?` | FK `JournalEntries`, restrict; set for an income or opening-balance entry |
| `CreatedAt` | `DateTime` | UTC |

- **Invariant:** exactly one of `PaymentId` or `JournalEntryId` is set. This is a check constraint plus a service rule.
- **Indexes:** a unique index on `PaymentId` and a unique index on `JournalEntryId`, each filtered to non-null.
- **Awaiting (derived):** no `BankDepositLine` for the item belongs to a deposit with `ReversalJournalEntryId IS NULL`.
- **Payer (derived, research §13):** `Payment.Member` full name. Otherwise the linked `JournalEntry.ExternalOrganisation` name (archived included), else the `JournalEntry.Description`. For an `OpeningBalance` entry, `Finance_Banking_OpeningBalancePayer`.
- **Invariant:** Σ `Amount` of awaiting items equals the GL balance of 1105.

## 4. `BankDeposit` (new)

| Field | Type | Rule |
|---|---|---|
| `Id` | `Guid` | PK |
| `Date` | `DateTime` | not before any included item's `Date`; inside an open period |
| `BankAccountId` | `Guid` | FK `Accounts`, restrict; a bank account other than Petty Cash |
| `CashAmount` | `decimal` | ≥ 0; ≤ the cash available (research §8) |
| `ChequeTotal` | `decimal` | Σ included items' `Amount` |
| `JournalEntryId` | `Guid` | FK `JournalEntries`, restrict, unique |
| `ReversalJournalEntryId` | `Guid?` | FK `JournalEntries`, restrict, unique when set; set once |
| `ReversedAt` | `DateTime?` | UTC; set together with the reversal link |
| `CreatedAt` | `DateTime` | UTC |

- **Validation:** `CashAmount + ChequeTotal > 0`. At least one item or a cash amount is required.
- **Total (derived):** `CashAmount + ChequeTotal`.
- **Status (derived):** Recorded while `ReversalJournalEntryId` is null, Reversed after. A reversal is refused if any of these holds:
  - the deposit is already reversed;
  - its bank-debit `Transaction` is in a `Finalised` reconciliation;
  - its date is in a closed period.

## 5. `BankDepositLine` (new)

| Field | Type | Rule |
|---|---|---|
| `Id` | `Guid` | PK |
| `BankDepositId` | `Guid` | FK `BankDeposits`, restrict |
| `ReceivedItemId` | `Guid` | FK `ReceivedItems`, restrict |
| `CreatedAt` | `DateTime` | UTC |

- **Index:** unique on (`BankDepositId`, `ReceivedItemId`). `ReceivedItemId` alone is not unique, because an item can be deposited again after its deposit is reversed.
- **Rule:** at save time, every referenced item must be awaiting. This is checked inside the transaction (research §10).

## 6. Request shapes

| Type | Change |
|---|---|
| `RecordPaymentRequest` | + `Guid? BankAccountId` (required for `Card`/`ElectronicTransfer`, else ignored), + `string? Reference` (cheque/other only) |
| `RecordIncomeRequest` | − `DepositAccountId`; + `PaymentMethod PaymentMethod` (default `Cash`), + `Guid? BankAccountId`, + `string? Reference` |
| `RecordDepositRequest` (new) | `DateTime Date`, `Guid BankAccountId`, `IReadOnlyList<Guid> ReceivedItemIds`, `decimal CashAmount`, `string? Description` |
| `RecordCashWithdrawalRequest` (new) | `DateTime Date`, `Guid BankAccountId`, `decimal Amount` (> 0), `string? Purpose` |
| `RecordBankDepositRequest` | removed, with `BankDepositService` and `IBankDepositService` |

Read models, which are never persisted:
- `BankingOverview`: awaiting items, awaiting total, Petty Cash balance, has-bank-account flag.
- `AwaitingDepositItem`: id, date, payer, reference, method, amount.
- `BankingHistoryEntry`: id, date, type, bank account name, total, isReversed, depositId?.
- `DepositDetail`: deposit, its items, cash amount, total, canReverse, plus the reason it cannot be reversed.

## 7. Backup `1.4.0`

| Where | Member | Content |
|---|---|---|
| `BackupEnvelope` | 29 | `List<ReceivedItemBackupDto>? ReceivedItems` |
| `BackupEnvelope` | 31 | `List<BankDepositBackupDto>? BankDeposits` |
| `BackupEnvelope` | 32 | `List<BankDepositLineBackupDto>? BankDepositLines` |
| `EntityCounts` (30) | — | gains keys `ReceivedItems`, `BankDeposits`, `BankDepositLines` |

Each DTO mirrors its entity's fields, numbered from 1 in table order.

The restore order is as follows: `JournalEntries`, then `Fees`, then `Payments`, then `Transactions`, then **`ReceivedItems`**, then **`BankDeposits`**, then **`BankDepositLines`**, then the reconciliations.

When the file version is older than `1.4.0`, account `CashId` is renamed "Petty Cash" after the upsert. Files at `1.2.0` and `1.3.0` restore with no items and no deposits. A `1.4.0` file is refused by a `1.3.0` app, which `BackupSchema.IsRestorable` already does.
