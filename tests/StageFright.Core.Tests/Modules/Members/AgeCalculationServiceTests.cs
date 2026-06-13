using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Members;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Members;

/// <summary>
/// Unit tests for AgeCalculationService — age algorithm (FR-002a examples) and DOB validation.
/// </summary>
public class AgeCalculationServiceTests : TestBase
{
    private readonly AgeCalculationService _svc = new();

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
}
