using StageFright.Core.Modules.Localization.Resources;
using StageFright.Core.Modules.Organisations;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Organisations;

/// <summary>
/// Unit tests for ExternalOrganisationValidationService (FR-004): every length and email-format rule
/// for organisations and contacts yields a message keyed by its request property name, and blank
/// optional fields pass. The stub localizer echoes the key, so each assertion also pins the key used.
/// </summary>
public class ExternalOrganisationValidationServiceTests : TestBase
{
    private readonly ExternalOrganisationValidationService _svc =
        new(new StubStringLocalizer<ValidationResource>());

    private static SaveExternalOrganisationRequest ValidOrganisation() => new()
    {
        Name = "Riverside Hall",
        Address = "1 River Rd",
        Email = "bookings@riverside.example"
    };

    private static SaveExternalOrganisationContactRequest ValidContact() => new()
    {
        Name = "Sam Lee",
        Role = "Venue manager",
        Phone = "02 9000 0000",
        Mobile = "0400 000 000",
        Email = "sam@riverside.example"
    };

    /// <summary>A well-formed email address of exactly <paramref name="length"/> characters.</summary>
    private static string EmailOfLength(int length)
    {
        const string domain = "@b.co";
        return new string('a', length - domain.Length) + domain;
    }

    // ── Organisation: valid / blank optional fields ──────────────────────────────────────────

