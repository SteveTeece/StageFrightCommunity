using StageFright.Reports.Models;

namespace StageFright.Reports.Rendering;

/// <summary>Exports a ReportData structure to an RFC 4180 CSV string using CsvHelper.</summary>
public interface ICsvReportExporter
{
    /// <summary>
    /// Exports the report to a CSV string. First row contains column headers.
    /// Values containing commas, quotes, or newlines are RFC 4180 quote-escaped per FR-041.
    /// </summary>
    string Export(ReportData report);
}
