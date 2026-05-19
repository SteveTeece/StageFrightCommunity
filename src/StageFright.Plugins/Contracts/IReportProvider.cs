namespace StageFright.Plugins.Contracts;

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

/// <summary>Filter criteria for report generation.</summary>
public class ReportFilter
{
	public DateTime? DateFrom { get; set; }
	public DateTime? DateTo { get; set; }
	public string? CategoryFilter { get; set; }
	public string? MemberStatusFilter { get; set; }
	public Dictionary<string, object> CustomFilters { get; set; } = new();
}

/// <summary>Report data structure.</summary>
public class ReportData
{
	public string ReportTitle { get; set; } = string.Empty;
	public string[] ColumnHeaders { get; set; } = Array.Empty<string>();
	public string[][] Rows { get; set; } = Array.Empty<string[]>();
	public Dictionary<string, string>? Summaries { get; set; }
	public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
