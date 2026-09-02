using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StageFright.Core.Localization;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Rendering;

/// <summary>
/// Shared two-column, single-checkbox-column PDF page layout used by
/// <see cref="EventAttendanceSheetPdfRenderer"/> and <see cref="AgmAttendanceSheetPdfRenderer"/> so
/// both sheets render identically (User Story 3). Not published — <see cref="AttendanceRollPdfRenderer"/>
/// keeps its own separate two-checkbox-column layout and does not use this helper.
/// </summary>
internal static class CheckboxSheetPdfBuilder
{
    static CheckboxSheetPdfBuilder()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Conservative estimate of how many rows fit in one column of one A4 page at this layout's
    /// font size/padding — same tuning as <see cref="AttendanceRollPdfRenderer"/>'s equivalent
    /// constant (research.md Decision 6).
    /// </summary>
    private const int RowsPerColumn = 32;

    internal static byte[] Build(
        ILocalizer localizer,
        string organizationName,
        string title,
        string dateLine,
        string checkboxColumnHeader,
        IReadOnlyList<(string LastName, string FirstName, bool Checked)> rows)
    {
        var nameColumnHeader = localizer.Get<ReportsResource>("Reports_Sheet_NameColumn");
        var pagePrefix = localizer.Get<ReportsResource>("Reports_Render_PagePrefix");
        var pageSeparator = localizer.Get<ReportsResource>("Reports_Render_PageSeparator");

        var chunks = rows.Count == 0
            ? new[] { Array.Empty<(string LastName, string FirstName, bool Checked)>() }
            : rows.Chunk(RowsPerColumn * 2).ToArray();

        var document = Document.Create(container =>
        {
            foreach (var chunk in chunks)
            {
                var left = chunk.Take(RowsPerColumn).ToArray();
                var right = chunk.Skip(RowsPerColumn).ToArray();

                container.Page(page =>
                {
                    page.Margin(18); // minimum margin most printers can reliably print to (~0.25in)
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(t => t.FontSize(10));

                    page.Header().Column(col => BuildHeader(col, organizationName, title, dateLine));

                    page.Content().PaddingTop(10).Row(row =>
                    {
                        row.Spacing(20);

                        if (left.Length == 0)
                            return;

                        row.RelativeItem().Element(c => BuildMemberTable(c, left, nameColumnHeader, checkboxColumnHeader));

                        if (right.Length > 0)
                            row.RelativeItem().Element(c => BuildMemberTable(c, right, nameColumnHeader, checkboxColumnHeader));
                        else
                            row.RelativeItem();
                    });

                    page.Footer().AlignRight().Text(text =>
                    {
                        text.Span(pagePrefix).FontSize(8);
                        text.CurrentPageNumber().FontSize(8);
                        text.Span(pageSeparator).FontSize(8);
                        text.TotalPages().FontSize(8);
                    });
                });
            }
        });

        return document.GeneratePdf();
    }

    private static void BuildHeader(ColumnDescriptor col, string organizationName, string title, string dateLine)
    {
        if (!string.IsNullOrWhiteSpace(organizationName))
            col.Item().Text(organizationName).FontSize(22).Bold();
        col.Item().Text(title).FontSize(16).Bold();
        col.Item().Text(dateLine).FontSize(11).FontColor(Colors.Grey.Darken1);
        col.Item().PaddingTop(4).LineHorizontal(0.5f);
    }

    private static void BuildMemberTable(
        IContainer container,
        IReadOnlyList<(string LastName, string FirstName, bool Checked)> rows,
        string nameColumnHeader,
        string checkboxColumnHeader)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(def =>
            {
                def.RelativeColumn(4);
                def.RelativeColumn(1);
            });

            table.Header(header =>
            {
                header.Cell().Background(Colors.Grey.Lighten3).Padding(7).Text(nameColumnHeader).Bold().FontSize(9);
                header.Cell().Background(Colors.Grey.Lighten3).Padding(7).AlignCenter().Text(checkboxColumnHeader).Bold().FontSize(9);
            });

            foreach (var row in rows)
            {
                table.Cell().Padding(3).Text($"{row.LastName.ToUpperInvariant()}, {row.FirstName}");
                table.Cell().Padding(3).Element(c => CheckboxCell(c, row.Checked));
            }
        });
    }

    private static void CheckboxCell(IContainer container, bool @checked = false)
    {
        var box = container.AlignCenter().Border(1).BorderColor(Colors.Grey.Darken1).Width(12).Height(12);
        if (@checked)
            box.AlignCenter().AlignMiddle().Text("✓").FontSize(8).Bold();
    }
}
