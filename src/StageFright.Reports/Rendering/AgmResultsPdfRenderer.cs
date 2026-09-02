using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Agm;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Rendering;

/// <summary>
/// Renders a printable AGM results report to PDF bytes directly with QuestPDF — a plain
/// position list, not a checkbox roll, so it doesn't use <see cref="CheckboxSheetPdfBuilder"/>;
/// follows <see cref="PdfReportRenderer"/>'s page/header/footer layout instead. All page chrome
/// is sourced from <see cref="ReportsResource"/>; position labels and member names render verbatim.
/// </summary>
public class AgmResultsPdfRenderer : IAgmResultsPdfRenderer
{
    private readonly ILocalizer _localizer;

    static AgmResultsPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public AgmResultsPdfRenderer(ILocalizer localizer)
    {
        _localizer = localizer;
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
                    col.Item().Text(_localizer.Get<ReportsResource>("Reports_AgmResults_Title")).FontSize(16).Bold();
                    col.Item().Text(_localizer.Get<ReportsResource>(
                            "Reports_AgmResults_MeetingDateLine", data.AgmDate.ToString("d MMMM yyyy")))
                        .FontSize(11).FontColor(Colors.Grey.Darken1);
                    col.Item().Text(_localizer.Get<ReportsResource>(
                            "Reports_AgmResults_AttendanceLine", data.AttendedCount, data.TotalCount))
                        .FontSize(11).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(0.5f);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Item().Text(_localizer.Get<ReportsResource>("Reports_AgmResults_ElectedPositionsHeading")).FontSize(12).Bold();

                    if (data.PositionLines.Count == 0 && data.GeneralCommitteeMemberNames.Count == 0)
                    {
                        col.Item().PaddingTop(4).Text(_localizer.Get<ReportsResource>("Reports_AgmResults_NoPositions"));
                        return;
                    }

                    foreach (var line in data.PositionLines)
                    {
                        col.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span(_localizer.Get<ReportsResource>("Reports_AgmResults_PositionLabel", line.Label)).Bold();
                            t.Span(line.MemberText);
                        });
                    }

                    if (data.GeneralCommitteeMemberNames.Count > 0)
                    {
                        col.Item().PaddingTop(4).Text(_localizer.Get<ReportsResource>("Reports_AgmResults_GeneralCommitteeMemberLabel")).Bold();
                        foreach (var name in data.GeneralCommitteeMemberNames)
                        {
                            col.Item().PaddingTop(1).PaddingLeft(10).Text(name);
                        }
                    }
                });

                var pagePrefix = _localizer.Get<ReportsResource>("Reports_Render_PagePrefix");
                var pageSeparator = _localizer.Get<ReportsResource>("Reports_Render_PageSeparator");
                page.Footer().AlignRight().Text(text =>
                {
                    text.Span(pagePrefix).FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(pageSeparator).FontSize(8);
                    text.TotalPages().FontSize(8);
                });
            });
        });

        return document.GeneratePdf();
    }
}
