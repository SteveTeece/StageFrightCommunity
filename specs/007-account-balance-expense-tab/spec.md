# Feature Specification: Chart of Accounts Balance Column & Record Expense Tab

**Feature Branch**: `007-account-balance-expense-tab`

**Created**: 2026-07-16

**Status**: Draft

**Input**: User description: "create a new spec to implement issues #235 and #243. These issues are similar and can be handled in a single feature" — GitHub #235 "Add Balance column to Chart of Accounts listing" and #243 "Move Record Expense menu item to Overview" (move Record Expense from the Finance menu to a new tab on the Finance Overview screen).

## Clarifications

### Session 2026-07-16

- Q: What should the Chart of Accounts display if an account's balance cannot be computed (e.g. a GL calculation/data error) when the grid loads? → A: Show an inline error indicator (e.g. "—" or an error icon) for that row only; the rest of the grid loads normally.
- Q: Where should the new "Record Expense" tab sit within the Finance Overview screen's existing tab order (Outstanding, Record Member Payment, Record Income, Apply Annual Fees)? → A: Immediately after "Record Income", before "Apply Annual Fees".
- Q: Should the Balance column value in the Chart of Accounts be clickable/support drill-through to that account's underlying transactions, or is it a static display-only figure for this feature? → A: Static, display-only value — no drill-through in this feature.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See account balances in the Chart of Accounts (Priority: P1)

A treasurer or committee member opens the Chart of Accounts screen to review the club's accounts and, without visiting a separate report, wants to see how much is currently in (or owed on) each account.

**Why this priority**: Balance visibility is the primary reason to open the Chart of Accounts. Today the list only shows account number, name, and type — a user must run a separate report (e.g. Trial Balance) just to see how much money is in each account, which is the single biggest gap in this screen's usefulness.

**Independent Test**: Can be fully tested by opening the Chart of Accounts screen and confirming a Balance value is shown for every listed account, matching the figures on the Trial Balance report for the same accounts. Delivers value on its own with no dependency on the Record Expense change.

**Acceptance Scenarios**:

1. **Given** the Chart of Accounts screen is open, **When** the Active Accounts list loads, **Then** each account row shows its current balance alongside its existing account number, name, and type.
2. **Given** an account has had fee, payment, or transaction activity posted against it, **When** the Chart of Accounts is viewed, **Then** the displayed balance reflects that activity and matches the figure shown for the same account on the Trial Balance report.
3. **Given** an account has never had any activity posted against it, **When** the Chart of Accounts is viewed, **Then** its balance is shown as zero rather than blank or an error.
4. **Given** the user views the Archived Accounts list, **When** the list loads, **Then** each archived account also shows its balance.
5. **Given** the Balance column is displayed, **When** the user sorts the grid by that column, **Then** accounts are ordered by balance like any other sortable column on the grid.

---

### User Story 2 - Record an expense from the Finance Overview screen (Priority: P2)

A committee member working from the Finance Overview screen wants to record an expense without leaving that screen or hunting for it as a separate item in the Finance navigation menu.

**Why this priority**: This is a navigation/consolidation change rather than new financial capability — it improves discoverability and reduces menu clutter, but the underlying expense-recording functionality already exists and works today, so it delivers less standalone value than Story 1.

**Independent Test**: Can be fully tested by opening the Finance Overview screen, selecting the new Record Expense tab, and successfully recording an expense entirely from that screen — independent of whether the Balance column has been implemented.

**Acceptance Scenarios**:

1. **Given** the user is on the Finance Overview screen, **When** they view the available tabs, **Then** a "Record Expense" tab is present immediately after "Record Income" and before "Apply Annual Fees".
2. **Given** the user selects the Record Expense tab, **When** the tab is shown, **Then** the same expense-recording form and functionality previously reached via the standalone Finance menu item is presented, with no loss of fields, validation, or behavior.
3. **Given** the Finance navigation menu, **When** the user views the Finance menu's sub-items, **Then** "Record Expense" no longer appears as a standalone menu item.
4. **Given** a user has a saved bookmark or link to the previous direct Record Expense page, **When** they navigate to it, **Then** the expense-recording page still loads successfully (consistent with how the other Finance Overview tabs remain directly reachable).
5. **Given** the user switches to the Record Expense tab, **When** they use the browser back button or reload the page, **Then** the Record Expense tab remains selected, consistent with the behavior of the other Finance Overview tabs.

---

### Edge Cases

