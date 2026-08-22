# Reports Pipeline — Living Spec

> [DRAFT] Surface-first draft from existing code — every requirement is observed from the code surface unless tagged otherwise. Review before trusting.

## Purpose
The reports pipeline turns member, finance, and committee data into on-screen, printable, and exportable output through one shared contract and data model, so every module can add a new report without writing its own PDF/CSV/pagination logic. Without it, each module would hand-roll its own export code and there would be no single, consistent place to browse, filter, print, or export organisational reports.

## Requirements

### Reports are contributed through one provider contract, discovered automatically
Every report — regardless of which module owns it — MUST implement `IReportProvider` (`ReportId`, `ReportName`, `ModuleName`, `DisplayOrder`, `Filters`, `GenerateAsync`) and be registered in DI. The registry collects every registered provider via a constructor-injected `IEnumerable<IReportProvider>` rather than any hard-coded list, so adding a report never requires touching the registry, the menu, or the viewer.

#### Scenario: a module wants to add a new report
- **WHEN** a new class implementing `IReportProvider` is registered in the DI container
- **THEN** it automatically appears in the Reports menu and can be generated, printed, and exported without any other code change

### The registry orders and deduplicates providers defensively
The registry MUST group providers into menu sections ordered Members first, Finance second, then every other module alphabetically, and MUST silently skip (with a logged warning) any provider whose `ReportId` collides with one already registered, rather than throwing or overwriting.

#### Scenario: two providers register under the same ReportId
- **WHEN** the registry is constructed and a duplicate `ReportId` is found
- **THEN** the later provider is dropped, a warning is logged, and the first-registered provider keeps serving that ID

#### Scenario: providers span several modules
- **WHEN** the menu is built
- **THEN** Members' reports appear first, Finance's second, and any other module's reports follow in alphabetical order

### The Reports menu is generated from the registry, not hand-maintained
`ReportMenuItemProvider` MUST build its navigation sub-items entirely from `IReportProviderRegistry.GetMenuSections()`, so the nav always matches what is actually registered rather than a separately maintained list.

#### Scenario: a report provider is added or removed
- **WHEN** the application starts with the updated provider set
- **THEN** the Reports nav group reflects the change with no edits to the menu provider itself

### All report output — screen, PDF, and CSV — is driven from one renderer-agnostic data model
A provider's `GenerateAsync` MUST return a single `ReportData` structure (columns, sections, rows, optional subtotals/grand total) that both `PdfReportRenderer` and `ICsvReportExporter` consume without any provider-specific branching. Providers MUST pre-format every cell into its final display string (currency, dates, etc.) before returning, since no renderer re-derives formatting from column metadata at render time.

#### Scenario: a report is both printed and exported
- **WHEN** the same generated `ReportData` is passed to Print/PDF and to Export CSV
- **THEN** both outputs show identical row content and totals, sourced from the same object

### Report filters are provider-declared and rendered generically
A provider MAY declare zero or more `ReportFilterDefinition` entries; the viewer renders a control purely from each definition's `Type` (Select, Boolean, Text, or Date) without any report-specific UI code, and pre-populates each filter's default value whenever a provider is selected.

#### Scenario: a provider declares a Select filter with options and a default
- **WHEN** the report is opened
- **THEN** a dropdown renders with those options, pre-selected to the declared default, and regenerating re-reads the current filter values

