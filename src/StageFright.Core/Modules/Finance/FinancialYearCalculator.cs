namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Computes financial-year boundaries from a configurable start month + start day
/// (Settings.FinancialYearStartMonth / FinancialYearStartDay, default 7/1 = Australian
/// FY 1 Jul – 30 Jun). The year pivots on the (month, day) anchor: a date on or after the
/// anchor belongs to the FY that opened this calendar year; an earlier date belongs to the
/// prior FY. <c>startDay</c> defaults to 1, reproducing the pre-028 first-of-month behaviour.
/// </summary>
public static class FinancialYearCalculator
{
    /// <summary>Default FY start month (July) used before settings are available.</summary>
    public const int DefaultStartMonth = 7;

    /// <summary>Default FY start day (1st) used before settings are available.</summary>
    public const int DefaultStartDay = 1;

    /// <summary>
    /// Returns the inclusive UTC date range of the financial year containing
    /// <paramref name="date"/>. The end date carries a 23:59:59 time component.
    /// </summary>
    /// <param name="date">Any date within the target financial year.</param>
    /// <param name="startMonth">First month (1–12) of the financial year; out-of-range falls back to July.</param>
    /// <param name="startDay">Day of <paramref name="startMonth"/> (1–28) the year opens on; out-of-range falls back to the 1st.</param>
    public static (DateTime From, DateTime To) GetRange(DateTime date, int startMonth, int startDay = DefaultStartDay)
    {
        if (startMonth is < 1 or > 12)
            startMonth = DefaultStartMonth;
        if (startDay is < 1 or > 28)
            startDay = DefaultStartDay;

        // Pivot on the (month, day) anchor: on/after it → the FY that opened this calendar
        // year; before it → the prior FY. With startDay == 1 this reduces to date.Month >= startMonth.
        var onOrAfterAnchor = date.Month > startMonth
            || (date.Month == startMonth && date.Day >= startDay);
        var startYear = onOrAfterAnchor ? date.Year : date.Year - 1;

        var from = new DateTime(startYear, startMonth, startDay, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1).AddDays(-1);
        return (from, new DateTime(to.Year, to.Month, to.Day, 23, 59, 59, DateTimeKind.Utc));
    }

    /// <summary>Returns the financial year immediately before the one containing <paramref name="date"/>.</summary>
    public static (DateTime From, DateTime To) GetPreviousRange(DateTime date, int startMonth, int startDay = DefaultStartDay)
    {
        var (from, _) = GetRange(date, startMonth, startDay);
        return GetRange(from.AddDays(-1), startMonth, startDay);
    }
}
