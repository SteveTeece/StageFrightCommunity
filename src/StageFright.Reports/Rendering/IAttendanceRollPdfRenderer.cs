using StageFright.Core.Modules.Rehearsals;

namespace StageFright.Reports.Rendering;

/// <summary>Renders a printable attendance roll to PDF bytes.</summary>
public interface IAttendanceRollPdfRenderer
{
    /// <summary>
    /// Renders an attendance roll to PDF bytes: two-column layout, minimal-width checkbox
    /// columns, wrapping column headings, surname in capitals alongside first name. The returned
    /// array is non-empty on success, even for a zero-member roll — pure function, no I/O.
    /// </summary>
    byte[] Render(AttendanceRollData data, string organizationName = "");
}
