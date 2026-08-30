namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Computes financial-year boundaries from a configurable start month + start day
/// (Settings.FinancialYearStartMonth / FinancialYearStartDay, default 7/1 = Australian
/// FY 1 Jul – 30 Jun). The year pivots on the (month, day) anchor: a date on or after the
/// anchor belongs to the FY that opened this calendar year; an earlier date belongs to the
/// prior FY. <c>startDay</c> defaults to 1, reproducing the pre-028 first-of-month behaviour.
/// The <c>inceptionDate</c> overloads additionally shorten the <em>first</em> financial year of
/// an organisation founded after the anchor and flag it as a part-year (spec 028, FR-022 / #353).
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

    /// <summary>
    /// First-period-aware range. When <paramref name="inceptionDate"/> is non-null and falls after
    /// the normal opening anchor of the financial year containing <paramref name="date"/>, the range
    /// opens on the inception date instead and <c>IsPartYear</c> is <see langword="true"/> — a first
    /// financial year shorter than twelve months (spec 028, FR-022). Every other year, and a null or
    /// on-anchor inception date, returns the full twelve-month range with <c>IsPartYear</c> false,
    /// identical to <see cref="GetRange(DateTime,int,int)"/>.
    /// </summary>
    /// <param name="date">Any date within the target financial year.</param>
    /// <param name="startMonth">First month (1–12) of the financial year; out-of-range falls back to July.</param>
    /// <param name="startDay">Day of <paramref name="startMonth"/> (1–28) the year opens on; out-of-range falls back to the 1st.</param>
    /// <param name="inceptionDate">Optional organisation founding date; only its date component is used.</param>
    public static (DateTime From, DateTime To, bool IsPartYear) GetRange(
        DateTime date, int startMonth, int startDay, DateTime? inceptionDate)
    {
        var (from, to) = GetRange(date, startMonth, startDay);

        if (inceptionDate is not { } inception)
            return (from, to, false);

        var opensAt = inception.Date;

        // Part-year only for the financial year that actually contains the inception date, and only
        // when the organisation was founded strictly after that year's normal opening anchor.
        if (opensAt > from && opensAt <= to)
        {
            var clampedFrom = new DateTime(opensAt.Year, opensAt.Month, opensAt.Day, 0, 0, 0, DateTimeKind.Utc);
            return (clampedFrom, to, true);
        }

        return (from, to, false);
    }

    /// <summary>
    /// First-period-aware equivalent of <see cref="GetPreviousRange(DateTime,int,int)"/>. Pivots on
    /// the current financial year's <em>un-clamped</em> opening anchor, so calling it from inside a
    /// part-year first period still returns the genuinely prior (empty) year rather than the
    /// part-year itself; from year two onward it returns the part-year first period with
    /// <c>IsPartYear</c> true.
    /// </summary>
    public static (DateTime From, DateTime To, bool IsPartYear) GetPreviousRange(
        DateTime date, int startMonth, int startDay, DateTime? inceptionDate)
    {
        var (from, _) = GetRange(date, startMonth, startDay);
        return GetRange(from.AddDays(-1), startMonth, startDay, inceptionDate);
    }
}
