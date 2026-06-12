namespace StageFright.Reports.Models;

/// <summary>A logical grouping of rows within a report, optionally with a heading and subtotal row.</summary>
public class ReportSection
{
    /// <summary>Optional section heading (e.g., "Income", "Expenses"). Null = no heading rendered.</summary>
    public string? Heading { get; init; }

    /// <summary>Data rows in this section.</summary>
    public IReadOnlyList<ReportRow> Rows { get; init; } = Array.Empty<ReportRow>();

    /// <summary>Optional subtotal row appended after the last data row. Null = no subtotal.</summary>
    public ReportRow? Subtotal { get; init; }
}
