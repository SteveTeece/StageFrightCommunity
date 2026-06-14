using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StageFright.Core.Enums;
using StageFright.Reports.Models;

namespace StageFright.Reports.Rendering;

/// <summary>
/// Renders a ReportData structure to PDF bytes using QuestPDF.
/// Includes title, subtitle, generation date, column headers, section headings,
/// data rows, subtotals, and grand totals per FR-037.
/// </summary>
public class PdfReportRenderer : IPdfReportRenderer
{
    static PdfReportRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(ReportData report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text(report.Title).FontSize(16).Bold();
                    if (!string.IsNullOrEmpty(report.SubTitle))
                        col.Item().Text(report.SubTitle).FontSize(11).FontColor(Colors.Grey.Darken1);
                    col.Item().Text($"Generated: {report.GeneratedAt:d MMMM yyyy HH:mm} UTC")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(4).LineHorizontal(0.5f);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    if (report.Columns.Count == 0)
                        return;

                    // Flatten all content into one table
                    col.Item().Table(table =>
                    {
                        var colCount = (uint)report.Columns.Count;

                        table.ColumnsDefinition(def =>
                        {
                            foreach (var c in report.Columns)
                            {
                                if (c.Alignment == ReportColumnAlignment.Left)
                                    def.RelativeColumn(3);
                                else
                                    def.RelativeColumn(1);
                            }
                        });

                        // Header row
                        table.Header(header =>
                        {
                            foreach (var c in report.Columns)
                            {
                                var hCell = header.Cell().Background(Colors.Grey.Lighten3).Padding(4);
                                hCell.Text(c.Header).Bold().FontSize(9);
                            }
                        });

                        foreach (var section in report.Sections)
                        {
                            if (!string.IsNullOrEmpty(section.Heading))
                            {
                                table.Cell().ColumnSpan(colCount).PaddingTop(8).PaddingBottom(2)
                                    .Text(section.Heading).Bold().FontSize(11);
                            }

                            foreach (var row in section.Rows)
                                WriteDataRow(table, row, report.Columns, row.IsEmphasized);

                            if (section.Subtotal != null)
                            {
                                table.Cell().ColumnSpan(colCount).LineHorizontal(0.5f);
                                WriteDataRow(table, section.Subtotal, report.Columns, true);
                            }
                        }

                        if (report.GrandTotal != null)
                        {
                            table.Cell().ColumnSpan(colCount).PaddingTop(4).LineHorizontal(1f);
                            WriteDataRow(table, report.GrandTotal, report.Columns, true);
                        }
                    });
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Page ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" of ").FontSize(8);
                    text.TotalPages().FontSize(8);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void WriteDataRow(TableDescriptor table, ReportRow row, IReadOnlyList<ReportColumn> columns, bool bold)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            var value = i < row.Cells.Count ? row.Cells[i] : string.Empty;
            var cell = table.Cell().Padding(3);
            var txt = col.Alignment switch
            {
                ReportColumnAlignment.Right => cell.AlignRight().Text(value),
                ReportColumnAlignment.Center => cell.AlignCenter().Text(value),
                _ => cell.Text(value)
            };
            if (bold) txt.Bold();
        }
    }
}
