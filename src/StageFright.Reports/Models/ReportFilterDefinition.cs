using StageFright.Core.Enums;

namespace StageFright.Reports.Models;

/// <summary>Describes a single filter parameter accepted by a report provider.</summary>
public class ReportFilterDefinition
{
    /// <summary>Unique filter key used in ReportFilterValues (e.g., "dateFrom", "memberStatus").</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>UI control type to render for this filter.</summary>
    public ReportFilterType Type { get; init; }

    /// <summary>Human-readable label shown next to the filter control.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>For Select filters: available option values. Empty for other types.</summary>
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();

    /// <summary>Pre-selected value when the report filter panel first opens. Null = no default.</summary>
    public string? DefaultValue { get; init; }
}
