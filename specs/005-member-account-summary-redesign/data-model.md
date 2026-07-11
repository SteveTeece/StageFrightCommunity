# Phase 1 Data Model: Member Account Summary Report Redesign

This feature touches no database entities, EF Core migrations, or GL/aging calculations (FR-009). The "model" here is the in-memory reporting model shared by `StageFright.Reports` and `StageFright.UI`. All changes are additive and optional.

## `ReportData` (existing — `src/StageFright.Reports/Models/ReportData.cs`)

| Field | Type | Change | Notes |
|---|---|---|---|
| `Title`, `SubTitle`, `GeneratedAt`, `Columns`, `Sections`, `GrandTotal` | (existing) | Unchanged | |
| `SummaryColumns` | `IReadOnlyList<ReportColumn>?` | **New** | Column headers for the collapsed master view (e.g. Member / Current / 30 Days / 60 Days / 90+ Days / Balance). `null`/empty ⇒ report has no master-detail view; `ReportViewer` renders the existing flat table exactly as today. Only `MemberAccountSummaryReportProvider` populates this initially. |

## `ReportSection` (existing — `src/StageFright.Reports/Models/ReportSection.cs`)

| Field | Type | Change | Notes |
|---|---|---|---|
| `Heading`, `Rows`, `Subtotal` | (existing) | Unchanged | `Rows`/`Subtotal` remain the full detail content, used for both the expanded on-screen panel and for PDF/CSV export. |
| `SummaryRow` | `ReportRow?` | **New** | The collapsed one-line representation of this section (one member). `null` ⇒ section always renders in full (today's behavior for the other five reports). Non-null ⇒ section is a master row in the `RadzenDataGrid`, expandable to reveal `Heading`/`Rows`/`Subtotal` via Radzen's row-expand template. |

**Validation rule**: `ReportData.SummaryColumns` and `ReportSection.SummaryRow` are used together — if `SummaryColumns` is non-empty, every section in `Sections` must supply a `SummaryRow` (enforced by construction in `MemberAccountSummaryReportProvider`, not by a runtime guard, since `ReportData` is an internal DTO built entirely by trusted provider code, not external input). `SummaryRow.Cells.Count` must equal `SummaryColumns.Count`, mirroring the existing `Rows`/`Columns` count convention.

## `MemberAccountSummaryReportProvider` (existing — `src/StageFright.Reports/Providers/MemberAccountSummaryReportProvider.cs`)

New filter (`Filters` property):

| Key | Type | Label | Default |
|---|---|---|---|
| `includeArchived` | `ReportFilterType.Boolean` | "Show Archived Members" | `"false"` |

Per-member generation change (`GenerateAsync`):
- Member fetch: only active members by default; archived members appended only when `filters.Get("includeArchived") == "true"` (mirrors the existing `dateFrom`/`dateTo` pattern already in this provider).
- `SummaryRow.Cells`: `[Name (with "(Archived)" suffix if applicable), "Current: {aging0}", "30 days: {aging30}", "60 days: {aging60}", "90+ days: {aging90Plus}", FormatCurrency(closingBalance)]` — reuses the exact aging computation already in the method.
- `Rows` (detail): unchanged content and order — Opening Balance, transactions (`OrderBy(t => t.Date)`, already oldest-first), Closing Balance, Aging summary row.
- `ReportData.SummaryColumns`: `[Member, Current, "30 Days", "60 Days", "90+ Days", Balance]`.

No changes to `IGLRepository`, `IMemberRepository`, `IFeeRepository`, or any entity/repository contract.

## `ReportViewer` (existing — `src/StageFright.UI/Shared/ReportViewer.razor` / `.razor.cs`)

No new persisted or long-lived component state is needed: Radzen's `RadzenDataGrid` master-detail `<Template>` manages expand/collapse internally per row. `ReportViewer.razor.cs` only needs a `bool UseMasterDetail => _report?.SummaryColumns?.Count > 0;` computed property to pick the rendering path (RadzenDataGrid master-detail vs. today's flat table).

No changes to `PdfReportRenderer` or `CsvReportExporter` — both continue reading `ReportData.Columns`/`Sections[].Rows`/`Subtotal`/`GrandTotal` exactly as today, oblivious to `SummaryColumns`/`SummaryRow`.
