namespace StageFright.Plugins.Contracts;

using System;
using System.Threading.Tasks;

/// <summary>
/// Contract for report providers.
/// Plugins implement this to contribute custom reports.
/// </summary>
public interface IReportProvider
{
	/// <summary>Module name this report belongs to (e.g., "Finance", "Members").</summary>
	string ModuleName { get; }

	/// <summary>Unique identifier for this report within its module.</summary>
	string ReportId { get; }

	/// <summary>Display name for the report.</summary>
	string ReportName { get; }

	/// <summary>Display order for positioning in menu (lower = earlier).</summary>
	int DisplayOrder { get; }

	/// <summary>Generates report data asynchronously.</summary>
	Task<ReportData> GenerateAsync(ReportFilter? filter = null);
}