[NEEDS CLARIFICATION: `ReportFilterType.DateRange` is declared but no current provider uses it, and the viewer's filter panel has no dedicated case for it (falls through to rendering a single date input) — is a range control still planned, or is this a dead enum value?]

### Report generation runs synchronously behind a blocking indicator, with cancellation offered only for slow reports
Report generation MUST show a "Generating…" indicator immediately and MUST NOT reveal a Cancel action until 5 seconds have elapsed, so fast reports never flash unnecessary chrome while slow ones remain interruptible.

#### Scenario: a report resolves in under 5 seconds
- **WHEN** `GenerateAsync` completes quickly
- **THEN** the modal closes and the report renders without a Cancel button ever appearing

#### Scenario: a report is still generating after 5 seconds
- **WHEN** 5 seconds elapse with no result
- **THEN** a Cancel button appears, and cancelling surfaces a distinct "cancelled" message rather than a generic error

### Generation failures are caught centrally and are always retryable, never fatal
Any exception thrown by `GenerateAsync` — including business-rule exceptions such as `GLBalanceException` — MUST be caught by the viewer, logged, and replaced with a generic user-facing error plus a "Try Again" action. A report-generation failure must never crash the page.

#### Scenario: a provider throws because the ledger is out of balance
- **WHEN** `TrialBalanceReportProvider` detects debits ≠ credits and throws `GLBalanceException`
- **THEN** the viewer shows a friendly error and a retry button instead of propagating the exception

### Master-detail (collapsed per-section) rendering is a viewer-only concession — PDF and CSV always render full detail
When a provider populates `ReportData.SummaryColumns` (and each section's `SummaryRow`), the on-screen viewer collapses sections into one summary row per section, expandable via `RadzenDataGrid`. `PdfReportRenderer` and `ICsvReportExporter` ignore `SummaryColumns`/`SummaryRow` entirely and always render every section's full row detail, because printed/exported output must remain a complete record.

#### Scenario: a master-detail report (e.g. Committee Report, Member Account Summary) is printed or exported
- **WHEN** the user clicks Print/PDF or Export CSV
- **THEN** the output contains every underlying row, not the collapsed summary shown on screen

### Report bodies hand-roll table markup with fixed 15-row paging because report shape is dynamic per report
Since each report defines its own columns, section headings, and subtotal/grand-total rows at generation time, `ReportViewer` cannot use `RadzenDataGrid`'s typed-column model for row-level content — it renders section bodies as hand-authored HTML tables with manual pagination fixed at 15 rows per page, matching the page size used elsewhere in the app. Only the outer one-row-per-section list of a master-detail report has a fixed column shape, so that list does use `RadzenDataGrid` (also at page size 15) while its expanded detail rows still hand-roll.

#### Scenario: a flat report (no SummaryColumns) has more rows than fit on one page
- **WHEN** the report has more data rows than the fixed page size
- **THEN** the viewer paginates manually in fixed-size pages, keeping section headings and subtotals attached to the correct page

#### Scenario: a master-detail report is viewed
- **WHEN** the report declares `SummaryColumns`
- **THEN** the section list itself renders through `RadzenDataGrid` with paging, while each expanded section's detail rows still render as a hand-rolled table

### GL-derived reports aggregate by account identity, never by the denormalized transaction snapshot
Reports that total or group ledger activity MUST key off `Transaction.AccountId` (the live foreign key), never off the denormalized `Transaction.GLAccount` string, which is a point-in-time label snapshot that can go stale if an account is later renamed.

#### Scenario: an account is renamed after transactions were posted against it
- **WHEN** a GL-based report (Trial Balance, General Ledger, Account Register, Balance Sheet, BAS Summary) aggregates historical activity
- **THEN** totals are computed by matching `AccountId`, so the report reflects the account's current name/type rather than whatever label was recorded at posting time

### Historical financial reports include archived accounts and members so past activity still resolves correctly
Because financial and membership records are never hard-deleted, reports covering a date range or balance calculation MUST include archived (soft-deleted) accounts/members alongside active ones, so that transactions predating an archive action still show a correct name and count toward totals. Member- and committee-listing reports additionally let the user opt in to seeing archived-only or all-status records via a filter.

#### Scenario: an account or member is archived after having historical activity
- **WHEN** a report covering a period before the archive date is generated
- **THEN** the archived account/member still appears with its correct name and contributes to totals, rather than being silently dropped

#### Scenario: a user wants to see only archived members
- **WHEN** they select "Archived Only" (or "All") on a report's status filter
- **THEN** the report includes archived records accordingly

### Print and CSV export hand off to the operating system's default application rather than rendering in-app
`PrintReport` and `ExportCsv` MUST write the rendered bytes to a temp file and launch it via the OS shell (`Process.Start` with `UseShellExecute = true`) rather than implementing an in-app PDF/CSV viewer, consistent with this being a desktop MAUI shell rather than a browser.

#### Scenario: a user clicks "Print / PDF"
- **WHEN** PDF bytes are generated successfully
- **THEN** a temp `.pdf` file is written and opened in the user's default PDF viewer
- **AND** a failure to render or launch shows an in-app error instead of a partial or corrupt hand-off

## Uncovered
_None — every file in the area was read._