    [Fact]
    public void Should_ReturnNoErrors_When_OrganisationRequestIsFullyValid()
    {
        var errors = _svc.GetErrors(ValidOrganisation());

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ReturnNoErrors_When_OrganisationOptionalFieldsAreBlank(string? blank)
    {
        var request = ValidOrganisation();
        request.Address = blank;
        request.Email = blank;

        var errors = _svc.GetErrors(request);

        Assert.Empty(errors);
    }

    // ── Organisation: name ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ReturnNameRequiredError_When_OrganisationNameIsBlank(string name)
    {
        var request = ValidOrganisation();
        request.Name = name;

        var errors = _svc.GetErrors(request);

        Assert.Equal("Validation_Organisation_NameRequired", Assert.Single(errors, e => e.Key == "Name").Value);
        Assert.Single(errors);
    }

    [Fact]
    public void Should_ReturnNoErrors_When_OrganisationNameIsAtMaxLength()
    {
        var request = ValidOrganisation();
        request.Name = new string('n', 200);

        var errors = _svc.GetErrors(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnNameMaxLengthError_When_OrganisationNameExceedsMaxLength()
    {
        var request = ValidOrganisation();
        request.Name = new string('n', 201);

        var errors = _svc.GetErrors(request);

        Assert.Single(errors);
        Assert.Equal("Validation_Organisation_NameMaxLength", errors["Name"]);
    }

    [Fact]
    public void Should_ReturnNoErrors_When_OrganisationNameAtMaxLengthHasSurroundingWhitespace()
    {
        var request = ValidOrganisation();
        request.Name = "  " + new string('n', 200) + "  ";

        var errors = _svc.GetErrors(request);

        Assert.Empty(errors);
    }

    // ── Organisation: address ────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_ReturnNoErrors_When_OrganisationAddressIsAtMaxLength()
    {
        var request = ValidOrganisation();
        request.Address = new string('a', 1000);

        var errors = _svc.GetErrors(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnAddressMaxLengthError_When_OrganisationAddressExceedsMaxLength()
    {
        var request = ValidOrganisation();
        request.Address = new string('a', 1001);

        var errors = _svc.GetErrors(request);

        Assert.Single(errors);
        Assert.Equal("Validation_Organisation_AddressMaxLength", errors["Address"]);
    }

    // ── Organisation: email ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_ReturnNoErrors_When_OrganisationEmailIsAtMaxLength()
    {
        var request = ValidOrganisation();
        request.Email = EmailOfLength(254);

        var errors = _svc.GetErrors(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnEmailMaxLengthError_When_OrganisationEmailExceedsMaxLength()
    {
        var request = ValidOrganisation();
        request.Email = EmailOfLength(255);

        var errors = _svc.GetErrors(request);

        Assert.Single(errors);
        Assert.Equal("Validation_Organisation_EmailMaxLength", errors["Email"]);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-local.com")]
    [InlineData("no-dot@domain")]
    [InlineData("two@@signs.com")]
    [InlineData("has space@domain.com")]
    public void Should_ReturnEmailInvalidError_When_OrganisationEmailIsMalformed(string email)
    {
        var request = ValidOrganisation();
        request.Email = email;

        var errors = _svc.GetErrors(request);

        Assert.Single(errors);
        Assert.Equal("Validation_Organisation_EmailInvalid", errors["Email"]);
    }

    [Fact]
    public void Should_ReturnNoErrors_When_OrganisationEmailHasSurroundingWhitespace()
    {
        var request = ValidOrganisation();
        request.Email = "  bookings@riverside.example  ";

        var errors = _svc.GetErrors(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnErrorPerField_When_EveryOrganisationFieldIsInvalid()
    {
        var request = new SaveExternalOrganisationRequest
        {
            Name = " ",
            Address = new string('a', 1001),
            Email = "bad"
        };

        var errors = _svc.GetErrors(request);

        Assert.Equal(3, errors.Count);
        Assert.Equal("Validation_Organisation_NameRequired", errors["Name"]);
        Assert.Equal("Validation_Organisation_AddressMaxLength", errors["Address"]);
        Assert.Equal("Validation_Organisation_EmailInvalid", errors["Email"]);
    }

    // ── Contact: valid / blank optional fields ───────────────────────────────────────────────

    [Fact]
    public void Should_ReturnNoErrors_When_ContactRequestIsFullyValid()
    {
        var errors = _svc.GetErrors(ValidContact());

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ReturnNoErrors_When_ContactOptionalFieldsAreBlank(string? blank)
    {
        var request = ValidContact();
        request.Role = blank;
        request.Phone = blank;
        request.Mobile = blank;
        request.Email = blank;

        var errors = _svc.GetErrors(request);

        Assert.Empty(errors);
    }

    // ── Contact: name ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ReturnNameRequiredError_When_ContactNameIsBlank(string name)
    {
        var request = ValidContact();
        request.Name = name;

        var errors = _svc.GetErrors(request);

        Assert.Single(errors);
        Assert.Equal("Validation_OrganisationContact_NameRequired", errors["Name"]);
    }

    [Fact]
    public void Should_ReturnNoErrors_When_ContactNameIsAtMaxLength()
    {
        var request = ValidContact();
        request.Name = new string('n', 200);

        var errors = _svc.GetErrors(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnNameMaxLengthError_When_ContactNameExceedsMaxLength()
    {
        var request = ValidContact();
        request.Name = new string('n', 201);

        var errors = _svc.GetErrors(request);

        Assert.Single(errors);
        Assert.Equal("Validation_OrganisationContact_NameMaxLength", errors["Name"]);
    }

    // ── Contact: role / phone / mobile ───────────────────────────────────────────────────────

    [Fact]
    public void Should_ReturnNoErrors_When_ContactRoleIsAtMaxLength()
    {
        var request = ValidContact();
        request.Role = new string('r', 100);

        var errors = _svc.GetErrors(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnRoleMaxLengthError_When_ContactRoleExceedsMaxLength()
    {
        var request = ValidContact();
        request.Role = new string('r', 101);

        var errors = _svc.GetErrors(request);

        Assert.Single(errors);
        Assert.Equal("Validation_OrganisationContact_RoleMaxLength", errors["Role"]);
    }

    [Fact]
    public void Should_ReturnNoErrors_When_ContactPhoneIsAtMaxLength()
    {
        var request = ValidContact();
        request.Phone = new string('1', 50);

        var errors = _svc.GetErrors(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnPhoneMaxLengthError_When_ContactPhoneExceedsMaxLength()
    {
        var request = ValidContact();
        request.Phone = new string('1', 51);

        var errors = _svc.GetErrors(request);

        Assert.Single(errors);
        Assert.Equal("Validation_OrganisationContact_PhoneMaxLength", errors["Phone"]);
    }

    [Fact]
    public void Should_ReturnNoErrors_When_ContactMobileIsAtMaxLength()
    {
        var request = ValidContact();
        request.Mobile = new string('4', 50);

        var errors = _svc.GetErrors(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnMobileMaxLengthError_When_ContactMobileExceedsMaxLength()
    {
        var request = ValidContact();
        request.Mobile = new string('4', 51);

        var errors = _svc.GetErrors(request);

        Assert.Single(errors);
        Assert.Equal("Validation_OrganisationContact_MobileMaxLength", errors["Mobile"]);
    }

    // ── Contact: email ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_ReturnNoErrors_When_ContactEmailIsAtMaxLength()
    {
        var request = ValidContact();
        request.Email = EmailOfLength(254);

        var errors = _svc.GetErrors(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnEmailMaxLengthError_When_ContactEmailExceedsMaxLength()
    {
        var request = ValidContact();
        request.Email = EmailOfLength(255);

        var errors = _svc.GetErrors(request);

        Assert.Single(errors);
        Assert.Equal("Validation_OrganisationContact_EmailMaxLength", errors["Email"]);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-local.com")]
    [InlineData("no-dot@domain")]
    [InlineData("two@@signs.com")]
    [InlineData("has space@domain.com")]
    public void Should_ReturnEmailInvalidError_When_ContactEmailIsMalformed(string email)
    {
        var request = ValidContact();
        request.Email = email;

        var errors = _svc.GetErrors(request);

        Assert.Single(errors);
        Assert.Equal("Validation_OrganisationContact_EmailInvalid", errors["Email"]);
    }

    [Fact]
    public void Should_ReturnErrorPerField_When_EveryContactFieldIsInvalid()
    {
        var request = new SaveExternalOrganisationContactRequest
        {
            Name = "",
            Role = new string('r', 101),
            Phone = new string('1', 51),
            Mobile = new string('4', 51),
            Email = "bad"
        };

        var errors = _svc.GetErrors(request);

        Assert.Equal(5, errors.Count);
        Assert.Equal("Validation_OrganisationContact_NameRequired", errors["Name"]);
        Assert.Equal("Validation_OrganisationContact_RoleMaxLength", errors["Role"]);
        Assert.Equal("Validation_OrganisationContact_PhoneMaxLength", errors["Phone"]);
        Assert.Equal("Validation_OrganisationContact_MobileMaxLength", errors["Mobile"]);
        Assert.Equal("Validation_OrganisationContact_EmailInvalid", errors["Email"]);
    }
}
