namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Computes financial-year boundaries from a configurable start month
/// (Settings.FinancialYearStartMonth, default 7 = Australian FY 1 Jul – 30 Jun).
/// </summary>
public static class FinancialYearCalculator
{
    /// <summary>Default FY start month (July) used before settings are available.</summary>
    public const int DefaultStartMonth = 7;

    /// <summary>
    /// Returns the inclusive UTC date range of the financial year containing
    /// <paramref name="date"/>. The end date carries a 23:59:59 time component.
    /// </summary>
    public static (DateTime From, DateTime To) GetRange(DateTime date, int startMonth)
    {
        if (startMonth is < 1 or > 12)
            startMonth = DefaultStartMonth;

        var startYear = date.Month >= startMonth ? date.Year : date.Year - 1;
        var from = new DateTime(startYear, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1).AddDays(-1);
        return (from, new DateTime(to.Year, to.Month, to.Day, 23, 59, 59, DateTimeKind.Utc));
    }

    /// <summary>Returns the financial year immediately before the one containing <paramref name="date"/>.</summary>
    public static (DateTime From, DateTime To) GetPreviousRange(DateTime date, int startMonth)
    {
        var (from, _) = GetRange(date, startMonth);
        return GetRange(from.AddDays(-1), startMonth);
    }
}
