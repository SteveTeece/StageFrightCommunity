using Microsoft.Extensions.Logging;
using StageFright.Plugins.Contracts;
using System.Collections.Generic;
using System.Linq;

namespace StageFright.Reports.Services;

/// <summary>
/// Organizes reports by module for display in the Reports menu.
/// Provides hierarchical menu structure for report navigation.
/// </summary>
public class ReportMenuService
{
	private readonly ReportAggregationService _reportAggregationService;
	private readonly ILogger<ReportMenuService> _logger;

	/// <summary>
	/// Represents a menu item for a single report.
	/// </summary>
	public class ReportMenuItem
	{
		/// <summary>The module name this report belongs to.</summary>
		public string ModuleName { get; set; } = string.Empty;

		/// <summary>The unique report ID within the module.</summary>
		public string ReportId { get; set; } = string.Empty;

		/// <summary>Display name for the report.</summary>
		public string DisplayName { get; set; } = string.Empty;

		/// <summary>Display order for menu positioning.</summary>
		public int DisplayOrder { get; set; }
	}

	/// <summary>
	/// Represents a module section in the reports menu.
	/// </summary>
	public class ReportModuleSection
	{
		/// <summary>The module name.</summary>
		public string ModuleName { get; set; } = string.Empty;

		/// <summary>Reports in this module, ordered by DisplayOrder.</summary>
		public List<ReportMenuItem> Reports { get; set; } = new();

		/// <summary>Display order for module positioning.</summary>
		public int DisplayOrder { get; set; }
	}

	/// <summary>
	/// Initializes a new instance of the ReportMenuService.
	/// </summary>
	/// <param name="reportAggregationService">The aggregation service for discovering reports</param>
	/// <param name="logger">The logger for diagnostic output</param>
	public ReportMenuService(
		ReportAggregationService reportAggregationService,
		ILogger<ReportMenuService> logger)
	{
		_reportAggregationService = reportAggregationService ?? throw new ArgumentNullException(nameof(reportAggregationService));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Gets the complete menu structure organized by module.
	/// </summary>
	/// <returns>A list of module sections, each containing organized reports</returns>
	public List<ReportModuleSection> GetMenuStructure()
	{
		try
		{
			var reportsByModule = _reportAggregationService.GetReportsByModule();
			var modules = new List<ReportModuleSection>();

			// Module display order defaults
			var moduleOrder = new Dictionary<string, int>
			{
				{ "Members", 1 },
				{ "Finance", 2 },
				{ "Rehearsals", 3 },
				{ "Events", 4 }
			};

			foreach (var moduleName in reportsByModule.Keys.OrderBy(m => moduleOrder.ContainsKey(m) ? moduleOrder[m] : int.MaxValue))
			{
				var section = new ReportModuleSection
				{
					ModuleName = moduleName,
					DisplayOrder = moduleOrder.ContainsKey(moduleName) ? moduleOrder[moduleName] : int.MaxValue,
					Reports = reportsByModule[moduleName]
						.Select((report, _) => new ReportMenuItem
						{
							ModuleName = report.ModuleName,
							ReportId = report.ReportId,
							DisplayName = report.ReportName,
							DisplayOrder = report.DisplayOrder
						})
						.OrderBy(item => item.DisplayOrder)
						.ToList()
				};

				modules.Add(section);
			}

			_logger.LogInformation("Generated menu structure with {ModuleCount} modules", modules.Count);
			return modules.OrderBy(m => m.DisplayOrder).ToList();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error generating menu structure");
			throw;
		}
	}

	/// <summary>
	/// Gets all reports for a specific module.
	/// </summary>
	/// <param name="moduleName">The module name</param>
	/// <returns>A list of report menu items for the module</returns>
	public List<ReportMenuItem> GetModuleReports(string moduleName)
	{
		try
		{
			var allReports = _reportAggregationService.GetAllReports()
				.Where(r => r.ModuleName == moduleName)
				.OrderBy(r => r.DisplayOrder)
				.ToList();

			return allReports
				.Select(r => new ReportMenuItem
				{
					ModuleName = r.ModuleName,
					ReportId = r.ReportId,
					DisplayName = r.ReportName,
					DisplayOrder = r.DisplayOrder
				})
				.ToList();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting reports for module {ModuleName}", moduleName);
			throw;
		}
	}

	/// <summary>
	/// Gets a single report menu item by module and report ID.
	/// </summary>
	/// <param name="moduleName">The module name</param>
	/// <param name="reportId">The report ID</param>
	/// <returns>The report menu item, or null if not found</returns>
	public ReportMenuItem? GetMenuItemForReport(string moduleName, string reportId)
	{
		var report = _reportAggregationService.GetReport(moduleName, reportId);
		if (report == null)
			return null;

		return new ReportMenuItem
		{
			ModuleName = report.ModuleName,
			ReportId = report.ReportId,
			DisplayName = report.ReportName,
			DisplayOrder = report.DisplayOrder
		};
	}
}
