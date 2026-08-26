using StageFright.Core.Modules.Agm;

namespace StageFright.Reports.Rendering;

/// <summary>Renders a printable AGM results report (date, attendance count, elected positions) to PDF bytes.</summary>
public interface IAgmResultsPdfRenderer
{
    /// <summary>
    /// Renders an AGM results report to PDF bytes. The returned array is non-empty on success,
    /// even when no positions are recorded — pure function, no I/O.
    /// </summary>
    byte[] Render(AgmResultsData data, string organizationName = "");
}
