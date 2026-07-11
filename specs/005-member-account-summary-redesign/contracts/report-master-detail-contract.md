# Contract: Report master-detail extension

This documents the internal contract between a `StageFright.Reports` `IReportProvider` implementation and `StageFright.UI`'s `ReportViewer` component, for the new optional master-detail capability. No public/external API is exposed by this application (desktop MAUI Blazor app, no HTTP surface), so this is the closest analog: the shared model contract that any current or future report provider must honor.

## Opting in to master-detail rendering

A report provider opts in by populating, on the `ReportData` it returns from `GenerateAsync`:

```csharp
new ReportData
{
    // ...existing Title/SubTitle/GeneratedAt/Columns/GrandTotal as today...
    SummaryColumns = [ /* master-view column headers, e.g. */
        new ReportColumn { Header = "Member" },
        new ReportColumn { Header = "Current" },
        new ReportColumn { Header = "30 Days" },
        new ReportColumn { Header = "60 Days" },
        new ReportColumn { Header = "90+ Days" },
        new ReportColumn { Header = "Balance", Alignment = ReportColumnAlignment.Right, Format = ReportColumnFormat.Currency }
    ],
    Sections = sections // each section below MUST set SummaryRow
};

new ReportSection
{
    Heading = "Amanda Scott",           // unchanged — still used inside the expanded detail
    SummaryRow = new ReportRow          // NEW — collapsed master-row representation
    {
        Cells = ["Amanda Scott", "Current: 0.00", "30 days: 0.00", "60 days: 0.00", "90+ days: 0.00", "5.00"]
    },
    Rows = detailRows,                  // unchanged — full detail, used both for expand panel and exports
    Subtotal = null
}
```

## Contract rules

1. **All-or-nothing per report**: if `ReportData.SummaryColumns` is non-empty, every `ReportSection` in `Sections` MUST set `SummaryRow`. `ReportViewer` does not support a mix of master-detail and flat sections within one report.
2. **Cell-count parity**: `SummaryRow.Cells.Count` MUST equal `SummaryColumns.Count`, exactly as `Rows[i].Cells.Count` MUST equal `Columns.Count` today.
3. **Backward compatibility**: a report that never sets `SummaryColumns`/`SummaryRow` (all five other existing reports) renders through the unchanged flat-table path in `ReportViewer` — this contract is purely additive.
4. **Exports are unaffected**: `SummaryColumns`/`SummaryRow` are consumed only by `ReportViewer`'s on-screen rendering. `PdfReportRenderer` and `CsvReportExporter` read `Columns`/`Sections[].Rows`/`Subtotal`/`GrandTotal` exactly as before and MUST NOT be changed to reference the new fields.
5. **Ordering**: `ReportViewer` renders master rows (sections) in the order `Sections` is returned in — a provider that wants alphabetical-by-name order (as this feature does) must sort `Sections` itself before returning `ReportData`; `ReportViewer` does not re-sort.

## `ReportViewer` rendering contract

- When `ReportData.SummaryColumns` is non-empty: render a `RadzenDataGrid<ReportSection>` (`AllowPaging="true" PageSize="15" class="rz-shadow-0"`, `AllowSorting="false"`) with one dynamically-generated `RadzenDataGridColumn` per `SummaryColumns` entry (`Template` indexing into `section.SummaryRow!.Cells[i]`), and a `<Template Context="section">` master-detail block that renders the existing flat table (heading suppressed since the master row already shows the name, `Rows`, `Subtotal`) for that one section.
- Otherwise: render the existing hand-rolled flat Bootstrap table exactly as today, unchanged.
- Print/PDF (`PrintReport`) and CSV export (`ExportCsv`) button behavior and wiring are unchanged in both cases — they always operate on the full `_report` object regardless of which rendering path was used on screen.
