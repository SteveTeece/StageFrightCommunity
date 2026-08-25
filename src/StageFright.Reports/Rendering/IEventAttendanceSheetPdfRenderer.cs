using StageFright.Core.Modules.Events;

namespace StageFright.Reports.Rendering;

/// <summary>Renders a printable event attendance sheet to PDF bytes.</summary>
public interface IEventAttendanceSheetPdfRenderer
{
    /// <summary>
    /// Renders an event attendance sheet to PDF bytes: two-column layout, minimal-width
    /// checkbox column, wrapping column headings, surname in capitals alongside first name. The
    /// returned array is non-empty on success, even for a zero-member sheet — pure function, no I/O.
    /// </summary>
    byte[] Render(EventAttendanceSheetData data, string organizationName = "");
}
