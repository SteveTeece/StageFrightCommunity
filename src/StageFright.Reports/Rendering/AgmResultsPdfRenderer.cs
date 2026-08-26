using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StageFright.Core.Modules.Agm;

namespace StageFright.Reports.Rendering;

/// <summary>
/// Renders a printable AGM results report to PDF bytes directly with QuestPDF — a plain
/// position list, not a checkbox roll, so it doesn't use <see cref="CheckboxSheetPdfBuilder"/>;
/// follows <see cref="PdfReportRenderer"/>'s page/header/footer layout instead.
/// </summary>
public class AgmResultsPdfRenderer : IAgmResultsPdfRenderer
{
    static AgmResultsPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(AgmResultsData data, string organizationName = "")
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
                    if (!string.IsNullOrWhiteSpace(organizationName))
                        col.Item().Text(organizationName).FontSize(22).Bold();
                    col.Item().Text("AGM Results").FontSize(16).Bold();
                    col.Item().Text($"Annual General Meeting: {data.AgmDate:d MMMM yyyy}")
                        .FontSize(11).FontColor(Colors.Grey.Darken1);
                    col.Item().Text($"Attendance: {data.AttendedCount} of {data.TotalCount} members attended")
                        .FontSize(11).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(0.5f);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Item().Text("Elected Positions").FontSize(12).Bold();

                    if (data.PositionLines.Count == 0 && data.GeneralCommitteeMemberNames.Count == 0)
                    {
                        col.Item().PaddingTop(4).Text("No positions recorded.");
                        return;
                    }

                    foreach (var line in data.PositionLines)
                    {
                        col.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span($"{line.Label}: ").Bold();
                            t.Span(line.MemberText);
                        });
                    }

                    if (data.GeneralCommitteeMemberNames.Count > 0)
                    {
                        col.Item().PaddingTop(4).Text("General Committee Member:").Bold();
                        foreach (var name in data.GeneralCommitteeMemberNames)
                        {
                            col.Item().PaddingTop(1).PaddingLeft(10).Text(name);
                        }
                    }
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
}
