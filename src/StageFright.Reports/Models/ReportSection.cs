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

    /// <summary>
    /// Optional collapsed one-line representation of this section for master-detail rendering.
    /// Null ⇒ section always renders in full. Non-null ⇒ section is a master row, expandable to
    /// reveal <see cref="Heading"/>/<see cref="Rows"/>/<see cref="Subtotal"/>. Cell count must match
    /// the parent <see cref="ReportData.SummaryColumns"/> count.
    /// </summary>
    public ReportRow? SummaryRow { get; init; }
}
