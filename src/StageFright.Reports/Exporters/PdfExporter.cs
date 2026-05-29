using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using StageFright.Plugins.Contracts;
using System.IO;

namespace StageFright.Reports.Exporters;

/// <summary>
/// Exports report data to PDF format with professional formatting using iText.
/// </summary>
public class PdfExporter
{
	private const float PageMargin = 20f;
	private const float HeaderFontSize = 20f;
	private const float TableHeaderFontSize = 12f;
	private const float TableDataFontSize = 10f;

	/// <summary>
	/// Exports report data to PDF format.
	/// </summary>
	/// <param name="reportData">The report data to export</param>
	/// <returns>The PDF content as a byte array</returns>
	public byte[] ExportToPdf(ReportData reportData)
	{
		if (reportData == null)
			throw new ArgumentNullException(nameof(reportData));

		using var memoryStream = new MemoryStream();
		using var pdfWriter = new PdfWriter(memoryStream);
		using var pdfDoc = new PdfDocument(pdfWriter);
		using var document = new Document(pdfDoc);

		// Set margins
		document.SetMargins(PageMargin, PageMargin, PageMargin, PageMargin);

		// Add report title
		var titleParagraph = new Paragraph(reportData.ReportTitle)
			.SetFontSize(HeaderFontSize)
			.SetBold()
			.SetMarginBottom(10);
		document.Add(titleParagraph);

		// Add generation timestamp
		var generatedParagraph = new Paragraph($"Generated: {reportData.GeneratedAt:G}")
			.SetFontSize(10)
			.SetItalic()
			.SetMarginBottom(15);
		document.Add(generatedParagraph);

		// Create and add data table
		var table = CreateDataTable(reportData);
		document.Add(table);

		// Add summaries if available
		if (reportData.Summaries != null && reportData.Summaries.Count > 0)
		{
			document.Add(new Paragraph("\n"));

			var summaryHeading = new Paragraph("Summary")
				.SetFontSize(TableHeaderFontSize)
				.SetBold()
				.SetMarginTop(10)
				.SetMarginBottom(5);
			document.Add(summaryHeading);

			foreach (var summary in reportData.Summaries)
			{
				var summaryLine = new Paragraph($"{summary.Key}: {summary.Value}")
					.SetFontSize(TableDataFontSize)
					.SetMarginBottom(3);
				document.Add(summaryLine);
			}
		}

		document.Close();

		return memoryStream.ToArray();
	}

	/// <summary>
	/// Exports report data to a PDF file.
	/// </summary>
	/// <param name="reportData">The report data to export</param>
	/// <param name="filePath">The path where the PDF file will be saved</param>
	/// <returns>A task representing the asynchronous operation</returns>
	public async Task ExportToPdfFileAsync(ReportData reportData, string filePath)
	{
		if (reportData == null)
			throw new ArgumentNullException(nameof(reportData));

		if (string.IsNullOrEmpty(filePath))
			throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

		var pdfBytes = ExportToPdf(reportData);
		await File.WriteAllBytesAsync(filePath, pdfBytes);
	}

	/// <summary>
	/// Generates a suggested filename for PDF export based on report title and timestamp.
	/// </summary>
	/// <param name="reportTitle">The report title</param>
	/// <returns>A sanitized filename with .pdf extension</returns>
	public string GenerateFileName(string reportTitle)
	{
		if (string.IsNullOrEmpty(reportTitle))
			reportTitle = "Report";

		// Sanitize filename
		var invalidChars = Path.GetInvalidFileNameChars();
		var sanitized = new string(reportTitle
			.Where(c => !invalidChars.Contains(c))
			.ToArray());

		// Replace spaces with underscores
		sanitized = sanitized.Replace(" ", "_");

		// Add timestamp
		var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
		return $"{sanitized}_{timestamp}.pdf";
	}

	/// <summary>
	/// Creates a formatted PDF table from report data.
	/// </summary>
	private static Table CreateDataTable(ReportData reportData)
	{
		// Create table with number of columns matching headers
		var table = new Table(reportData.ColumnHeaders.Length)
			.SetWidth(UnitValue.CreatePercentValue(100));

		// Add header row
		foreach (var header in reportData.ColumnHeaders)
		{
			var headerCell = new Cell()
				.Add(new Paragraph(header))
				.SetBackgroundColor(new iText.Kernel.Colors.DeviceGray(0.85f))
				.SetBold()
				.SetTextAlignment(TextAlignment.CENTER)
				.SetPadding(5)
				.SetFontSize(TableHeaderFontSize);
			table.AddHeaderCell(headerCell);
		}

		// Add data rows
		foreach (var row in reportData.Rows)
		{
			foreach (var cell in row)
			{
				var dataCell = new Cell()
					.Add(new Paragraph(cell ?? string.Empty))
					.SetPadding(4)
					.SetFontSize(TableDataFontSize)
					.SetBorder(new SolidBorder(0.5f));
				table.AddCell(dataCell);
			}
		}

		return table;
	}
}
