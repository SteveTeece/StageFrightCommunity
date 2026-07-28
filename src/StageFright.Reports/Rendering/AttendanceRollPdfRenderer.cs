using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StageFright.Core.Modules.Rehearsals;

namespace StageFright.Reports.Rendering;

/// <summary>Renders a printable attendance roll to PDF bytes using QuestPDF.</summary>
public class AttendanceRollPdfRenderer : IAttendanceRollPdfRenderer
{
    static AttendanceRollPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Conservative estimate of how many roll rows fit in one column of one A4 page at this
    /// renderer's font size/padding. Tuned via manual visual check (research.md Outstanding Risks).
    /// </summary>
    private const int RowsPerColumn = 32;

    public byte[] Render(AttendanceRollData data, string organizationName = "")
    {
        var chunks = data.Members.Count == 0
            ? new[] { Array.Empty<AttendanceRollMember>() }
            : data.Members.Chunk(RowsPerColumn * 2).ToArray();

        var feeHeaderText = data.AttendanceFeeAmount.ToString("C0");

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

                    page.Header().Column(col => BuildHeader(col, data, organizationName));

                    page.Content().PaddingTop(10).Row(row =>
                    {
                        row.Spacing(20);

                        if (left.Length == 0)
                            return;

                        row.RelativeItem().Element(c => BuildMemberTable(c, left, feeHeaderText));

                        if (right.Length > 0)
                            row.RelativeItem().Element(c => BuildMemberTable(c, right, feeHeaderText));
                        else
                            row.RelativeItem();
                    });

                    page.Footer().AlignRight().Text(text =>
                    {
                        text.Span("Page ").FontSize(8);
                        text.CurrentPageNumber().FontSize(8);
                        text.Span(" of ").FontSize(8);
                        text.TotalPages().FontSize(8);
                    });
                });
            }
        });

        return document.GeneratePdf();
    }

    private static void BuildHeader(ColumnDescriptor col, AttendanceRollData data, string organizationName)
    {
        if (!string.IsNullOrWhiteSpace(organizationName))
            col.Item().Text(organizationName).FontSize(22).Bold();
        col.Item().Text("Attendance Roll").FontSize(16).Bold();
        col.Item().Text($"Rehearsal: {data.RehearsalDate:d MMMM yyyy} at {data.RehearsalTime:hh\\:mm}")
            .FontSize(11).FontColor(Colors.Grey.Darken1);
        col.Item().Text($"Generated: {DateTime.UtcNow:d MMMM yyyy HH:mm} UTC")
            .FontSize(9).FontColor(Colors.Grey.Medium);
        col.Item().PaddingTop(4).LineHorizontal(0.5f);
    }

    private static void BuildMemberTable(IContainer container, IReadOnlyList<AttendanceRollMember> members, string feeHeaderText)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(def =>
            {
                def.RelativeColumn(4);
                def.RelativeColumn(1);
                def.RelativeColumn(1);
            });

            table.Header(header =>
            {
                header.Cell().Background(Colors.Grey.Lighten3).Padding(7).Text("Name").Bold().FontSize(9);
                header.Cell().Background(Colors.Grey.Lighten3).Padding(7).Text("Present").Bold().FontSize(9);
                header.Cell().Background(Colors.Grey.Lighten3).Padding(7).Text(feeHeaderText).Bold().FontSize(9);
            });

            foreach (var member in members)
            {
                table.Cell().Padding(3).Text($"{member.LastName.ToUpperInvariant()}, {member.FirstName}");
                table.Cell().Padding(3).Element(c => CheckboxCell(c, member.Attended));
                table.Cell().Padding(3).Element(c => CheckboxCell(c, member.RehearsalFeePaid));
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
