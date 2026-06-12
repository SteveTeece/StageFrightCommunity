using StageFright.Reports.Models;

namespace StageFright.Reports.Registry;

/// <summary>
/// Implemented by modules and plugins to contribute reports to the Reports menu.
/// Failures in GenerateAsync are caught by the registry and return a user-friendly error.
/// </summary>
public interface IReportProvider
{
    /// <summary>Unique report identifier (e.g., "income-statement"). Duplicates are skipped and logged.</summary>
    string ReportId { get; }

    /// <summary>Human-readable report name shown in the Reports menu.</summary>
    string ReportName { get; }

    /// <summary>Module name used to group reports in the menu (e.g., "Finance", "Members").</summary>
    string ModuleName { get; }

    /// <summary>Sort order within the module's reports section.</summary>
    int DisplayOrder { get; }

    /// <summary>Filter parameters accepted by this report. Empty list = no filters.</summary>
    IReadOnlyList<ReportFilterDefinition> Filters { get; }

    /// <summary>Generates the report data asynchronously. Exceptions are caught by the caller.</summary>
    Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default);
}
