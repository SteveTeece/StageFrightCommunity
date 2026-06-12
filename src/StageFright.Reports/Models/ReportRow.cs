namespace StageFright.Reports.Models;

/// <summary>A single data row in a report section.</summary>
public class ReportRow
{
    /// <summary>Cell values in column order. Count must match the parent ReportData.Columns count.</summary>
    public IReadOnlyList<string> Cells { get; init; } = Array.Empty<string>();

    /// <summary>When true the renderer applies emphasis formatting (e.g., bold for totals or key rows).</summary>
    public bool IsEmphasized { get; init; }
}
