# Phase 0 Research: Print Chart of Accounts

## Decision: Balance source — reuse `IAccountBalanceService`, don't re-derive from the GL

**Decision**: `ChartOfAccountsReportProvider` calls the same `IAccountBalanceService.GetActiveAccountBalancesAsync()` that `ChartOfAccountsPage` already calls to populate its grid.

**Rationale**: FR-008 requires the printed balance to match the screen's figure exactly, and FR-010 requires a per-account calculation failure to be isolated without blocking the rest of the report. `AccountBalanceService` already implements both — it try/catches `IGLRepository.GetAccountBalanceAsync` per account, flips sign for credit-normal types, and sets `HasError` instead of throwing. Calling the same service guarantees identical figures by construction and gets the error isolation for free, with zero duplicated logic.

**Alternatives considered**: Querying `IGLRepository` directly inside the new provider (the pattern `TrialBalanceReportProvider`/`BalanceSheetReportProvider` use for their own GL-derived totals). Rejected because it would re-implement the normal-side sign flip and per-account try/catch a second time, risking drift from the screen's figure the next time that logic changes in only one place.

## Decision: Print trigger — direct registry lookup + temp-file hand-off, not the `ReportViewer` modal

**Decision**: The Chart of Accounts screen's "Print Chart of Accounts" button resolves the provider via `IReportProviderRegistry.GetProvider("chart-of-accounts")`, calls `GenerateAsync` with the screen's current `includeBalances` toggle state, renders the result with the existing `IPdfReportRenderer`, writes it to a temp file, and opens it with `Process.Start(..., UseShellExecute = true)` — the same sequence `ReportViewer.PrintReport()` and `RehearsalList`'s attendance-roll print button already use.

**Rationale**: FR-013 requires the document to open automatically "consistent with how every other printable report in the system behaves" — and the system's other *quick-print-from-a-screen* buttons (attendance roll, event/AGM attendance sheets) all skip any preview UI and hand off straight to the OS. The `ReportViewer` modal is the Reports-menu experience (Story 3), which this feature also gets by simply registering the provider in DI — no separate code path needed there.

**Alternatives considered**: Navigating to `/reports?reportId=chart-of-accounts` and auto-triggering print from there. Rejected — it inserts a page transition and a visible "Generating…"/preview modal that Story 1's Independent Test doesn't describe ("click 'Print Chart of Accounts' ... a document opens"), and it would be the only screen-level print button in the app routed that way.

## Decision: Balance column is structurally absent when the option is off, not blank

**Decision**: `ReportData.Columns` and each `ReportRow.Cells` are built with two entries (No., Name) when `includeBalances` is off, and three (No., Name, Balance) when it's on — the Balance column is omitted from the collection entirely, not included with empty string values.

**Rationale**: FR-009 says the document "MUST NOT show a balance column" when the option is off — an empty-but-present column would still read as a column. `PdfReportRenderer` (and CSV export) already tolerate `Cells.Count` being shorter than `Columns.Count` for a row (existing renderer code pads with empty string), so a per-generation column count that depends on the filter needs no renderer change.

**Alternatives considered**: Always emitting three columns with the Balance cell left blank when the option is off. Rejected — visibly contradicts FR-009's wording and would print an empty column header.

## Decision: System/bank indicator is a plain-text suffix on the Name cell, not its own column

**Decision**: When an account is a system account and/or bank/cash account, its Name cell gets a parenthetical suffix, e.g. `Cash on Hand (System, Bank)`.

**Rationale**: The spec's edge case explicitly calls for these indicators to render "just as plain text suitable for print," contrasting with the on-screen badge widgets. A suffix keeps the row to two/three columns total and needs no new header or alignment rule for what is, for most rows, an entirely absent flag.

**Alternatives considered**: A dedicated "Flags" column. Rejected as unnecessary width/complexity for two booleans that are false on the overwhelming majority of rows.

## Decision: `DisplayOrder = 15` in the Finance report menu

**Decision**: `ChartOfAccountsReportProvider.DisplayOrder` is `15`, placing it between Income Statement (`10`) and Trial Balance (`20`) in the Reports menu's Finance section.

**Rationale**: Existing Finance `DisplayOrder` values (10, 20, 25, 30, 35, 40, 50, 60) already leave room at 15; a structural chart-of-accounts listing is the natural report to see before the balance-driven ones (Trial Balance, Balance Sheet, etc.), and no existing provider's `DisplayOrder` needs to change.

**Alternatives considered**: Appending at the end (`70`). Rejected — it would bury a foundational, low-complexity report behind six other reports for no functional reason.
