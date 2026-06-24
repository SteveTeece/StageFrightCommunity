using StageFright.Reports.Models;

namespace StageFright.Reports.Rendering;

/// <summary>Renders a ReportData structure to a PDF byte array using QuestPDF.</summary>
public interface IPdfReportRenderer
{
    /// <summary>
    /// Renders the report to PDF bytes. The returned array is non-empty on success.
    /// Includes organization name (large bold header), title, subtitle/date range, generation date,
    /// all column headers, section headings, data rows, subtotals, and grand totals per FR-037.
    /// </summary>
    byte[] Render(ReportData report, string organizationName = "");
}
