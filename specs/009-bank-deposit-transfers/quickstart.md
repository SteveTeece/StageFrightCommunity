# Quickstart: Bank Deposit Recording

Manual validation guide for the three user stories in this feature.

## Prerequisites

```bash
dotnet restore
dotnet build
dotnet run --project src/StageFright.App/
```

The SQLite database at `<repo-root>/TestData/stagefright.db` auto-migrates on first run. The app's debug data seeder (`src/StageFright.App/Seeding/DebugDataSeeder.cs`) already exercises the bank-deposit workflow when sweeping excess cash to a bank account after rehearsal attendance fees, so a freshly seeded database will already contain sample deposits.

## Story 1 — Record a bank deposit of collected cash (P1)

1. Navigate to **Finance → Record Bank Deposit** (`/finance/bank-deposit`).
2. **Fixed source, no picker (FR-002)**: confirm the form shows Cash on Hand as the fixed source (not a selectable dropdown).
3. **Record a deposit (Acceptance Scenario 1)**: note the current Cash on Hand and a destination bank account's balance (e.g. via Chart of Accounts), enter a date, amount (e.g. $300), pick the destination bank account, and submit. Confirm Cash on Hand decreases and the destination account increases by exactly that amount, and no other account changes.
4. **Reject invalid amount (Acceptance Scenario 2)**: try to submit with $0 or a negative amount and confirm a clear validation message appears and no ledger entry is created.
5. **Consistent activity records (Acceptance Scenario 3)**: after a successful deposit, view account activity (e.g. Account Register report) for both Cash on Hand and the destination bank account and confirm both show the deposit with matching date, amount, and description.

## Story 2 — One clear workflow instead of two overlapping ones (P2)

1. **Bank-deposit-specific form (Acceptance Scenario 1)**: confirm the page previously labeled "Transfer" is now titled "Record Bank Deposit" and shows a fixed cash source + destination bank-account picker, not a generic any-account-to-any-account picker.
2. **Generic movement still available via Journal Entry (Acceptance Scenario 2)**: navigate to **Finance → Journal Entries** (`/finance/journal`) and confirm the ability to move funds between two arbitrary accounts (e.g. two bank accounts, for a reason other than depositing collected cash) is still available there, unchanged.
3. **No eligible destination (Edge Case)**: in a database with only Cash on Hand as a bank account, confirm `/finance/bank-deposit` shows a clear message directing the treasurer to add a bank account first (linking to `/finance/accounts`), rather than allowing an invalid submission.
4. **Blank description default (Edge Case)**: submit a deposit with no description and confirm it is recorded with a standard default description (e.g. "Bank deposit — {account name}") rather than a blank one.

## Story 3 — Historical transfers remain accurate in reports (P3)

1. Using a database with pre-refactor `Transfer`-typed entries (or the pre-existing seeded/historical data), run **Finance → Reports → Account Register** for a date range covering those entries.
2. **Historical accuracy (Acceptance Scenario 1)**: confirm each historical transfer entry still displays with its original accounts, debit/credit amounts, and date — unaffected by this feature.
3. Confirm **Finance → Reports → Trial Balance** for the same period still balances (Σdebits = Σcredits) and matches figures from before this change shipped.

## Automated verification

```bash
dotnet test tests/StageFright.Core.Tests/ --filter "FullyQualifiedName~BankDepositService"
dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~BankDepositPage"
dotnet test tests/StageFright.Integration.Tests/ --filter "FullyQualifiedName~V14_ExpensesTransfersTests"
dotnet test
```

The full `dotnet test` run (all five test projects) must pass before considering the feature complete, per this repo's build/test verification rule.
