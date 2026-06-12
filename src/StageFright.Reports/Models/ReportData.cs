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
}
