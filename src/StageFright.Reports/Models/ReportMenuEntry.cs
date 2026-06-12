namespace StageFright.Reports.Models;

/// <summary>A single report entry within a ReportMenuSection.</summary>
public class ReportMenuEntry
{
    /// <summary>Unique report identifier (matches IReportProvider.ReportId).</summary>
    public string ReportId { get; init; } = string.Empty;

    /// <summary>Human-readable report name shown in the menu.</summary>
    public string ReportName { get; init; } = string.Empty;
}
