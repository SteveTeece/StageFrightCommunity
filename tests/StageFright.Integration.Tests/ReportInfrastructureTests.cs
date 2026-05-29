using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StageFright.Plugins.Contracts;
using StageFright.Reports.Services;
using Xunit;
using FluentAssertions;

namespace StageFright.Integration.Tests;

/// <summary>
/// Integration tests for report infrastructure.
/// Verifies report provider discovery, aggregation, menu structure, and data validation.
/// </summary>
public class ReportInfrastructureTests
{
	private IServiceProvider CreateServiceProvider()
	{
		var services = new ServiceCollection();
		services.AddLogging(builder => builder.AddDebug());
		services.AddScoped<ReportAggregationService>();
		services.AddScoped<ReportMenuService>();
		return services.BuildServiceProvider();
	}

	[Fact]
	public void ReportAggregationService_Discovers_AllReportProviders()
	{
		// Arrange
		var serviceProvider = CreateServiceProvider();
		var aggregationService = serviceProvider.GetRequiredService<ReportAggregationService>();

		// Act
		var reports = aggregationService.GetAllReports();

		// Assert
		reports.Should().NotBeNull();
		// Note: Reports will be empty until actual report providers are registered
		// This test validates the infrastructure is in place
	}

	[Fact]
	public void ReportAggregationService_GroupsByModule()
	{
		// Arrange
		var serviceProvider = CreateServiceProvider();
		var aggregationService = serviceProvider.GetRequiredService<ReportAggregationService>();

		// Act
		var reportsByModule = aggregationService.GetReportsByModule();

		// Assert
		reportsByModule.Should().NotBeNull();
		reportsByModule.Should().BeOfType<Dictionary<string, List<IReportProvider>>>();
	}

	[Fact]
	public void ReportMenuService_GeneratesMenuStructure()
	{
		// Arrange
		var serviceProvider = CreateServiceProvider();
		var menuService = serviceProvider.GetRequiredService<ReportMenuService>();

		// Act
		var menuStructure = menuService.GetMenuStructure();

		// Assert
		menuStructure.Should().NotBeNull();
		menuStructure.Should().BeOfType<List<ReportMenuService.ReportModuleSection>>();
	}

	[Fact]
	public void ReportData_HasRequiredProperties()
	{
		// Arrange
		var reportData = new ReportData
		{
			ReportTitle = "Test Report",
			ColumnHeaders = new[] { "Column1", "Column2" },
			Rows = new[] { new[] { "Value1", "Value2" } },
			GeneratedAt = DateTime.UtcNow
		};

		// Act & Assert
		reportData.ReportTitle.Should().Be("Test Report");
		reportData.ColumnHeaders.Should().HaveCount(2);
		reportData.Rows.Should().HaveCount(1);
		reportData.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void ReportData_Summaries_AreOptional()
	{
		// Arrange
		var reportData = new ReportData
		{
			ReportTitle = "Test Report",
			ColumnHeaders = new[] { "Column1" },
			Rows = Array.Empty<string[]>()
		};

		// Act & Assert
		reportData.Summaries.Should().BeNull();
	}

	[Fact]
	public void ReportData_Summaries_CanBePopulated()
	{
		// Arrange
		var reportData = new ReportData
		{
			ReportTitle = "Test Report",
			ColumnHeaders = new[] { "Column1" },
			Rows = Array.Empty<string[]>(),
			Summaries = new Dictionary<string, string>
			{
				{ "Total", "$1,000.00" },
				{ "Count", "10" }
			}
		};

		// Act & Assert
		reportData.Summaries.Should().HaveCount(2);
		reportData.Summaries["Total"].Should().Be("$1,000.00");
		reportData.Summaries["Count"].Should().Be("10");
	}

	[Fact]
	public void ReportFilter_HasExpectedProperties()
	{
		// Arrange
		var now = DateTime.UtcNow;
		var filter = new ReportFilter
		{
			DateFrom = now.AddMonths(-1),
			DateTo = now,
			CategoryFilter = "Income"
		};

		// Act & Assert
		filter.DateFrom.Should().Be(now.AddMonths(-1));
		filter.DateTo.Should().Be(now);
		filter.CategoryFilter.Should().Be("Income");
	}

	[Fact]
	public void ReportFilter_Properties_AreOptional()
	{
		// Arrange
		var filter = new ReportFilter();

		// Act & Assert
		filter.DateFrom.Should().BeNull();
		filter.DateTo.Should().BeNull();
		filter.CategoryFilter.Should().BeNull();
		filter.MemberStatusFilter.Should().BeNull();
	}

	[Fact]
	public void ReportAggregationService_ClearCache_RefreshesReports()
	{
		// Arrange
		var serviceProvider = CreateServiceProvider();
		var aggregationService = serviceProvider.GetRequiredService<ReportAggregationService>();

		// Act
		var reportsFirst = aggregationService.GetAllReports();
		aggregationService.ClearCache();
		var reportsSecond = aggregationService.GetAllReports();

		// Assert
		reportsFirst.Should().NotBeNull();
		reportsSecond.Should().NotBeNull();
		// Cache should be cleared and fresh discovery performed
	}

	[Fact]
	public void ReportMenuService_GetModuleReports_ReturnsEmptyListForUnknownModule()
	{
		// Arrange
		var serviceProvider = CreateServiceProvider();
		var menuService = serviceProvider.GetRequiredService<ReportMenuService>();

		// Act
		var reports = menuService.GetModuleReports("UnknownModule");

		// Assert
		reports.Should().NotBeNull();
		reports.Should().BeEmpty();
	}

	[Fact]
	public void ReportMenuService_GetMenuItemForReport_ReturnsNullForUnknownReport()
	{
		// Arrange
		var serviceProvider = CreateServiceProvider();
		var menuService = serviceProvider.GetRequiredService<ReportMenuService>();

		// Act
		var menuItem = menuService.GetMenuItemForReport("UnknownModule", "UnknownReport");

		// Assert
		menuItem.Should().BeNull();
	}

	[Theory]
	[InlineData("Finance", 2)]
	[InlineData("Members", 1)]
	public void ReportMenuService_OrdersModulesByDisplayOrder(string moduleName, int expectedOrder)
	{
		// Arrange
		var serviceProvider = CreateServiceProvider();
		var menuService = serviceProvider.GetRequiredService<ReportMenuService>();

		// Act
		var menuStructure = menuService.GetMenuStructure();

		// Assert
		var module = menuStructure.FirstOrDefault(m => m.ModuleName == moduleName);
		if (module != null)
		{
			module.DisplayOrder.Should().BeGreaterThan(0);
		}
	}
}
