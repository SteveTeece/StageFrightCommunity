using StageFright.Core.Localization;
using StageFright.Core.Modules.Agm;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Rendering;

/// <summary>
/// Renders a printable AGM attendance report to PDF bytes via the shared checkbox-sheet layout.
/// Sheet chrome (title, date line, column headers) is sourced from <see cref="ReportsResource"/>;
/// member names render verbatim.
/// </summary>
public class AgmAttendanceSheetPdfRenderer : IAgmAttendanceSheetPdfRenderer
{
    private readonly ILocalizer _localizer;

    public AgmAttendanceSheetPdfRenderer(ILocalizer localizer)
    {
        _localizer = localizer;
    }

    public byte[] Render(AgmAttendanceSheetData data, string organizationName = "")
    {
        var rows = data.Members
            .Select(m => (m.LastName, m.FirstName, Checked: m.Attended))
            .ToList();

        return CheckboxSheetPdfBuilder.Build(
            _localizer,
            organizationName,
            title: _localizer.Get<ReportsResource>("Reports_AgmSheet_Title"),
            dateLine: _localizer.Get<ReportsResource>(
                "Reports_AgmResults_MeetingDateLine", data.AgmDate.ToString("d MMMM yyyy")),
            checkboxColumnHeader: _localizer.Get<ReportsResource>("Reports_AgmSheet_AttendedColumn"),
            rows: rows);
    }
}
