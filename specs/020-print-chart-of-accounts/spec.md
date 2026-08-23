# Feature Specification: Print Chart of Accounts

**Feature Branch**: `020-print-chart-of-accounts`
**Created**: 2026-08-23
**Status**: Draft
**Source**: [GitHub Issue #303](https://github.com/SteveTeece/StageFrightCommunity/issues/303) — "[FEATURE] Add report to print Chart of Accounts"

## User Scenarios & Testing

### User Story 1 - Print the chart of accounts structure (Priority: P1)

A committee member on the Chart of Accounts screen wants a printed copy of the organisation's account structure — every active account, grouped the way the ledger itself is organised — so they can review it away from the screen, file it, or hand it to an auditor or incoming treasurer.

**Why this priority**: This is the entire request in Issue #303 and delivers value with no other story required — a printable chart of accounts is useful on its own even before balances are added.

**Independent Test**: From the Chart of Accounts screen, click "Print Chart of Accounts" with the balance option left off. A document opens listing every active account, grouped under its account type, in account-number order — independently verifiable without any other part of this feature.

**Acceptance Scenarios**:

1. **Given** the Chart of Accounts screen with active accounts across multiple types, **When** the user clicks "Print Chart of Accounts", **Then** a document opens grouping the accounts under headings for Assets, Liabilities, Equity, Income, and Expenses, in that order.
2. **Given** two active accounts of the same type, **When** the report is generated, **Then** they appear within their type's section ordered by account number, lowest first.
3. **Given** an archived account, **When** the report is generated, **Then** that account does not appear anywhere in the printed document.
4. **Given** the balance option is left off, **When** the report is generated, **Then** no balance figures appear anywhere in the document.

---

### User Story 2 - Include current account balances (Priority: P2)

A treasurer wants to optionally add each account's current balance to the printed chart of accounts, so the same document can double as a quick balance-position snapshot without running a separate report.

**Why this priority**: Directly requested in Issue #303 as a secondary option; it enhances Story 1's output but the base printed list already delivers value without it.

**Independent Test**: Turn on the "include current account balances" option on the Chart of Accounts screen, then print. The resulting document shows each account's current balance alongside its number and name — verifiable independently of Story 3.

**Acceptance Scenarios**:

1. **Given** the "include current account balances" option is turned on, **When** the user prints the chart of accounts, **Then** every account row shows its current balance next to its number and name.
2. **Given** the option is turned on and one account's balance cannot be calculated, **When** the report is generated, **Then** that account's row shows an error indicator in place of a balance and every other account's balance still prints correctly.
3. **Given** the option was left on from a previous print, **When** the user turns it off and prints again, **Then** the new document contains no balance column.
4. **Given** the option is on, **When** the report is generated, **Then** the balance shown for each account matches the balance currently displayed for that account on the Chart of Accounts screen.

---

### User Story 3 - Generate the report from the Reports menu (Priority: P3)

Any user browsing the central Reports menu wants to find "Chart of Accounts" alongside the organisation's other reports, so it's discoverable without needing to know it also lives on the Chart of Accounts screen, and so it can be exported to a spreadsheet like every other report.

**Why this priority**: Not explicitly requested in the issue, but it costs little once Story 1 exists (every report in this system is built the same way) and gives the report CSV export and menu discoverability for free — genuinely optional and safe to defer.

**Independent Test**: Open the Reports menu, select "Chart of Accounts", generate it, and export it to a spreadsheet file — verifiable entirely independently of the Chart of Accounts screen.

**Acceptance Scenarios**:

1. **Given** the Reports menu, **When** the user opens it, **Then** "Chart of Accounts" appears listed under the Finance section.
2. **Given** the "Chart of Accounts" report is open in the Reports menu, **When** the user generates it, **Then** the same account grouping and ordering as Story 1 is shown, with an on-screen option to include balances.
3. **Given** the report has been generated from the Reports menu, **When** the user exports it to a spreadsheet file, **Then** the exported content matches what was shown on screen.

## Edge Cases

- Only the built-in system accounts exist (no user-created accounts yet): the report still prints, with each account type section showing whatever accounts exist for it; a type with no accounts at all still shows its heading with no rows beneath it, consistent with how other grouped reports in the system behave.
- An account name is unusually long: the printed document wraps or fits it using the same rendering already used by every other report — no special handling needed.
- A system account or a bank/cash account is included: the printed row visibly indicates this (as the on-screen list already does with badges), just as plain text suitable for print.
- The balance option is toggled between separate prints, or between the Chart of Accounts screen and the Reports menu: each print reflects only the option's state at the moment that specific print was requested.
- Because account types are a mix of debit-normal and credit-normal balances, the report never prints a combined grand-total figure — summing them would not be meaningful.

## Requirements

### Functional Requirements

- **FR-001**: The Chart of Accounts screen MUST provide a "Print Chart of Accounts" button.
- **FR-002**: The Chart of Accounts screen MUST provide an on/off option, defaulted to off, labeled to indicate it includes current account balances on the printed report.
- **FR-003**: Clicking "Print Chart of Accounts" MUST produce a document listing every active (non-archived) account.
- **FR-004**: The document MUST group accounts into sections by account type, in the fixed order Assets, Liabilities, Equity, Income, Expenses.
- **FR-005**: Within each type section, accounts MUST be ordered by account number, ascending.
- **FR-006**: Each account row MUST show the account's number and name, with a visible indication when the account is a system account and/or a bank/cash account.
- **FR-007**: When the include-balances option is on at the time of printing, every account row MUST additionally show that account's current balance.
- **FR-008**: A current balance shown on the report MUST match the balance the Chart of Accounts screen shows for that same account.
- **FR-009**: When the include-balances option is off, the document MUST NOT show a balance column or any balance figures.
- **FR-010**: If an individual account's current balance cannot be calculated, that account's row MUST show an error indicator instead of a balance value, and every other account's row MUST still print normally.
- **FR-011**: The document MUST NOT include archived accounts.
- **FR-012**: The document MUST NOT show a combined grand-total balance figure.
- **FR-013**: The generated document MUST open automatically for the user, consistent with how every other printable report in the system behaves.
- **FR-014**: The Chart of Accounts report MUST also be available from the system's central Reports menu, offering the same account grouping, the same include-balances option, and export to a spreadsheet file.

### Key Entities

- **Account**: An entry in the organisation's chart of accounts. For this feature, its relevant attributes are its account number and name (both shown on every printed row), its account type (Asset, Liability, Equity, Income, or Expense — determines which section it prints under and its hierarchy position), whether it is a protected system account or a bank/cash account (both shown as an indicator), and whether it is archived (archived accounts are excluded from the printed report). Its current balance is not stored on the account itself — it is calculated at print time from the account's transaction history, the same way the Chart of Accounts screen already calculates it.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A user on the Chart of Accounts screen can produce a printed listing of all active accounts, correctly grouped by type, in a single click.
- **SC-002**: 100% of active accounts appear in the printed report, each placed under its correct account-type section and ordered correctly by account number within that section.
- **SC-003**: 0% of archived accounts appear anywhere in the printed report.
- **SC-004**: When the include-balances option is on, 100% of accounts without a balance-calculation error show a balance that matches the figure shown for that account on the Chart of Accounts screen at the time of printing.
- **SC-005**: When the include-balances option is off, the printed report contains zero balance figures.
- **SC-006**: The same report's content, generated from the Reports menu and exported to a spreadsheet file, matches what was shown on screen with no missing or altered rows.

## Assumptions

- The "account hierarchy" requested in Issue #303 is the organisation's existing type-and-number structure (Assets, Liabilities, Equity, Income, Expenses, ordered by account number within each) — the same grouping already used by this system's other account-driven reports. The Account entity carries no separate parent/child hierarchy field, so no deeper nesting is assumed.
- The include-balances option defaults to off, so the base printed document is a clean structural listing of the chart of accounts; balances are an explicit opt-in addition.
- "Checkbox" in the issue describes an on/off choice, not a specific control widget — implemented as this system's standard toggle control, matching every other on/off option elsewhere in the app.
- The printed report reflects only active accounts, matching the accounts shown in the screen's "Active Accounts" section that the print button sits alongside; archived accounts (shown separately on screen) are out of scope for this printed document.
- Routing the report through the system's existing shared report pipeline (Story 3) is an assumed low-cost extension beyond the issue's literal ask, chosen because every other report in the system is built this way and it adds spreadsheet export and menu discoverability for negligible extra cost; it can be dropped without weakening Stories 1–2 if not wanted.

## Verbatim Constraints

- Button label: `Print Chart of Accounts`
