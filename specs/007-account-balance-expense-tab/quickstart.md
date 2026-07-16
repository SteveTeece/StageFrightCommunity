# Quickstart: Chart of Accounts Balance Column & Record Expense Tab

Manual validation guide for the two independent user stories in this feature. Run each story's scenarios separately — neither depends on the other.

## Prerequisites

```bash
dotnet restore
dotnet build
dotnet run --project src/StageFright.App/
```

The SQLite database at `<repo-root>/TestData/stagefright.db` auto-migrates on first run. If you want a quick populated dataset, the app's debug data seeder (`src/StageFright.App/Seeding/DebugDataSeeder.cs`) already creates income/expense accounts and sample GL activity.

## Story 1 — Balance column in Chart of Accounts (P1)

1. Navigate to **Finance → Chart of Accounts** (`/finance/accounts`).
2. **Active accounts show a balance (FR-001, FR-003)**: confirm every row in "Active Accounts" shows a currency-formatted Balance value, including system accounts (Cash on Hand, Member Receivable).
3. **Matches Trial Balance (FR-004/SC-003)**: open **Finance → Reports → Trial Balance**, note the Debit/Credit figures for a couple of accounts, and confirm the Chart of Accounts Balance for those same accounts is consistent (net debit for Asset/Expense, net credit — shown positive — for Liability/Equity/Income).
4. **Zero-activity account (Acceptance Scenario 3)**: create a brand-new account via the "Add Account" form, reload the page, and confirm its Balance shows as `$0.00`, not blank.
5. **Archived accounts show a balance (FR-002)**: archive a non-system account (or use one already archived) and confirm its Balance still shows in the "Archived Accounts" section.
6. **Sorting (FR-006)**: click the Balance column header in either grid and confirm accounts reorder ascending/descending by balance, same as clicking "No." or "Name" already does.
7. **Fresh-load recompute (FR-005)**: record an expense or income affecting an account you're watching (see Story 2, or use Record Member Payment), return to Chart of Accounts, and confirm that account's Balance reflects the new activity immediately.
8. **Static, no drill-through (FR-013)**: confirm clicking a Balance cell does nothing — no navigation, no modal.

## Story 2 — Record Expense tab on Finance Overview (P2)

1. Navigate to **Finance** (`/finance`) — the Finance Overview screen.
2. **Tab present and positioned (FR-008)**: confirm the tab order reads Outstanding, Record Member Payment, Record Income, **Record Expense**, Apply Annual Fees.
3. **Full functionality on the tab (FR-009)**: select "Record Expense", fill in Date/Amount/Paid from/Expense account, submit, and confirm the same success message and "Record Another" behavior as before.
4. **Menu no longer lists it (FR-007)**: open the main navigation's Finance menu and confirm "Record Expense" is no longer a sub-item (Chart of Accounts, Transfers, Journal Entries, Reconciliation, Opening Balances still are).
5. **Direct route still works (FR-010)**: navigate directly to `/finance/expenses` (e.g. paste the URL or use a bookmark) and confirm the same expense form renders as a standalone page.
6. **Tab state survives reload/back-forward (FR-011)**: with the Record Expense tab selected, reload the page (or use browser back after switching tabs) and confirm the Record Expense tab remains selected, matching the existing behavior of the other tabs.

## Automated verification

```bash
dotnet test tests/StageFright.Core.Tests/ --filter "FullyQualifiedName~AccountBalanceService"
dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~ChartOfAccountsPage|FullyQualifiedName~FinancePage"
dotnet test
```

The full `dotnet test` run (all five test projects) must pass before considering the feature complete, per this repo's build/test verification rule.
