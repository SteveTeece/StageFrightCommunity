# Phase 0 Research: Bank Deposit Recording

## 1. Distinct classification without a schema change (FR-011)

**Decision**: Add a new member `BankDeposit` to `JournalEntryType` (`StageFright.Core/Enums/JournalEntryType.cs`), alongside the existing `Income`, `ExpensePayment`, `Transfer`, `GeneralJournal`, `OpeningBalance`. `BankDepositService` posts new deposits with `JournalEntry.Type = JournalEntryType.BankDeposit`; every pre-existing row keeps `JournalEntryType.Transfer` untouched.

**Rationale**:
- `JournalEntryConfiguration.Configure` already maps `Type` with `builder.Property(j => j.Type).HasConversion<string>().IsRequired()` — the column is a plain string, so adding an enum member is a pure code-level change with **no EF Core migration**.
- This directly satisfies the clarified FR-011 ("its own distinct classification... even though both still post matching debit/credit entries the same way") with the smallest possible change: one enum member, no new table, no new FK.
- No report or repository code branches on `JournalEntryType` today (confirmed — `StageFright.Reports` has zero references to the enum; `AccountRegisterReportProvider`/`TrialBalanceReportProvider` read `Transaction`/`Account` rows directly). So introducing a new member cannot break existing report rendering, and satisfies FR-009 for free — historical `Transfer` rows are literally never read differently based on type.

**Alternatives considered**:
- A new `boolean IsBankDeposit` flag on `JournalEntry` instead of a new enum member — rejected: less discoverable than a proper enum classification, and every other financial-workflow classification in this codebase (`Income`, `ExpensePayment`, `Transfer`, `GeneralJournal`, `OpeningBalance`) already uses this same enum, so a boolean flag would be an inconsistent one-off.
- Reclassifying historical `Transfer` rows to `BankDeposit` retroactively — explicitly rejected by the spec's own Assumptions ("Existing historical transfer records remain valid and are not migrated or reclassified").

## 2. Retiring `AccountTransferService` vs. wrapping it (FR-002/FR-008)

**Decision**: Delete `AccountTransferService`/`IAccountTransferService`/`RecordTransferRequest` outright and replace them with a new `BankDepositService`/`IBankDepositService`/`RecordBankDepositRequest`, rather than keeping the generic any-two-accounts service around and having a thin bank-deposit wrapper call into it.

