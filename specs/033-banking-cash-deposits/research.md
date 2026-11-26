# Research: Banking Rework (spec 033)

Each entry records one design choice the spec leaves open. The basis is four code readers' findings on the Finance services, attendance, the Finance UI and the seeded system accounts, plus a direct read of backup, reconciliation, the general journal and opening balances.

## 1. Undeposited Funds account identity

- **Decision:** a seeded system account with fixed id `00000000-0000-0000-0000-000000000008`, number `1105`, `Type = Asset`, `IsSystem = true`, `IsBankAccount = false`, added through `HasData` and a migration `InsertData`. It gets `SystemAccounts.UndepositedFundsId` and `UndepositedFundsNumber`.
- **Rationale:** `AccountNumber` is unique across the whole chart, archived accounts included. User bank accounts are numbered from 1110 upwards, and other user assets from 1300. The numbers 1101 to 1109 are never handed out automatically, so 1105 sits next to Petty Cash and can never clash. Because it is a system account, `OrganisationHistoryService` skips it, the chart of accounts blocks editing and archiving, and the trial balance always lists it. Because it is not a bank account, it never appears in a bank picker, a reconciliation or an expense pay-from list.
- **Alternatives considered:** 1110 to 1299 would eventually collide with a user bank account and trip the unique index. Picking a free number at migration time cannot be expressed through `HasData`, and the app is unreleased, so a fixed number is enough.

## 2. Renaming Cash on Hand to Petty Cash

- **Decision:** a migration `UpdateData` on row `…0001`, `Name` set to "Petty Cash", with a `Down` that restores the old name. The C# names `SystemAccounts.CashId` and `CashNumber` stay. Doc comments, GL descriptions written from now on, and resource strings that say "Cash on Hand" are reworded.
- **Rationale:** this follows the `SyncTaxAccountSeedNames` precedent and the `TaxPaid*` naming exception CLAUDE.md records. `Transaction.GLAccount` snapshots stay valid because the number does not change.
- **Alternatives considered:** renaming the identifiers to `PettyCashId` would touch about 15 call sites and many tests for no behavioural gain.

## 3. Where a cheque awaiting deposit is recorded

- **Decision:** a new financial entity `ReceivedItem`. It holds `Id`, `Date`, `Amount`, `Method` (`Check` or `Other`), an optional `Reference`, and exactly one source: `PaymentId` (member payment), or `JournalEntryId` (an income entry or the opening-balance entry). It also holds `CreatedAt`. There is one item per payment or income entry, for its gross amount. Its awaiting or deposited state is derived, never stored: an item is deposited when a `BankDepositLine` points at it from a deposit that is not reversed.
- **Rationale:** `Payment` and `JournalEntry` are immutable financial records, and the deposit state changes over time (spec Key Entities). `PaymentService` writes one money-side GL line per fee it pays plus one for any overpayment, so keying on GL lines would split one cheque into several items. Under exclusive tax the money-side figure is the gross, not the entered amount, so the item stores the gross. `Payment.Notes` can be edited, so the set-once cheque reference cannot live there. Deriving the state means a reversal needs no write to the item.
- **Alternatives considered:** a status column on `Payment` or `JournalEntry` would break immutability. A status column on `ReceivedItem` would be redundant with the deposit lines, and the two could drift apart.

## 4. Deposit and reversal records

- **Decision:** a `BankDeposit` entity with `Id`, `Date`, `BankAccountId`, `CashAmount`, `ChequeTotal`, `JournalEntryId`, `ReversalJournalEntryId?`, `ReversedAt?` and `CreatedAt`. Each included item is a `BankDepositLine` with `Id`, `BankDepositId`, `ReceivedItemId` and `CreatedAt`. The deposit posts one `JournalEntry` of the existing type `BankDeposit`. It has one bank debit for the total (FR-013), a credit to Undeposited Funds for the cheque total, and a credit to Petty Cash for the cash amount. Each credit is left out when it is zero, because `GLRepository` rejects zero lines. The reversal posts a new type, `BankDepositReversal`, with the opposite lines on the original date, and sets `ReversalJournalEntryId` and `ReversedAt` once.
- **Rationale:** reusing `BankDeposit` keeps existing reports and the type's meaning unchanged (FR-021). The only mutation is the one-time reversal link, the same set-once pattern spec 032 uses for its organisation link. The GL rows themselves are never touched.
- **Alternatives considered:** deriving the cash amount and cheque total from the GL lines would avoid two columns, but every history and detail view would then have to rebuild them. A separate reversal entity adds a table for one link.

