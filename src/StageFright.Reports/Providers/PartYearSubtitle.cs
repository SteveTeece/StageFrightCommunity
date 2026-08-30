using StageFright.Core.Localization;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Providers;

/// <summary>
/// Wraps a financial-statement subtitle with the "part-year — first financial year" disclosure
/// when the reported default period is an organisation's sub-twelve-month first financial year
/// (spec 028, FR-022 / issue #353). A no-op when <c>isPartYear</c> is false, so a call site can
/// apply it unconditionally.
/// </summary>
internal static class PartYearSubtitle
{
    public static string Wrap(ILocalizer localizer, string subtitle, bool isPartYear) =>
        isPartYear
            ? localizer.Get<ReportsResource>("Reports_Common_PartYearSubtitle", subtitle)
            : subtitle;
}
