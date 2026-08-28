using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Core.Modules.Members;
using StageFright.Core.Tests.Fixtures;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.Core.Tests.Modules.Members;

/// <summary>
/// Unit tests for MemberValidationService — Name/StreetAddress/Email checks plus the
/// DOB/age-range validation delegated to AgeCalculationService, for both Create and Update requests.
/// </summary>
public class MemberValidationServiceTests : TestBase
{
    private readonly MemberValidationService _svc =
        new(new AgeCalculationService(), new StubStringLocalizer<ValidationResource>());

    private static SettingsEntity MakeSettings(int maxAgeRangeYears = 150, int minimumMemberAge = 0) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationName = "Test Org",
        AnnualFee = 50m,
        AttendanceFee = 5m,
        MembershipRenewalMonth = 1,
        MaxAgeRangeYears = maxAgeRangeYears,
        MinimumMemberAge = minimumMemberAge
    };

    private static CreateMemberRequest MakeCreateRequest(DateTime? dob) => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        StreetAddress = "1 Main St",
        JoinDate = DateTime.UtcNow.Date,
        DateOfBirth = dob
    };

    private static UpdateMemberRequest MakeUpdateRequest(DateTime? dob) => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        StreetAddress = "1 Main St",
        JoinDate = DateTime.UtcNow.Date,
        DateOfBirth = dob
    };

    [Fact]
    public void Validate_CreateRequest_Throws_WhenDobViolatesMaxAgeRange()
    {
        var settings = MakeSettings(maxAgeRangeYears: 100);
        var dob = DateTime.UtcNow.Date.AddYears(-101);
        var request = MakeCreateRequest(dob);

        Assert.Throws<ValidationException>(() => _svc.Validate(request, settings));
    }

    [Fact]
    public void Validate_CreateRequest_Throws_WhenDobViolatesMinimumAge()
    {
        var settings = MakeSettings(minimumMemberAge: 18);
        var dob = DateTime.UtcNow.Date.AddYears(-10);
        var request = MakeCreateRequest(dob);

        Assert.Throws<ValidationException>(() => _svc.Validate(request, settings));
    }

    [Fact]
    public void Validate_CreateRequest_DoesNotThrow_WhenDobIsNull()
    {
        var settings = MakeSettings(minimumMemberAge: 18);
        var request = MakeCreateRequest(null);

        _svc.Validate(request, settings); // must not throw — DOB is optional
    }

    [Fact]
    public void Validate_UpdateRequest_Throws_WhenDobViolatesMaxAgeRange()
    {
        var settings = MakeSettings(maxAgeRangeYears: 100);
        var dob = DateTime.UtcNow.Date.AddYears(-101);
        var request = MakeUpdateRequest(dob);

        Assert.Throws<ValidationException>(() => _svc.Validate(request, settings));
    }

    [Fact]
    public void Validate_UpdateRequest_DoesNotThrow_WhenDobIsValid()
    {
        var settings = MakeSettings(maxAgeRangeYears: 150, minimumMemberAge: 18);
        var dob = DateTime.UtcNow.Date.AddYears(-40);
        var request = MakeUpdateRequest(dob);

        _svc.Validate(request, settings); // must not throw
    }

    [Fact]
    public void Validate_CreateRequest_Throws_WhenFirstNameIsBlank()
    {
        var settings = MakeSettings();
        var request = MakeCreateRequest(null) with { FirstName = "   " };

        Assert.Throws<ValidationException>(() => _svc.Validate(request, settings));
    }

    [Fact]
    public void Validate_CreateRequest_Throws_WhenLastNameIsBlank()
    {
        var settings = MakeSettings();
        var request = MakeCreateRequest(null) with { LastName = "   " };

        Assert.Throws<ValidationException>(() => _svc.Validate(request, settings));
    }

    [Fact]
    public void Validate_CreateRequest_Throws_WhenFirstNameExceedsMaxLength()
    {
        var settings = MakeSettings();
        var request = MakeCreateRequest(null) with { FirstName = new string('A', 101) };

        Assert.Throws<ValidationException>(() => _svc.Validate(request, settings));
    }

    [Fact]
    public void Validate_CreateRequest_Throws_WhenLastNameExceedsMaxLength()
    {
        var settings = MakeSettings();
        var request = MakeCreateRequest(null) with { LastName = new string('A', 101) };

        Assert.Throws<ValidationException>(() => _svc.Validate(request, settings));
    }

    [Fact]
    public void Validate_UpdateRequest_Throws_WhenFirstNameIsBlank()
    {
        var settings = MakeSettings();
        var request = MakeUpdateRequest(null) with { FirstName = "   " };

        Assert.Throws<ValidationException>(() => _svc.Validate(request, settings));
    }

    [Fact]
    public void Validate_UpdateRequest_Throws_WhenLastNameIsBlank()
    {
        var settings = MakeSettings();
        var request = MakeUpdateRequest(null) with { LastName = "   " };

        Assert.Throws<ValidationException>(() => _svc.Validate(request, settings));
    }

    [Fact]
    public void Validate_UpdateRequest_Throws_WhenFirstNameExceedsMaxLength()
    {
        var settings = MakeSettings();
        var request = MakeUpdateRequest(null) with { FirstName = new string('A', 101) };

        Assert.Throws<ValidationException>(() => _svc.Validate(request, settings));
    }

    [Fact]
    public void Validate_UpdateRequest_Throws_WhenLastNameExceedsMaxLength()
    {
        var settings = MakeSettings();
        var request = MakeUpdateRequest(null) with { LastName = new string('A', 101) };

        Assert.Throws<ValidationException>(() => _svc.Validate(request, settings));
    }
}