## 5. Cash withdrawals

- **Decision:** no new entity. A withdrawal is a `JournalEntry` of the new type `CashWithdrawal`, whose `Description` holds the purpose: debit Petty Cash, credit the bank account. The Banking history merges `BankDeposit` rows with `CashWithdrawal` journal entries.
- **Rationale:** a withdrawal has no lines or state beyond its journal entry. FR-017 corrects it with a deposit, so it never needs a reversal link.
- **Alternatives considered:** a `CashWithdrawal` entity would duplicate the journal entry's date, amount and description.

## 6. Routing money by payment method

- **Decision:** a static, stateless function `ReceiptRouting.Destination(PaymentMethod method, Guid? bankAccountId)` returns `CashId` for cash, `UndepositedFundsId` for cheque and other, and the chosen bank account for card and electronic transfer. `ReceiptRouting.CreatesReceivedItem(method)` is true for cheque and other. `PaymentService` and `IncomeEntryService` call it, and each checks the chosen bank account with its existing account repository: it must exist, be a bank account, and not be Petty Cash. `RecordPaymentRequest` gains `BankAccountId?` and `Reference?`. `RecordIncomeRequest` swaps `DepositAccountId` for `PaymentMethod`, `BankAccountId?` and `Reference?`. `AttendanceService` is unchanged, because it already posts cash to 1100.
- **Rationale:** the mapping is a pure rule with two callers. A static pure function holds no state, so the constitution's "avoid static state" rule is respected, and it is unit-testable without a mock.
- **Alternatives considered:** an injected router service would need an interface solely for NSubstitute, an interface with one implementation.

## 7. Backup format 1.4.0

- **Decision:** `BackupSchema.CurrentSchemaVersion` becomes `"1.4.0"`. The envelope gains member 29 `ReceivedItems`, member 31 `BankDeposits` and member 32 `BankDepositLines`, with matching `EntityCounts` keys and three DTOs. `JournalEntryBackupDto` is unchanged. Restore upserts the three sets after `Transactions` and before reconciliations, in the order items, deposits, lines. After the upsert, a file whose version is older than `1.4.0` has its `CashId` account name set back to "Petty Cash".
- **Rationale:** member 27 and 28 belong to spec 032, and member 30 is `EntityCounts`. Restore only adds or updates rows by id and never deletes, so the seeded Undeposited Funds row survives an old restore. However, an old file's `…0001` row would bring the name "Cash on Hand" back. Older files hold no items, so nothing is listed as awaiting deposit (FR-022).
- **Alternatives considered:** always overwriting system account names on restore would also revert any future deliberate renames recorded in a file.

## 8. Cash available for a deposit

- **Decision:** `min(balance(CashId, end of deposit date), balance(CashId, DateTime.MaxValue))`, using the existing `GetAccountBalanceAsync`. The end of the day is `date.Date.AddDays(1).AddTicks(-1)`. The figure is computed inside the posting transaction, and again for display.
- **Rationale:** `GetAccountBalanceAsync` compares full date-times, so passing the bare date would miss postings later on the same day. Rehearsal cash can be recorded after the event, so both bounds matter (clarification Q4).
- **Alternatives considered:** a dedicated repository query would duplicate an existing one.

## 9. Reversal checks and reconciliation

- **Decision:** the reversal runs in one transaction, in this order:
  1. Refuse if the deposit is already reversed.
  2. Refuse if its bank `Transaction` is in a finalised reconciliation. This uses a new `IBankReconciliationRepository.IsTransactionInFinalisedAsync`.
  3. Remove any draft reconciliation line for that transaction with the existing `RemoveLineAsync`.
  4. Post the opposite lines on the original date. The closed-period guard runs inside `AddBalancedSetAsync`.
  5. Set the reversal link.

  Both the original bank line and the reversal's bank line stay visible in the reconciliation workspace. They net to zero, and the user can clear both together.
