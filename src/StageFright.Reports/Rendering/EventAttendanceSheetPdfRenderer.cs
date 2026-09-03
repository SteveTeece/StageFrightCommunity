using StageFright.Core.Localization;
using StageFright.Core.Modules.Events;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Rendering;

/// <summary>
/// Renders a printable event attendance sheet to PDF bytes via the shared checkbox-sheet layout.
/// Sheet chrome (title, date line, column headers) is sourced from <see cref="ReportsResource"/>;
/// the event type name and member names render verbatim.
/// </summary>
public class EventAttendanceSheetPdfRenderer : IEventAttendanceSheetPdfRenderer
{
    private readonly ILocalizer _localizer;

    public EventAttendanceSheetPdfRenderer(ILocalizer localizer)
    {
        _localizer = localizer;
    }

    public byte[] Render(EventAttendanceSheetData data, string organizationName = "")
    {
        var rows = data.Members
            .Select(m => (m.LastName, m.FirstName, Checked: m.Participated))
            .ToList();

        return CheckboxSheetPdfBuilder.Build(
            _localizer,
            organizationName,
            title: _localizer.Get<ReportsResource>("Reports_EventSheet_Title"),
            dateLine: _localizer.Get<ReportsResource>(
                "Reports_EventSheet_DateLine", data.EventTypeName, data.EventDate.ToString("d MMMM yyyy")),
            checkboxColumnHeader: _localizer.Get<ReportsResource>("Reports_EventSheet_ParticipatedColumn"),
            rows: rows);
    }
}
