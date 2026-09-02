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
    /// <remarks>
    /// These are culture-invariant tokens used for filtering/comparison/persistence (FR-024) — never
    /// localise them. The user-facing option text is <see cref="OptionLabels"/>.
    /// </remarks>
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();

    /// <summary>
    /// For Select filters: the display label for each entry in <see cref="Options"/>, positionally
    /// aligned. When empty (or shorter than <see cref="Options"/>) the renderer falls back to the
    /// option value itself. Providers populate this from <c>EnumsResource</c> / <c>ReportsResource</c>
    /// so the label localises while the option value stays invariant (spec 027, FR-024).
    /// </summary>
    public IReadOnlyList<string> OptionLabels { get; init; } = Array.Empty<string>();

    /// <summary>Pre-selected value when the report filter panel first opens. Null = no default.</summary>
    public string? DefaultValue { get; init; }
}