- **Rationale:** reconciliation clears one `Transaction` at a time (`ReconciliationLine`), so the check and the removal are both about the deposit's single bank line. Hiding real GL rows from reconciliation would make the workspace disagree with the ledger.
- **Alternatives considered:** auto-clearing the pair would mean writing to a draft the user did not touch.

## 10. Two deposits racing for the same cheque

- **Decision:** inside the deposit transaction, re-read which items are still awaiting deposit, and refuse with a translated `ValidationException` when any selected item is no longer awaiting.
- **Rationale:** SQLite serialises writers, so a check inside the transaction is enough. A unique index on `BankDepositLine.ReceivedItemId` cannot work, because a reversed item may be deposited again.
- **Alternatives considered:** optimistic concurrency tokens on items would add a column for a case the transaction already covers.

## 11. Errors shown to the user

- **Decision:** rule refusals throw `ValidationException` with translated `ValidationResource` text, including the formatted cash available where relevant. The closed-period case stays `ClosedPeriodException`. Pages show the closed-period key as they do today, and show other exceptions' messages.
- **Rationale:** this is the existing service and page pattern (`BankDepositPage`, `RecordIncome`). No new exception type is needed.
- **Alternatives considered:** a `BankingException` type would carry nothing that `ValidationException` does not.

## 12. Pages and navigation

- **Decision:** four routed pages:
  - `BankingPage` at `/finance/banking`, with a second `@page "/finance/bank-deposit"` so old links keep working;
  - `DepositPage` at `/finance/banking/deposit`;
  - `CashWithdrawalPage` at `/finance/banking/withdrawal`;
  - `DepositDetailPage` at `/finance/banking/deposits/{id:guid}`, which reverses through an inline two-step confirm, as in `AnnualFeeApplication`.

  Grids are `RadzenDataGrid` with the standard settings, and the item picker copies `OutstandingFeeSelectionGrid` with its select-all header template. The menu item `Nav_Finance_Banking` replaces `Nav_Finance_RecordBankDeposit` at `DisplayOrder` 3. `MoneyFormatter` gains a read-only `Symbol`, which new and touched amount inputs use in place of `CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol`.
- **Rationale:** a second `@page` is the simplest redirect and needs no stub page. The existing currency prefix shows the culture's symbol (€ under fr-FR), which breaks FR-023, and the forms this feature touches should not keep that bug.
- **Alternatives considered:** dialogs on the Banking page would make deep links and bUnit tests harder.

## 13. Payer shown for an item

- **Decision:** the payer is resolved when the list is read:
  - for a member payment, the member's name;
  - for an income entry, its linked organisation's name (archived included), else its description;
  - for the opening-balance item, the translated label `Finance_Banking_OpeningBalancePayer`.
- **Rationale:** nothing is copied, so names stay current, and the opening-balance label translates (FR-023).
- **Alternatives considered:** storing a payer snapshot on the item would go stale when a member is renamed.

## 14. Known limits accepted

- Account names are stored data and display untranslated, as every existing system account does. "Petty Cash" and "Undeposited Funds" follow that rule.
- Deposits recorded before this change have no `BankDeposit` record, so the Banking history does not list them. They stay correct in the GL and every report (FR-021), and the app is unreleased.
- A card or electronic-transfer member payment that covers several fees still writes one bank line per fee, as payments do today. FR-013's one-line rule covers deposits only.

## 15. Debug seed data

- **Decision:** rework the existing chronological cash sweep in `DebugDataSeeder` rather than adding a parallel one. Across both seeded years:
  - Some member payments are by cheque (with references), some by card or electronic transfer.
  - The council grant arrives by electronic transfer, and the bowling-club sponsorship is a cheque.
  - Every few weeks a deposit takes the waiting cheques plus the cash above the petty-cash float.
  - A float is withdrawn before each annual concert and banked back with the takings.
  - One deposit is reversed and recorded again.
  - Opening balances include a small Undeposited Funds amount.

  Everything is posted in date order, so every deposit passes the FR-010 cash check.
- **Rationale:** FR-024, together with CLAUDE.md's two-year rule, requires this. The old typed-amount deposit API is removed.
