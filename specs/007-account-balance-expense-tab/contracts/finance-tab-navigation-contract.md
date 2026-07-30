# Contract: Finance Overview tab order and navigation state

This documents the internal contract governing the Finance Overview screen's tabs (`FinancePage.razor`/`.razor.cs`) and their relationship to the Finance navigation menu (`FinanceMenuItemProvider`). No public/external API is exposed by this application, so — as with the other contracts in this feature — this is the closest analog: the shared convention every tab on this screen must follow, extended here to add "Record Expense".

## Tab order (FR-008)

`FinancePage.razor`'s `<Tabs>` MUST render, in this exact order:

1. Outstanding (`MemberBalanceList`)
2. Record Member Payment (`PaymentForm`)
3. Record Income (`RecordIncome`)
4. **Record Expense (`ExpensePaymentPage`) — new**
5. Apply Annual Fees (link to `/finance/annual-fees`)

## Tab → query-string key mapping (FR-011)

Each `Tab`'s `OnShown` callback MUST call `NavToTab(key)`, which navigates to `/finance?tab={key}` with `replace: true`. `FinancePage.razor.cs`'s `OnInitialized` MUST map `TabQuery` back to `DefaultTabIndex` using the same keys, so reload/back/forward restores the previously selected tab:

| Tab | `OnShown` key | `DefaultTabIndex` |
|---|---|---|
| Outstanding | `"outstanding"` | `0` (default/fallback) |
| Record Member Payment | `"record-payment"` | `1` |
| Record Income | `"record-income"` | `2` |
| **Record Expense** | `"record-expense"` | `3` |
| Apply Annual Fees | `"annual-fees"` | `4` (was `3` before this feature) |

This is a pure index/key insertion — no existing key or index for Outstanding/Record Member Payment/Record Income changes; only "Apply Annual Fees" shifts from index 3 to 4.

## Tab content contract (FR-009)

The "Record Expense" tab's `<Content>` renders `<ExpensePaymentPage />` directly — the same component instance type that answers the standalone `/finance/expenses` route (see the standalone-route rule below). No new component, no prop/parameter differences, no wrapping logic: the tab must present byte-for-byte the same form, validation, and save behavior as the pre-existing standalone page.

## Standalone route survival (FR-010)

`ExpensePaymentPage.razor`'s `@page "/finance/expenses"` directive MUST remain unchanged. A component decorated with `@page` continues to function as an ordinary child component when embedded elsewhere (the tab, per above) — embedding it in a tab MUST NOT involve removing, conditionalizing, or duplicating its `@page` directive. Deep-linking directly to `/finance/expenses` MUST continue to render the same form as a standalone page, consistent with how `Record Member Payment`/`Record Income` already behave when reached without going through a tab.

## Menu contract (FR-007)

`FinanceMenuItemProvider.GetMenuItems()`'s `Finance` sub-item list MUST NOT include an entry with `Title = "Record Expense"`. All other sub-items (`Overview`, `Chart of Accounts`, `Transfers`, `Journal Entries`, `Reconciliation`, `Opening Balances`) are unaffected — their `Route`/`DisplayOrder` values do not need to change (gaps in `DisplayOrder` are harmless; the provider does not require a contiguous sequence). This menu change does not affect the route contract above — `/finance/expenses` keeps working; it's simply no longer listed in the nav menu.
