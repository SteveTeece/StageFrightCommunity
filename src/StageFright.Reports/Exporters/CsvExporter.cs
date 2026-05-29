using CsvHelper;
using StageFright.Plugins.Contracts;
using System.Globalization;
using System.IO;
using System.Text;

namespace StageFright.Reports.Exporters;

/// <summary>
/// Exports report data to CSV format with proper escaping and comma-handling.
/// </summary>
public class CsvExporter
{
	/// <summary>
	/// Exports report data to CSV format.
	/// </summary>
	/// <param name="reportData">The report data to export</param>
	/// <returns>The CSV content as a string</returns>
	public string ExportToCsv(ReportData reportData)
	{
		if (reportData == null)
			throw new ArgumentNullException(nameof(reportData));

		using var memoryStream = new MemoryStream();
		using (var writer = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true))
		using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
		{
			// Write report title as a comment
			writer.WriteLine($"# {reportData.ReportTitle}");
			writer.WriteLine($"# Generated: {reportData.GeneratedAt:G}");
			writer.WriteLine();

			// Write column headers
			foreach (var header in reportData.ColumnHeaders)
			{
				csv.WriteField(header);
			}
			csv.NextRecord();

			// Write data rows
			foreach (var row in reportData.Rows)
			{
				foreach (var cell in row)
				{
					csv.WriteField(cell);
				}
				csv.NextRecord();
			}

			// Write summaries section if available
			if (reportData.Summaries != null && reportData.Summaries.Count > 0)
			{
				writer.WriteLine();
				writer.WriteLine("# Summary");
				foreach (var summary in reportData.Summaries)
				{
					csv.WriteField(summary.Key);
					csv.WriteField(summary.Value);
					csv.NextRecord();
				}
			}

			csv.Flush();
		}

		memoryStream.Position = 0;
		using var reader = new StreamReader(memoryStream, Encoding.UTF8);
		return reader.ReadToEnd();
	}

	/// <summary>
	/// Exports report data to a CSV file.
	/// </summary>
	/// <param name="reportData">The report data to export</param>
	/// <param name="filePath">The path where the CSV file will be saved</param>
	/// <returns>A task representing the asynchronous operation</returns>
	public async Task ExportToCsvFileAsync(ReportData reportData, string filePath)
	{
		if (reportData == null)
			throw new ArgumentNullException(nameof(reportData));

		if (string.IsNullOrEmpty(filePath))
			throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

		var csvContent = ExportToCsv(reportData);
		await File.WriteAllTextAsync(filePath, csvContent, Encoding.UTF8);
	}

	/// <summary>
	/// Exports report data to a CSV byte array suitable for download.
	/// </summary>
	/// <param name="reportData">The report data to export</param>
	/// <returns>The CSV content as a byte array</returns>
	public byte[] ExportToCsvBytes(ReportData reportData)
	{
		var csvContent = ExportToCsv(reportData);
		return Encoding.UTF8.GetBytes(csvContent);
	}

	/// <summary>
	/// Generates a suggested filename for CSV export based on report title and timestamp.
	/// </summary>
	/// <param name="reportTitle">The report title</param>
	/// <returns>A sanitized filename with .csv extension</returns>
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
		return $"{sanitized}_{timestamp}.csv";
	}
}
