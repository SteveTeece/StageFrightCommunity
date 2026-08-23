using StageFright.Core.Modules.Agm;

namespace StageFright.Reports.Rendering;

/// <summary>Renders a printable AGM attendance report to PDF bytes.</summary>
public interface IAgmAttendanceSheetPdfRenderer
{
    /// <summary>
    /// Renders an AGM attendance report to PDF bytes, using the same layout rules as
    /// <see cref="IEventAttendanceSheetPdfRenderer"/> so both sheets look and behave identically.
    /// The returned array is non-empty on success, even for a zero-member roster — pure function, no I/O.
    /// </summary>
    byte[] Render(AgmAttendanceSheetData data, string organizationName = "");
}
