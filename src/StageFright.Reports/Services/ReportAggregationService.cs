using Microsoft.Extensions.Logging;
using StageFright.Plugins.Contracts;
using System.Collections.Generic;
using System.Linq;

namespace StageFright.Reports.Services;

/// <summary>
/// Discovers and aggregates all registered report providers.
/// Uses the plugin discovery system to find IReportProvider implementations.
/// </summary>
public class ReportAggregationService
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<ReportAggregationService> _logger;
	private List<IReportProvider>? _cachedReports;

	/// <summary>
	/// Initializes a new instance of the ReportAggregationService.
	/// </summary>
	/// <param name="serviceProvider">The service provider for resolving report providers</param>
	/// <param name="logger">The logger for diagnostic output</param>
	public ReportAggregationService(IServiceProvider serviceProvider, ILogger<ReportAggregationService> logger)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Gets all available report providers.
	/// </summary>
	/// <returns>A list of all registered IReportProvider implementations</returns>
	public IEnumerable<IReportProvider> GetAllReports()
	{
		if (_cachedReports != null)
			return _cachedReports;

		try
		{
			var reports = _serviceProvider.GetServices<IReportProvider>().ToList();
			_logger.LogInformation("Discovered {ReportCount} report providers", reports.Count);

			foreach (var report in reports)
			{
				_logger.LogDebug("Report provider: {ModuleName}/{ReportId} - {ReportName}",
					report.ModuleName, report.ReportId, report.ReportName);
			}

			_cachedReports = reports;
			return reports;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error discovering report providers");
			throw;
		}
	}

	/// <summary>
	/// Gets reports organized by module name.
	/// </summary>
	/// <returns>A dictionary mapping module names to lists of reports</returns>
	public Dictionary<string, List<IReportProvider>> GetReportsByModule()
	{
		var allReports = GetAllReports();
		return allReports
			.GroupBy(r => r.ModuleName)
			.ToDictionary(
				g => g.Key,
				g => g.OrderBy(r => r.DisplayOrder).ToList()
			);
	}

	/// <summary>
	/// Gets a specific report provider by module and report ID.
	/// </summary>
	/// <param name="moduleName">The module name</param>
	/// <param name="reportId">The report ID</param>
	/// <returns>The report provider, or null if not found</returns>
	public IReportProvider? GetReport(string moduleName, string reportId)
	{
		var allReports = GetAllReports();
		return allReports.FirstOrDefault(r => r.ModuleName == moduleName && r.ReportId == reportId);
	}

	/// <summary>
	/// Clears the cached report list, forcing a fresh discovery on the next call.
	/// Useful for testing or plugin reload scenarios.
	/// </summary>
	public void ClearCache()
	{
		_cachedReports = null;
	}
}
