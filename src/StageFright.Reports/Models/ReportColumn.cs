using StageFright.Core.Enums;

namespace StageFright.Reports.Models;

/// <summary>Defines a column in a report table.</summary>
public class ReportColumn
{
    /// <summary>Column header text.</summary>
    public string Header { get; init; } = string.Empty;

    /// <summary>Horizontal alignment for cell values in this column.</summary>
    public ReportColumnAlignment Alignment { get; init; } = ReportColumnAlignment.Left;
}