- What happens when an account's balance is negative relative to its normal accounting sign (e.g. an Income or Liability account showing a net debit)? The balance is still displayed as computed — no special-casing or hiding of unexpected signs, so users can spot the discrepancy.
- What happens when an individual account's balance cannot be computed (e.g. a GL calculation or data error)? That account's row shows an inline error indicator in the Balance column only; the rest of the grid continues to load and display normally (see FR-012).
- What happens when an archived account has a non-zero balance? The balance is still shown in the Archived Accounts list; archiving does not clear or hide it.
- How does the system handle a very large number of GL entries against one account when computing its balance for the grid? The balance is computed the same way as the existing Trial Balance/account-balance calculations elsewhere in the system, so performance characteristics match those existing screens.
- What happens if a user deep-links directly to the expense-recording route instead of going through the Finance Overview tab? The form still renders as a standalone page, the same way Record Member Payment and Record Income already do today.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Chart of Accounts Active Accounts list MUST display each account's current balance.
- **FR-002**: The Chart of Accounts Archived Accounts list MUST display each archived account's current balance.
- **FR-003**: Displayed balances MUST be formatted as currency, consistent with how monetary values are formatted elsewhere in the application.
- **FR-004**: Each account's balance MUST be calculated from the same general ledger debit/credit data used by existing financial reports (e.g. Trial Balance), so the figures agree.
- **FR-005**: The Balance column MUST reflect the most recently posted transactions each time the Chart of Accounts screen is loaded.
- **FR-006**: The Chart of Accounts grid MUST support sorting by the Balance column, consistent with the sorting already available on its other columns.
- **FR-007**: The Finance navigation menu MUST NOT display "Record Expense" as a standalone sub-item.
- **FR-008**: The Finance Overview screen MUST present a "Record Expense" tab positioned immediately after the "Record Income" tab and before "Apply Annual Fees", giving the tab order: Outstanding, Record Member Payment, Record Income, Record Expense, Apply Annual Fees.
- **FR-009**: Selecting the Record Expense tab MUST present the full existing expense-recording functionality (form fields, validation, and save behavior) unchanged from what was previously reached via the standalone menu item.
- **FR-010**: The previous direct route/URL used to reach the expense-recording page MUST continue to work as a standalone, directly-navigable page.
- **FR-011**: Selecting the Record Expense tab MUST update the page's URL/navigation state so that reloading the page or using browser back/forward keeps the correct tab selected, consistent with the other Finance Overview tabs.
- **FR-012**: If an individual account's balance cannot be computed (e.g. due to a data or calculation error), the Chart of Accounts MUST show an inline error indicator for that account's Balance cell only, while all other accounts' balances continue to load and display normally.
- **FR-013**: The Balance column MUST be a static, non-interactive value; it MUST NOT provide click-through/drill-down navigation to transaction detail or the Account Register report as part of this feature.

### Key Entities *(include if feature involves data)*

- **Account**: A Chart of Accounts entry (Asset, Liability, Equity, Income, or Expense type) that GL transactions post against. This feature adds a computed, read-only balance to how each account is displayed — no new attributes are stored on the entity itself.
- **Transaction**: An immutable GL debit/credit entry already posted against an Account; the source data used to compute the balance shown in the Chart of Accounts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can determine the current balance of any account without leaving the Chart of Accounts screen or running a separate report.
- **SC-002**: 100% of accounts shown in the Chart of Accounts (active and archived) display either a balance value or an inline error indicator — no account row is ever left blank.
- **SC-003**: Balances shown in the Chart of Accounts match the corresponding figures on the Trial Balance report at all times.
- **SC-004**: A user can record an expense entirely from the Finance Overview screen, without navigating to a separate menu item.
- **SC-005**: The Finance navigation menu's sub-item count is reduced by one (Record Expense removed) while the same functionality remains reachable within one click from the Finance Overview screen.

## Assumptions

- "Account" refers to the existing Chart of Accounts / GL account entity used throughout the Finance module (Asset/Liability/Equity/Income/Expense), not a separate categorization concept.
- Balance is computed as of the current moment using the same net debits-minus-credits calculation already used for existing financial reports, so no new balance-calculation rules are introduced by this feature — only its display on the Chart of Accounts screen is new.
- Archived accounts continue to display their balance using the same calculation as active accounts; archiving an account does not zero out or hide its historical balance.
- The Record Expense tab reuses the existing expense-recording form/functionality as-is; only its location in navigation changes, not its fields, validation, or save behavior.
- No new user roles or permissions are introduced — any user who could already view the Chart of Accounts or record an expense retains that same access after this change.
- Drill-through navigation from an account's balance to its underlying transactions (e.g. an Account Register view) is explicitly out of scope for this feature; the balance is a static display value only.
