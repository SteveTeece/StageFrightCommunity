using StageFright.Core.Modules.Events;

namespace StageFright.Reports.Rendering;

/// <summary>Renders a printable event attendance sheet to PDF bytes via the shared checkbox-sheet layout.</summary>
public class EventAttendanceSheetPdfRenderer : IEventAttendanceSheetPdfRenderer
{
    public byte[] Render(EventAttendanceSheetData data, string organizationName = "")
    {
        var rows = data.Members
            .Select(m => (m.LastName, m.FirstName, Checked: m.Participated))
            .ToList();

        return CheckboxSheetPdfBuilder.Build(
            organizationName,
            title: "Event Attendance Sheet",
            dateLine: $"{data.EventTypeName}: {data.EventDate:d MMMM yyyy}",
            checkboxColumnHeader: "Participated",
            rows: rows);
    }
}