**Rationale**:
- `AccountTransferService` has exactly one production caller today: `TransferPage` (being retired per FR-008) and one debug-seeding call-site (`DebugDataSeeder.cs:491`, which already passes `FromAccountId = SystemAccounts.CashId` — i.e. it's already only ever used as a bank-deposit in practice). No other module or page depends on "arbitrary any-two-bank-account transfer" as a capability; that capability already fully exists via `GeneralJournalService`/`JournalEntryPage` (multi-line, any-account postings), so keeping a second implementation of the same idea around after the UI that used it is retired would be dead/duplicate code.
- Constitution §3.1 ("prefer clarity... avoid cleverness") and CLAUDE.md ("don't add features... beyond what the task requires") both favor a direct one-for-one replacement over introducing a wrapper layer whose only job would be to always pass `FromAccountId = SystemAccounts.CashId` into a class that itself becomes pure dead weight.

**Alternatives considered**:
- Keep `AccountTransferService` for internal/future use and add `BankDepositService` as a thin wrapper — rejected: this leaves a fully generic, no-longer-UI-reachable transfer capability in the codebase that duplicates `GeneralJournalService`, which is exactly the "duplicate functionality" complaint issue #237 raised. It also fails constitution §3.1's "no half-finished implementations" spirit (a service with no real caller left in production).

## 3. Fixed source account and destination-picker scope (FR-002/FR-003/FR-007)

**Decision**: `RecordBankDepositRequest` carries only `Date`, `Amount`, `ToAccountId`, `Description` — no `FromAccountId` field. `BankDepositService.RecordDepositAsync` always uses `SystemAccounts.CashId` as the source/credit side internally. `BankDepositPage` loads bank accounts via the existing `IAccountService.GetBankAccountsAsync()` and filters out `SystemAccounts.CashId` client-side (`banks.Where(a => a.Id != SystemAccounts.CashId)`) to build the destination picker's option list; if that filtered list is empty, the page shows the same "add a bank account first" warning `TransferPage` already shows when fewer than two bank accounts exist, linking to `/finance/accounts` (FR-007).

**Rationale**: `IAccountService.GetBankAccountsAsync()` already exists and returns every `IsBankAccount` account, including Cash on Hand; filtering it in the page mirrors how `IncomeEntryService`/`RecordIncome` already leaves deposit-account filtering to the caller (no new repository/service method is needed for this). Making the source fixed and non-selectable at the request-model level (rather than merely hidden in the UI) prevents a malformed/future caller from ever picking an arbitrary source — the fixed-source rule (FR-002) is enforced by the shape of the contract, not just by UI convention.

**Alternatives considered**:
- Keep `FromAccountId` on the request and validate it always equals `SystemAccounts.CashId` — rejected: this leaves a field on the contract that can only ever hold one legal value, which is exactly the kind of no-op parameter CLAUDE.md's "don't add validation for scenarios that can't happen" / "trust internal code" guidance argues against. Omitting the field is simpler and makes the invariant a compile-time fact instead of a runtime check.

## 4. Validation and default description (FR-004/FR-005, Edge Cases)

**Decision**: `BankDepositService.RecordDepositAsync` validates, in order: `Amount <= 0m` → `ValidationException`; destination account not found → `EntityNotFoundException`; destination account `IsBankAccount == false` → `ValidationException`; destination account id `== SystemAccounts.CashId` → `ValidationException` (defensive — the UI never offers this option, but the service must not silently accept it from any other caller). Blank/whitespace `Description` defaults to `$"Bank deposit — {toAccount.Name}"`, following the exact same `string.IsNullOrWhiteSpace(...) ? default : trimmed` pattern already used by `AccountTransferService`/`IncomeEntryService`. No sufficient-funds check is added — deposits exceeding the current Cash on Hand balance are still recorded, exactly as `AccountTransferService` behaves today, per the spec's edge case ("consistent with today's transfer behavior, which does not enforce a sufficient-funds check").

**Rationale**: Directly implements FR-004/FR-005 and all three Edge Cases using the established validation/description-default pattern from sibling services — no new validation style is introduced. The default description format (`"Bank deposit — {account name}"`) satisfies the edge case's example ("e.g., 'Bank deposit'") while remaining identifiable per-entry in reports, consistent with how `Income`/`Transfer` already suffix their default descriptions with the relevant account name.

**Alternatives considered**:
- Literal default description "Bank deposit" with no account name — rejected: every sibling service (`Income`, `Transfer`) includes the relevant account name in its default description for report identifiability; dropping that here would be an inconsistent regression for entries with no description.

## 5. Test coverage approach

**Decision**:
- `BankDepositServiceTests` (unit, `StageFright.Core.Tests`) replacing `AccountTransferServiceTests` — covering: zero/negative amount, destination not found, destination not a bank account, destination = Cash on Hand, successful DR-destination/CR-cash posting under `JournalEntryType.BankDeposit`, default description, unit-of-work wrapping, audit logging. (The old `SourceEqualsDestination`/`SourceAccountDoesNotExist`/`SourceIsNotABankAccount` cases no longer apply — there is no source parameter — and are replaced by the destination-equals-cash case.)
- `V14_ExpensesTransfersTests`'s "Transfers" region (`--- Transfers ---`) is updated in place: `BuildTransferService()` → `BuildBankDepositService()`, `RecordTransferRequest`/`RecordTransferAsync` → `RecordBankDepositRequest`/`RecordDepositAsync` (no `FromAccountId`), and the journal-entry-type assertion changes from `JournalEntryType.Transfer` to `JournalEntryType.BankDeposit`. The "same account" validation case becomes "destination = Cash on Hand" instead of "From == To".
- New `BankDepositPageTests` (bUnit, `StageFright.UI.Tests`) — loading state, zero-eligible-destination warning (only Cash on Hand exists as a bank account), successful submit + "Record Another", and each client-side validation message (amount, destination required) — this is new coverage since `TransferPage` had none.
- `FinanceMenuItemProviderTests`'s `[InlineData("Transfers")]` case is updated to `[InlineData("Record Bank Deposit")]`.

**Rationale**: Every changed or new code path (service validation ×4, successful posting, default description, audit log, UI loading/warning/validation/success states, menu label) gets deterministic coverage per constitution §11, mirroring this repo's established `Should_[ExpectedBehavior]_When_[Condition]` naming and reusing the nearest sibling test files as templates, consistent with how spec 007 approached its own test-coverage plan.
