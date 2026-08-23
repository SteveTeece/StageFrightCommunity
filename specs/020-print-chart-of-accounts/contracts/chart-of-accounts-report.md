# Contract: Chart of Accounts Report

This is the interface a consumer (the Chart of Accounts screen, the Reports menu, and their tests) codes against. Identifiers here are pinned exactly — do not rename, recase, or pluralize any of them.

## `IReportProvider` implementation — `ChartOfAccountsReportProvider`

| Member | Value |
|---|---|
| `ReportId` | `"chart-of-accounts"` |
| `ReportName` | `"Chart of Accounts"` |
| `ModuleName` | `"Finance"` |
| `DisplayOrder` | `15` |

### `Filters`

One `ReportFilterDefinition`:

| `Key` | `Type` | `Label` | `DefaultValue` |
|---|---|---|---|
| `includeBalances` | `ReportFilterType.Boolean` | `"Include Current Balances"` | `"false"` |

### `GenerateAsync` output contract

- `Title`: `"Chart of Accounts"`
- `Sections`: exactly five, in this fixed order, with these exact headings: `"Assets"`, `"Liabilities"`, `"Equity"`, `"Income"`, `"Expenses"`. A section with no matching accounts still appears, with zero rows.
- Each section's `Rows` are ordered by `AccountNumber` ascending.
- `Columns` / each row's `Cells`:
  - `includeBalances` unset or `"false"` → `Columns = ["No.", "Name"]`; `Cells.Count == 2` per row.
  - `includeBalances == "true"` → `Columns = ["No.", "Name", "Balance"]`; `Cells.Count == 3` per row.
- Name cell text: account name, followed by `" (System)"`, `" (Bank)"`, or `" (System, Bank)"` when the corresponding flag(s) are set; no suffix otherwise.
- Balance cell text (only present when `includeBalances == "true"`): the formatted balance (`"F2"`-style, matching `TrialBalanceReportProvider`'s convention) when `AccountBalance.HasError` is `false`; a fixed error-indicator string (e.g. `"Error"`) when `HasError` is `true`.
- `GrandTotal`: always `null`.
- `SummaryColumns`: always `null`.
- Archived accounts never appear in any section.

## `ChartOfAccountsPage` UI contract

| Element | Value |
|---|---|
| Print button label (verbatim per spec) | `Print Chart of Accounts` |
| Include-balances toggle | `RadzenSwitch`, default unchecked (off) |

### Behavior

- Clicking "Print Chart of Accounts" resolves `IReportProviderRegistry.GetProvider("chart-of-accounts")`, calls `GenerateAsync` with `includeBalances` set to the toggle's current state (`"true"`/`"false"`), renders the result via `IPdfReportRenderer`, writes it to a temp `.pdf` file, and opens it via `Process.Start(..., UseShellExecute = true)` — no intermediate preview UI.
- A render/launch failure surfaces a friendly in-page error and does not throw out of the click handler (matches `ReportViewer.PrintReport()`'s existing catch pattern).

## Reports-menu surface (Story 3 — no new UI code)

Registering `ChartOfAccountsReportProvider` in `MauiProgram.cs` is sufficient: `ReportMenuItemProvider` and `ReportsPage`/`ReportViewer` already build the Finance section, the on-screen preview, the `includeBalances` filter control, and CSV export generically from `IReportProviderRegistry` — this contract's `ReportId`/`Filters`/`GenerateAsync` shape above is the entire surface those consume.
