using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Members;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Members;

/// <summary>
/// Unit tests for AgeCalculationService — age algorithm (FR-002a examples) and DOB validation.
/// </summary>
public class AgeCalculationServiceTests : TestBase
{
    private readonly AgeCalculationService _svc = new(RealLocalizer.Instance);

    // --- Age calculation ---

    [Fact]
    public void Calculate_Returns35_When_Dob19900228_And_Today20260227()
    {
        var dob = new DateTime(1990, 2, 28, 0, 0, 0, DateTimeKind.Utc);
        var today = new DateTime(2026, 2, 27, 0, 0, 0, DateTimeKind.Utc);

        var age = _svc.Calculate(dob, today);

        Assert.Equal(35, age);
    }

    [Fact]
    public void Calculate_Returns33_When_LeapDob19920229_And_Today20260228()
    {
        var dob = new DateTime(1992, 2, 29, 0, 0, 0, DateTimeKind.Utc);
        var today = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc);

        var age = _svc.Calculate(dob, today);

        Assert.Equal(33, age);
    }

    [Fact]
    public void Calculate_ReturnsNull_When_DobIsNull()
    {
        var today = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var age = _svc.Calculate(null, today);

        Assert.Null(age);
    }

    [Fact]
    public void Calculate_ReturnsBirthdayAge_On_ExactBirthday()
    {
        var dob = new DateTime(1990, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var today = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var age = _svc.Calculate(dob, today);

        Assert.Equal(36, age);
    }

    [Fact]
    public void Calculate_ReturnsDayBeforeBirthday_Age_Minus1()
    {
        var dob = new DateTime(1990, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var today = new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc);

        var age = _svc.Calculate(dob, today);

        Assert.Equal(35, age);
    }

    [Fact]
    public void Calculate_ReturnsCorrectAge_OnMarchFirst_ForFeb29Dob()
    {
        // Non-leap "today" year — the anniversary falls back to 1 March, and today IS
        // that fallback anniversary, so the birthday has just been reached.
        var dob = new DateTime(1992, 2, 29, 0, 0, 0, DateTimeKind.Utc);
        var today = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        var age = _svc.Calculate(dob, today);

        Assert.Equal(34, age);
    }

    [Fact]
    public void Calculate_ReturnsPreviousAge_OnFeb28_ForFeb29Dob_InLeapYear()
    {
        // today.Year (2028) IS itself a leap year, so 29 Feb is a real date this year —
        // no Mar-1 fallback applies. On 28 Feb the real anniversary (29 Feb) hasn't arrived yet.
        var dob = new DateTime(1992, 2, 29, 0, 0, 0, DateTimeKind.Utc);
        var today = new DateTime(2028, 2, 28, 0, 0, 0, DateTimeKind.Utc);

        var age = _svc.Calculate(dob, today);

        Assert.Equal(35, age);
    }

    [Fact]
    public void Calculate_IncrementsAge_OnFeb29_ForFeb29Dob_InLeapYear()
    {
        // today IS the real 29 Feb anniversary in a leap "today.Year" — age increments.
        var dob = new DateTime(1992, 2, 29, 0, 0, 0, DateTimeKind.Utc);
        var today = new DateTime(2028, 2, 29, 0, 0, 0, DateTimeKind.Utc);

        var age = _svc.Calculate(dob, today);

        Assert.Equal(36, age);
    }

    // --- DOB validation ---

    [Fact]
    public void ValidateDateOfBirth_Throws_When_DobIsInFuture()
    {
        var futureDob = DateTime.UtcNow.Date.AddDays(1);
        var today = DateTime.UtcNow.Date;

        var ex = Assert.Throws<ValidationException>(
            () => _svc.ValidateDateOfBirth(futureDob, today, 150, 0));

        Assert.Contains("past", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateDateOfBirth_Throws_When_DobIsToday()
    {
        var today = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var ex = Assert.Throws<ValidationException>(
            () => _svc.ValidateDateOfBirth(today, today, 150, 0));

        Assert.Contains("past", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateDateOfBirth_Throws_When_AgeExceedsMaxRange()
    {
        // Age = 200 years → exceeds maxAgeRangeYears=150
        var dob = new DateTime(1826, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var today = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var ex = Assert.Throws<ValidationException>(
            () => _svc.ValidateDateOfBirth(dob, today, 150, 0));

        Assert.Contains("150", ex.Message);
    }

    [Fact]
    public void ValidateDateOfBirth_Throws_When_AgeBelowMinimum()
    {
        // Age = 16 → below minimumMemberAge=18
        var dob = new DateTime(2010, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var today = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var ex = Assert.Throws<ValidationException>(
            () => _svc.ValidateDateOfBirth(dob, today, 150, 18));

        Assert.Contains("18", ex.Message);
    }

    [Fact]
    public void ValidateDateOfBirth_DoesNotThrow_When_DobIsNull()
    {
        var today = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Must not throw — DOB is optional
        _svc.ValidateDateOfBirth(null, today, 150, 0);
    }

    [Fact]
    public void ValidateDateOfBirth_DoesNotThrow_When_DobIsValid()
    {
        var dob = new DateTime(1990, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var today = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Valid: age=36, within range 0..150, above minimum 18
        _svc.ValidateDateOfBirth(dob, today, 150, 18);
    }

    [Fact]
    public void ValidateDateOfBirth_DoesNotThrow_When_AgeExactlyEqualsMaxAgeRangeYears()
    {
        var today = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var dob = new DateTime(1976, 6, 1, 0, 0, 0, DateTimeKind.Utc); // age=50, exactly at max

        _svc.ValidateDateOfBirth(dob, today, 50, 0);
    }

    [Fact]
    public void ValidateDateOfBirth_Throws_When_AgeIsMaxAgeRangeYearsPlusOne()
    {
        var today = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var dob = new DateTime(1975, 6, 1, 0, 0, 0, DateTimeKind.Utc); // age=51, one over max=50

        var ex = Assert.Throws<ValidationException>(
            () => _svc.ValidateDateOfBirth(dob, today, 50, 0));

        Assert.Contains("50", ex.Message);
    }

    [Fact]
    public void ValidateDateOfBirth_DoesNotThrow_When_AgeExactlyEqualsMinimumMemberAge()
    {
        var today = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var dob = new DateTime(2008, 6, 1, 0, 0, 0, DateTimeKind.Utc); // age=18, exactly at minimum

        _svc.ValidateDateOfBirth(dob, today, 150, 18);
    }

    [Fact]
    public void ValidateDateOfBirth_Throws_When_AgeIsMinimumMemberAgeMinusOne()
    {
        var today = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var dob = new DateTime(2008, 6, 2, 0, 0, 0, DateTimeKind.Utc); // age=17, one under minimum=18

        var ex = Assert.Throws<ValidationException>(
            () => _svc.ValidateDateOfBirth(dob, today, 150, 18));

        Assert.Contains("18", ex.Message);
    }

    [Fact]
    public void ValidateDateOfBirth_DoesNotThrow_When_MinimumMemberAgeIsZero_AndAgeIsZero()
    {
        var today = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var dob = today.AddMonths(-6); // age=0, birthday not yet reached this year

        // MinimumMemberAge=0 means "no minimum" — a newborn-equivalent DOB must pass.
        _svc.ValidateDateOfBirth(dob, today, 150, 0);
    }
}
