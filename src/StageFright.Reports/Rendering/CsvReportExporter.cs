using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using StageFright.Reports.Models;

namespace StageFright.Reports.Rendering;

/// <summary>
/// Exports a ReportData structure to RFC 4180 CSV using CsvHelper.
/// First row = column headers. Commas and embedded quotes are escaped per RFC 4180.
/// </summary>
public class CsvReportExporter : ICsvReportExporter
{
    public string Export(ReportData report)
    {
        var sb = new StringBuilder();

        using var writer = new StringWriter(sb);
        var config = new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            NewLine = "\n"
        };

        using var csv = new CsvWriter(writer, config);

        // Write header row
        foreach (var col in report.Columns)
            csv.WriteField(col.Header);
        csv.NextRecord();

        // Write data rows from all sections
        foreach (var section in report.Sections)
        {
            if (!string.IsNullOrEmpty(section.Heading))
            {
                csv.WriteField(section.Heading);
                for (var i = 1; i < report.Columns.Count; i++)
                    csv.WriteField(string.Empty);
                csv.NextRecord();
            }

            foreach (var row in section.Rows)
            {
                for (var i = 0; i < report.Columns.Count; i++)
                {
                    var value = i < row.Cells.Count ? row.Cells[i] : string.Empty;
                    csv.WriteField(value);
                }
                csv.NextRecord();
            }

            if (section.Subtotal != null)
            {
                for (var i = 0; i < report.Columns.Count; i++)
                {
                    var value = i < section.Subtotal.Cells.Count ? section.Subtotal.Cells[i] : string.Empty;
                    csv.WriteField(value);
                }
                csv.NextRecord();
            }
        }

        if (report.GrandTotal != null)
        {
            for (var i = 0; i < report.Columns.Count; i++)
            {
                var value = i < report.GrandTotal.Cells.Count ? report.GrandTotal.Cells[i] : string.Empty;
                csv.WriteField(value);
            }
            csv.NextRecord();
        }

        return sb.ToString();
    }
}
