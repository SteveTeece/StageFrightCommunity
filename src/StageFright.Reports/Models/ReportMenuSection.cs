namespace StageFright.Reports.Models;

/// <summary>A grouping of reports in the Reports menu, identified by module name.</summary>
public class ReportMenuSection
{
    /// <summary>Module name (e.g., "Finance", "Members") used as the section heading.</summary>
    public string ModuleName { get; init; } = string.Empty;

    /// <summary>Reports available in this section, ordered by DisplayOrder.</summary>
    public IReadOnlyList<ReportMenuEntry> Reports { get; init; } = Array.Empty<ReportMenuEntry>();
}
