namespace StageFright.Reports.Models;

/// <summary>Complete, renderer-agnostic report data. Used by both PDF and CSV renderers.</summary>
public class ReportData
{
    /// <summary>Report title (e.g., "Income Statement").</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Optional sub-title or date-range description (e.g., "1 January 2026 – 31 December 2026").</summary>
    public string? SubTitle { get; init; }

    /// <summary>UTC timestamp when the report was generated.</summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Ordered column definitions shared by all sections.</summary>
    public IReadOnlyList<ReportColumn> Columns { get; init; } = Array.Empty<ReportColumn>();

    /// <summary>Report body sections rendered top-to-bottom.</summary>
    public IReadOnlyList<ReportSection> Sections { get; init; } = Array.Empty<ReportSection>();

    /// <summary>Optional grand-total row rendered after all sections.</summary>
    public ReportRow? GrandTotal { get; init; }

    /// <summary>
    /// Optional basis-of-accounting disclosure shown on financial statements (FR-012). Null for
    /// non-financial reports (Member List, Committee). When set, it is rendered by
    /// <c>PdfReportRenderer</c> (a line below the "Generated: …" line), <c>CsvReportExporter</c>
    /// (a trailing note record after the grand total), and <c>ReportViewer.razor</c> (below the
    /// subtitle). Set by the financial-statement providers from the shared
    /// <c>Reports_Common_BasisOfAccounting</c> string, which describes the hybrid basis
    /// accurately — member fees on accrual, all other activity on cash.
    /// </summary>
    public string? BasisOfAccounting { get; init; }

    /// <summary>
    /// Optional column headers for a collapsed master view (one row per section).
    /// Null/empty ⇒ report has no master-detail view; <c>ReportViewer</c> renders the flat table.
    /// When non-empty, every section in <see cref="Sections"/> must set <see cref="ReportSection.SummaryRow"/>.
    /// </summary>
    public IReadOnlyList<ReportColumn>? SummaryColumns { get; init; }
}
