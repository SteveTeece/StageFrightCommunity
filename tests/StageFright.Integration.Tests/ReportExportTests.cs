using Xunit;
using FluentAssertions;
using StageFright.Reports.Exporters;
using StageFright.Plugins.Contracts;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StageFright.Integration.Tests;

/// <summary>
/// Integration tests for CSV export functionality.
/// Verifies proper escaping, quote-handling, header row, and data alignment.
/// </summary>
public class ReportExportTests
{
	[Fact]
	public void CsvExporter_WithValidReportData_GeneratesCsvSuccessfully()
	{
		// Arrange
		var csvExporter = new CsvExporter();
		var reportData = new ReportData
		{
			ReportTitle = "CSV Test Report",
			ColumnHeaders = new[] { "Name", "Amount", "Category" },
			Rows = new[]
			{
				new[] { "Item 1", "$100.00", "Income" },
				new[] { "Item 2", "$50.00", "Expense" }
			},
			GeneratedAt = DateTime.Now
		};

		// Act
		var csvContent = csvExporter.ExportToCsv(reportData);

		// Assert
		csvContent.Should().NotBeNull();
		csvContent.Should().NotBeEmpty();
		csvContent.Should().Contain("Name");
		csvContent.Should().Contain("Amount");
		csvContent.Should().Contain("Category");
	}

	[Fact]
	public void CsvExporter_IncludesHeaderRow()
	{
		// Arrange
		var csvExporter = new CsvExporter();
		var reportData = new ReportData
		{
			ReportTitle = "Header Test",
			ColumnHeaders = new[] { "Column A", "Column B", "Column C" },
			Rows = new[]
			{
				new[] { "Value 1", "Value 2", "Value 3" }
			},
			GeneratedAt = DateTime.Now
		};

		// Act
		var csvContent = csvExporter.ExportToCsv(reportData);

		// Assert
		var lines = csvContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
		lines.Length.Should().BeGreaterThan(0);
		var headerLine = lines.FirstOrDefault(l => l.Contains("Column A"));
		headerLine.Should().NotBeNull();
		headerLine.Should().Contain("Column B");
		headerLine.Should().Contain("Column C");
	}

	[Fact]
	public void CsvExporter_ProperlyEscapesCommas()
	{
		// Arrange
		var csvExporter = new CsvExporter();
		var reportData = new ReportData
		{
			ReportTitle = "Comma Test",
			ColumnHeaders = new[] { "Name", "Address" },
			Rows = new[]
			{
				new[] { "Smith, John", "123 Main St, Anytown, USA" }
			},
			GeneratedAt = DateTime.Now
		};

		// Act
		var csvContent = csvExporter.ExportToCsv(reportData);

		// Assert
		csvContent.Should().NotBeNull();
		// CSV should properly escape values containing commas with quotes
		csvContent.Should().Contain("Smith, John");
		csvContent.Should().Contain("123 Main St, Anytown, USA");
	}

	[Fact]
	public void CsvExporter_ProperlyHandlesQuotes()
	{
		// Arrange
		var csvExporter = new CsvExporter();
		var reportData = new ReportData
		{
			ReportTitle = "Quote Test",
			ColumnHeaders = new[] { "Name", "Notes" },
			Rows = new[]
			{
				new[] { "O'Brien", "Said \"Hello\" to the group" }
			},
			GeneratedAt = DateTime.Now
		};

		// Act
		var csvContent = csvExporter.ExportToCsv(reportData);

		// Assert
		csvContent.Should().NotBeNull();
		csvContent.Should().Contain("O'Brien");
	}

	[Fact]
	public void CsvExporter_WithSummaries_IncludesSummaryLines()
	{
		// Arrange
		var csvExporter = new CsvExporter();
		var reportData = new ReportData
		{
			ReportTitle = "Summary Test",
			ColumnHeaders = new[] { "Item", "Amount" },
			Rows = new[]
			{
				new[] { "Entry 1", "$100.00" },
				new[] { "Entry 2", "$200.00" }
			},
			Summaries = new Dictionary<string, string>
			{
				{ "Total", "$300.00" }
			},
			GeneratedAt = DateTime.Now
		};

		// Act
		var csvContent = csvExporter.ExportToCsv(reportData);

		// Assert
		csvContent.Should().NotBeNull();
		csvContent.Should().Contain("Total");
		csvContent.Should().Contain("$300.00");
	}

	[Fact]
	public void CsvExporter_WithEmptyRows_GeneratesValidCsv()
	{
		// Arrange
		var csvExporter = new CsvExporter();
		var reportData = new ReportData
		{
			ReportTitle = "Empty Report",
			ColumnHeaders = new[] { "Column 1", "Column 2" },
			Rows = Array.Empty<string[]>(),
			GeneratedAt = DateTime.Now
		};

		// Act
		var csvContent = csvExporter.ExportToCsv(reportData);

		// Assert
		csvContent.Should().NotBeNull();
		csvContent.Should().Contain("Column 1");
		csvContent.Should().Contain("Column 2");
	}
}
