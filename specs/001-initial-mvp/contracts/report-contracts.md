# Contracts: Reports Infrastructure

**Assemblies**: `IReportProvider` lives in `StageFright.Plugins.Contracts`; report data models and renderers in `StageFright.Reports`. Modules generate data; the shared infrastructure owns ALL display, PDF printing, and CSV export (FR-046–FR-048). Modules MUST NOT implement their own print/export logic.

## IReportProvider (FR-046)

```csharp
public interface IReportProvider
{
    string ReportId { get; }        // Unique across all modules; duplicates skipped + logged
    string ReportName { get; }      // Menu label, e.g. "Income Statement"
    string ModuleName { get; }      // Menu section, e.g. "Members", "Finance", plugin name
    int DisplayOrder { get; }       // Within module section
    IReadOnlyList<ReportFilterDefinition> Filters { get; }   // e.g. date range, status, category, member
    Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct);
}
```

## Report data structures

```csharp
public class ReportData
{
    public string Title { get; init; }
    public string? SubTitle { get; init; }            // e.g. "1 Jan 2026 – 31 Dec 2026"
    public DateTime GeneratedAt { get; init; }
    public IReadOnlyList<ReportColumn> Columns { get; init; }
    public IReadOnlyList<ReportSection> Sections { get; init; }
    public ReportRow? GrandTotal { get; init; }
}

public class ReportColumn
{
    public string Header { get; init; }
    public ReportColumnAlignment Alignment { get; init; }   // Left | Right | Center
    public ReportColumnFormat Format { get; init; }         // Text | Currency | Date | Percent | Number
}

public class ReportSection                                  // e.g. "Income", "Expenses", "Assets", per-year, per-member
{
    public string? Heading { get; init; }
    public IReadOnlyList<ReportRow> Rows { get; init; }
    public ReportRow? Subtotal { get; init; }
}

public class ReportRow
{
    public IReadOnlyList<string> Cells { get; init; }       // Pre-formatted values (module owns formatting)
    public bool IsEmphasized { get; init; }                 // Bold (totals, current-year rows)
}
```

```csharp
public class ReportFilterDefinition
{
    public string Key { get; init; }                 // "dateRange", "memberStatus", "category", "member"
    public ReportFilterType Type { get; init; }      // DateRange | SingleSelect | MultiSelect
    public string Label { get; init; }
    public IReadOnlyList<(string Value, string Label)> Options { get; init; }
    public string DefaultValue { get; init; }        // e.g. current calendar year; "Active"
}

public class ReportFilterValues : Dictionary<string, string> { }
```

## Filter behavior (FR-033–FR-036, FR-051, FR-052)

- Date-range filters default to current calendar year (Jan 1 – Dec 31).
- Member List status filter: Active (default) | Inactive | Archived | All.
- Committee Report filter: Active Only (default) | Archived Only | All.
- Filter state persists within a viewing session (print/export use the same values); resets to defaults when the report is closed and reopened.

## ReportProviderRegistry

```csharp
public interface IReportProviderRegistry
{
    IReadOnlyList<ReportMenuSection> GetMenuSections();   // Members, Finance, then plugins alphabetically (FR-045)
    IReportProvider? GetProvider(string reportId);
}
```

Registration errors and `GenerateAsync` failures: structured log → skip/show user-friendly error in viewer → other reports unaffected (FR-049).

## Common report viewer contract (FR-047)

`ReportViewer.razor` (in `StageFright.UI/Shared`) behavior:

1. Synchronous generation: modal "Generating report..." with spinner shown for the entire duration (always, not conditionally — NFR-019); a **Cancel** option appears if generation exceeds 5 s, returning to the Reports menu (FR-047).
2. No caching: select, Print, and Export to CSV each trigger fresh `GenerateAsync`.
3. Print: `IPdfReportRenderer.Render(ReportData)` (QuestPDF) → temp PDF → OS print dialog (research.md R3). Output includes title, date range, generation date, headers, aligned rows, subtotals, grand totals (FR-037).
4. Export: `ICsvReportExporter.Export(ReportData)` (CsvHelper) → save-file dialog; headers as first row, RFC 4180 escaping (FR-041).
5. Generation failure → user-friendly error with recovery options in the viewer (FR-049).

```csharp
public interface IPdfReportRenderer { byte[] Render(ReportData data); }
public interface ICsvReportExporter { string Export(ReportData data); }
```

## MVP report providers (FR-050)

| Module | Report | Notes |
|--------|--------|-------|
| Members | Member List | Name, Address, Phone, Email, Join Date, Age (if DOB), Status; status filter (FR-051) |
| Members | Committee Report | Member, Year, Position; by year desc; status filter (FR-052) |
| Finance | Income Statement | Income section + subtotal, Expense section + subtotal, net income/loss (FR-033) |
| Finance | Trial Balance | Assets/Income/Expenses sections, Debit/Credit columns; Σdebits = Σcredits within 0.01 or generation fails with the exact FR-034 error message |
| Finance | Account Register | Chronological transactions with running balance (FR-035) |
| Finance | Member Account Summary | Opening balance, period transactions, closing balance, aging current/30/60/90+ by DueDate; includes archived members (FR-036) |
