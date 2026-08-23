using StageFright.Core.Modules.Agm;

namespace StageFright.Reports.Rendering;

/// <summary>Renders a printable AGM attendance report to PDF bytes via the shared checkbox-sheet layout.</summary>
public class AgmAttendanceSheetPdfRenderer : IAgmAttendanceSheetPdfRenderer
{
    public byte[] Render(AgmAttendanceSheetData data, string organizationName = "")
    {
        var rows = data.Members
            .Select(m => (m.LastName, m.FirstName, Checked: m.Attended))
            .ToList();

        return CheckboxSheetPdfBuilder.Build(
            organizationName,
            title: "AGM Attendance Report",
            dateLine: $"Annual General Meeting: {data.AgmDate:d MMMM yyyy}",
            checkboxColumnHeader: "Attended",
            rows: rows);
    }
}
