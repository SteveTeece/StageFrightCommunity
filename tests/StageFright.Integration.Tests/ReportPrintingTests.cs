using Xunit;
using FluentAssertions;
using StageFright.Reports.Exporters;
using StageFright.Plugins.Contracts;
using System;
using System.Collections.Generic;

namespace StageFright.Integration.Tests;

/// <summary>
/// Unit tests for report printing functionality via PDF export.
/// Note: Full PDF generation requires itext-bouncy-castle-adapter. 
/// These tests verify the core logic and interface contract.
/// </summary>
public class ReportPrintingTests
{
	[Fact]
	public void PdfExporter_Instantiates()
	{
		// Arrange & Act
		var pdfExporter = new PdfExporter();

		// Assert
		pdfExporter.Should().NotBeNull();
	}

	[Fact]
	public void ReportData_WithTitle_StoresTitle()
	{
		// Arrange & Act
		var reportData = new ReportData
		{
			ReportTitle = "Test Report",
			ColumnHeaders = new[] { "Column 1", "Column 2" },
			Rows = Array.Empty<string[]>(),
			GeneratedAt = DateTime.Now
		};

		// Assert
		reportData.ReportTitle.Should().Be("Test Report");
	}

	[Fact]
	public void ReportData_WithHeaders_StoresHeaders()
	{
		// Arrange & Act
		var reportData = new ReportData
		{
			ReportTitle = "Test",
			ColumnHeaders = new[] { "Name", "Amount", "Date" },
			Rows = Array.Empty<string[]>(),
			GeneratedAt = DateTime.Now
		};

		// Assert
		reportData.ColumnHeaders.Should().Equal("Name", "Amount", "Date");
	}

	[Fact]
	public void ReportData_WithRows_StoresRows()
	{
		// Arrange & Act
		var reportData = new ReportData
		{
			ReportTitle = "Test",
			ColumnHeaders = new[] { "Item", "Value" },
			Rows = new[]
			{
				new[] { "Item 1", "$100" },
				new[] { "Item 2", "$200" }
			},
			GeneratedAt = DateTime.Now
		};

		// Assert
		reportData.Rows.Should().HaveCount(2);
		reportData.Rows[0].Should().Equal("Item 1", "$100");
		reportData.Rows[1].Should().Equal("Item 2", "$200");
	}

	[Fact]
	public void ReportData_WithSummaries_StoresSummaries()
	{
		// Arrange & Act
		var reportData = new ReportData
		{
			ReportTitle = "Test",
			ColumnHeaders = new[] { "Item" },
			Rows = Array.Empty<string[]>(),
			Summaries = new Dictionary<string, string>
			{
				{ "Total", "$300.00" },
				{ "Count", "3" }
			},
			GeneratedAt = DateTime.Now
		};

		// Assert
		reportData.Summaries.Should().HaveCount(2);
		reportData.Summaries["Total"].Should().Be("$300.00");
		reportData.Summaries["Count"].Should().Be("3");
	}
}
