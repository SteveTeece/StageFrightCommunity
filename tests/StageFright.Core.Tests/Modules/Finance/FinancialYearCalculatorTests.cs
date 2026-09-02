using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Spec 028 US7 (FR-020): the financial year pivots on a (month, day) anchor, so a start
/// day other than the first of the month — and a February start day — must bound the year
/// correctly. The optional <c>startDay</c> parameter defaults to 1, which reproduces the
/// pre-028 first-of-month behaviour every existing caller relies on.
/// </summary>
public class FinancialYearCalculatorTests
{
    [Fact]
    public void Should_OpenTheYearOnTheAnchorDay_When_StartDayIsNotTheFirst()
    {
        // 6 April start (a real non-first-of-month fiscal calendar).
        var (from, to) = FinancialYearCalculator.GetRange(new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc), 4, 6);

        Assert.Equal(new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(new DateTime(2027, 4, 5, 23, 59, 59, DateTimeKind.Utc), to);
    }

    [Fact]
    public void Should_AssignADateBeforeTheAnchor_ToThePriorYear_When_StartDayIsNotTheFirst()
    {
        // 3 April 2026 is still inside the FY that opened 6 April 2025.
        var (from, to) = FinancialYearCalculator.GetRange(new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc), 4, 6);

        Assert.Equal(new DateTime(2025, 4, 6, 0, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(new DateTime(2026, 4, 5, 23, 59, 59, DateTimeKind.Utc), to);
    }

    [Fact]
    public void Should_TreatTheAnchorDayItself_AsTheFirstDayOfTheNewYear()
    {
        var (from, _) = FinancialYearCalculator.GetRange(new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc), 4, 6);

        Assert.Equal(new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc), from);
    }

    [Fact]
    public void Should_BoundAFebruaryStartDay_OnBothSidesOfTheAnchor()
    {
        var onOrAfter = FinancialYearCalculator.GetRange(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 2, 15);
        Assert.Equal(new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc), onOrAfter.From);
        Assert.Equal(new DateTime(2027, 2, 14, 23, 59, 59, DateTimeKind.Utc), onOrAfter.To);

        var before = FinancialYearCalculator.GetRange(new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc), 2, 15);
        Assert.Equal(new DateTime(2025, 2, 15, 0, 0, 0, DateTimeKind.Utc), before.From);
        Assert.Equal(new DateTime(2026, 2, 14, 23, 59, 59, DateTimeKind.Utc), before.To);
    }

    [Fact]
    public void Should_ReturnUtcRange_EndingAtEndOfTheLastDay()
    {
        var (from, to) = FinancialYearCalculator.GetRange(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), 4, 6);

        Assert.Equal(DateTimeKind.Utc, from.Kind);
        Assert.Equal(DateTimeKind.Utc, to.Kind);
        Assert.Equal((23, 59, 59), (to.Hour, to.Minute, to.Second));
    }

    [Fact]
    public void Should_ReturnThePriorTwelveMonths_When_GetPreviousRangeWithANonFirstStartDay()
    {
        var (from, to) = FinancialYearCalculator.GetPreviousRange(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), 4, 6);

        Assert.Equal(new DateTime(2025, 4, 6, 0, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(new DateTime(2026, 4, 5, 23, 59, 59, DateTimeKind.Utc), to);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(1)]
    [InlineData(12)]
    public void Should_MatchTheFirstOfMonthBehaviour_When_StartDayDefaultsToOne(int startMonth)
    {
        var probe = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(FinancialYearCalculator.GetRange(probe, startMonth), FinancialYearCalculator.GetRange(probe, startMonth, 1));
        Assert.Equal(FinancialYearCalculator.GetPreviousRange(probe, startMonth), FinancialYearCalculator.GetPreviousRange(probe, startMonth, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(40)]
    public void Should_ClampAnOutOfRangeStartDay_ToTheFirst(int startDay)
    {
        var probe = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(FinancialYearCalculator.GetRange(probe, 7, 1), FinancialYearCalculator.GetRange(probe, 7, startDay));
    }

    // --- Spec 028 FR-022 / issue #353: a sub-twelve-month first financial year (part-year) ---

    [Fact]
    public void Should_OpenTheFirstYearOnTheInceptionDate_When_InceptionFallsAfterTheAnchor()
    {
        // Founded 1 October; 1 July anchor → first FY runs 1 Oct 2026 – 30 Jun 2027.
        var (from, to, isPartYear) = FinancialYearCalculator.GetRange(
            new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc), 7, 1, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(new DateTime(2027, 6, 30, 23, 59, 59, DateTimeKind.Utc), to);
        Assert.True(isPartYear);
        Assert.Equal(DateTimeKind.Utc, from.Kind);
    }

    [Fact]
    public void Should_ReturnAFullTwelveMonths_When_InceptionFallsExactlyOnTheAnchor()
    {
        var (from, to, isPartYear) = FinancialYearCalculator.GetRange(
            new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc), 7, 1, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(new DateTime(2027, 6, 30, 23, 59, 59, DateTimeKind.Utc), to);
        Assert.False(isPartYear);
    }

    [Fact]
    public void Should_LeaveEverySubsequentYearAtTwelveMonths_When_ThereIsAnInceptionDate()
    {
        // Second financial year — inception is a year earlier, so no clamp and no part-year flag.
        var (from, to, isPartYear) = FinancialYearCalculator.GetRange(
            new DateTime(2027, 11, 15, 0, 0, 0, DateTimeKind.Utc), 7, 1, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2027, 7, 1, 0, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(new DateTime(2028, 6, 30, 23, 59, 59, DateTimeKind.Utc), to);
        Assert.False(isPartYear);
    }

    [Theory]
    [InlineData(5, 20)]
    [InlineData(8, 1)]
    [InlineData(1, 15)]
    public void Should_MatchTheThreeArgResult_When_InceptionDateIsNull(int month, int day)
    {
        var probe = new DateTime(2026, month, day, 0, 0, 0, DateTimeKind.Utc);

        var (from, to, isPartYear) = FinancialYearCalculator.GetRange(probe, 7, 1, null);
        var (previousFrom, previousTo, previousPartYear) = FinancialYearCalculator.GetPreviousRange(probe, 7, 1, null);

        Assert.Equal(FinancialYearCalculator.GetRange(probe, 7, 1), (from, to));
        Assert.Equal(FinancialYearCalculator.GetPreviousRange(probe, 7, 1), (previousFrom, previousTo));
        Assert.False(isPartYear);
        Assert.False(previousPartYear);
    }

    [Fact]
    public void Should_ReturnThePartYearFirstPeriod_When_GetPreviousRangeIsCalledFromYearTwo()
    {
        var (from, to, isPartYear) = FinancialYearCalculator.GetPreviousRange(
            new DateTime(2027, 11, 15, 0, 0, 0, DateTimeKind.Utc), 7, 1, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(new DateTime(2027, 6, 30, 23, 59, 59, DateTimeKind.Utc), to);
        Assert.True(isPartYear);
    }

    [Fact]
    public void Should_NotCollapseOntoThePartYear_When_GetPreviousRangeIsCalledFromInsideTheFirstYear()
    {
        // From inside the part-year first period, "previous" is the genuinely prior (empty) year.
        var (from, _, isPartYear) = FinancialYearCalculator.GetPreviousRange(
            new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc), 7, 1, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc), from);
        Assert.False(isPartYear);
    }
}
