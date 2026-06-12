using StageFright.Reports.Models;

namespace StageFright.Reports.Registry;

/// <summary>
/// Registry of all registered IReportProvider implementations.
/// Members section appears before Finance; plugins appear alphabetically after Finance.
/// </summary>
public interface IReportProviderRegistry
{
    /// <summary>Returns all report menu sections ordered: Members, Finance, then plugins alphabetically.</summary>
    IReadOnlyList<ReportMenuSection> GetMenuSections();

    /// <summary>Returns the report provider with the given ReportId, or null if not registered.</summary>
    IReportProvider? GetProvider(string reportId);
}
