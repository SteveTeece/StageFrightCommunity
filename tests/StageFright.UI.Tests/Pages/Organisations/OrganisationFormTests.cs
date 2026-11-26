using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Core.Modules.Organisations;
using StageFright.UI.Pages.Organisations;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Tests.Pages.Organisations;

/// <summary>
/// bUnit tests for OrganisationForm (spec 032, US1): save on the new route (FR-002), load and save on
/// the edit route (FR-005), field-level messages from the validation service with no service call
/// (FR-004), and the duplicate-name refusal shown in the form banner (FR-003).
/// </summary>
public class OrganisationFormTests : LocalizedTestContext
{
    private readonly IExternalOrganisationService _service = Substitute.For<IExternalOrganisationService>();
    private readonly Guid _createdId = Guid.NewGuid();

    private readonly ExternalOrganisation _existing = new()
    {
        Id = Guid.NewGuid(),
        Name = "Riverside Hall",
        Address = "1 River Rd\nRiverside NSW 2000",
        Email = "bookings@riverside.example",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public OrganisationFormTests()
    {
        Services.AddSingleton(_service);
        Services.AddScoped<ExternalOrganisationValidationService>();

        _service.CreateAsync(Arg.Any<SaveExternalOrganisationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => new ExternalOrganisation { Id = _createdId, Name = ci.Arg<SaveExternalOrganisationRequest>()!.Name });
        _service.GetByIdAsync(_existing.Id, Arg.Any<CancellationToken>()).Returns(_existing);
    }

    private IStringLocalizer<OrganisationsResource> L =>
        Services.GetRequiredService<IStringLocalizer<OrganisationsResource>>();

    private IStringLocalizer<ValidationResource> V =>
        Services.GetRequiredService<IStringLocalizer<ValidationResource>>();

    private string NavUri => Services.GetRequiredService<NavigationManager>().Uri;

    private IRenderedComponent<OrganisationForm> RenderEdit() =>
        Render<OrganisationForm>(p => p.Add(f => f.Id, _existing.Id));

    // ── New route (FR-002) ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_RenderEmptyFieldsWithNewHeading_When_OnNewRoute()
    {
        var cut = Render<OrganisationForm>();

        Assert.Equal(L["Organisations_Form_PageTitleNew"].Value, cut.Find("h1").TextContent.Trim());
        Assert.Equal(string.Empty, cut.Find("#organisationName").GetAttribute("value") ?? string.Empty);
        Assert.Equal("textarea", cut.Find("#organisationAddress").TagName.ToLowerInvariant());
        Assert.Equal("4", cut.Find("#organisationAddress").GetAttribute("rows"));
        Assert.Equal("email", cut.Find("#organisationEmail").GetAttribute("type"));
        cut.Find("#saveOrganisationButton");
        cut.Find("#cancelOrganisationButton");
    }

    [Fact]
    public async Task Should_CreateAndOpenDetail_When_NewFormSaved()
    {
        var cut = Render<OrganisationForm>();

        cut.Find("#organisationName").Change("City Council");
        cut.Find("#organisationAddress").Change("1 Civic Pl\nTown");
        cut.Find("#organisationEmail").Change("info@council.example");
        await cut.Find("form").SubmitAsync();

        await _service.Received(1).CreateAsync(Arg.Is<SaveExternalOrganisationRequest>(r =>
            r!.Name == "City Council" &&
            r.Address == "1 Civic Pl\nTown" &&
            r.Email == "info@council.example"), Arg.Any<CancellationToken>());
        Assert.EndsWith($"/organisations/{_createdId}", NavUri);
    }

    [Fact]
    public async Task Should_CreateWithNameOnly_When_OptionalFieldsLeftBlank()
    {
        var cut = Render<OrganisationForm>();

        cut.Find("#organisationName").Change("City Council");
        await cut.Find("form").SubmitAsync();

        await _service.Received(1).CreateAsync(Arg.Is<SaveExternalOrganisationRequest>(r =>
            r!.Name == "City Council"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Should_ReturnToList_When_CancelClickedOnNewRoute()
    {
        var cut = Render<OrganisationForm>();

        cut.Find("#cancelOrganisationButton").Click();

        Assert.EndsWith("/organisations", NavUri);
        _service.DidNotReceive().CreateAsync(Arg.Any<SaveExternalOrganisationRequest>(), Arg.Any<CancellationToken>());
    }

    // ── Edit route (FR-005) ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_LoadExistingValuesWithEditHeading_When_OnEditRoute()
    {
        var cut = RenderEdit();

        Assert.Equal(L["Organisations_Form_PageTitleEdit"].Value, cut.Find("h1").TextContent.Trim());
        Assert.Equal("Riverside Hall", cut.Find("#organisationName").GetAttribute("value"));
        Assert.Equal("1 River Rd\nRiverside NSW 2000", cut.Find("#organisationAddress").GetAttribute("value"));
        Assert.Equal("bookings@riverside.example", cut.Find("#organisationEmail").GetAttribute("value"));
    }

    [Fact]
    public async Task Should_UpdateAndOpenDetail_When_EditFormSaved()
    {
        var cut = RenderEdit();

        cut.Find("#organisationAddress").Change("2 New St\nRiverside NSW 2000");
        await cut.Find("form").SubmitAsync();

        await _service.Received(1).UpdateAsync(_existing.Id, Arg.Is<SaveExternalOrganisationRequest>(r =>
            r!.Name == "Riverside Hall" &&
            r.Address == "2 New St\nRiverside NSW 2000" &&
            r.Email == "bookings@riverside.example"), Arg.Any<CancellationToken>());
        await _service.DidNotReceive().CreateAsync(Arg.Any<SaveExternalOrganisationRequest>(), Arg.Any<CancellationToken>());
        Assert.EndsWith($"/organisations/{_existing.Id}", NavUri);
    }

    [Fact]
    public void Should_ReturnToDetailsTab_When_CancelClickedOnEditRoute()
    {
        var cut = RenderEdit();

        cut.Find("#cancelOrganisationButton").Click();

        Assert.EndsWith($"/organisations/{_existing.Id}", NavUri);
    }

    [Fact]
    public void Should_ShowNotFound_When_EditRouteIdIsUnknown()
    {
        var cut = Render<OrganisationForm>(p => p.Add(f => f.Id, Guid.NewGuid()));

        Assert.Contains(L["Organisations_Detail_NotFound"].Value, cut.Markup);
        Assert.Empty(cut.FindAll("form"));
    }

    // ── Field-level messages (FR-004) ────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_ShowEmailMessageNextToField_AndNotCallService_When_EmailInvalid()
    {
        var cut = Render<OrganisationForm>();

        cut.Find("#organisationName").Change("City Council");
        cut.Find("#organisationEmail").Change("not-an-email");
        await cut.Find("form").SubmitAsync();

        Assert.Contains("is-invalid", cut.Find("#organisationEmail").ClassList);
        Assert.Equal(V["Validation_Organisation_EmailInvalid"].Value,
            cut.Find("#organisationEmail + .invalid-feedback").TextContent.Trim());
        Assert.DoesNotContain("is-invalid", cut.Find("#organisationName").ClassList);
        await _service.DidNotReceive().CreateAsync(Arg.Any<SaveExternalOrganisationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ShowNameMessageNextToField_AndNotCallService_When_NameTooLong()
    {
        var cut = Render<OrganisationForm>();

        cut.Find("#organisationName").Change(new string('a', 201));
        await cut.Find("form").SubmitAsync();

        Assert.Contains("is-invalid", cut.Find("#organisationName").ClassList);
        Assert.Equal(V["Validation_Organisation_NameMaxLength"].Value,
            cut.Find("#organisationName + .invalid-feedback").TextContent.Trim());
        await _service.DidNotReceive().CreateAsync(Arg.Any<SaveExternalOrganisationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ShowNameRequiredMessage_AndNotCallService_When_NameBlank()
    {
        var cut = Render<OrganisationForm>();

        await cut.Find("form").SubmitAsync();

        Assert.Equal(V["Validation_Organisation_NameRequired"].Value,
            cut.Find("#organisationName + .invalid-feedback").TextContent.Trim());
        await _service.DidNotReceive().CreateAsync(Arg.Any<SaveExternalOrganisationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ShowAddressMessageNextToField_AndNotCallService_When_AddressTooLong()
    {
        var cut = RenderEdit();

        cut.Find("#organisationAddress").Change(new string('a', 1001));
        await cut.Find("form").SubmitAsync();

        Assert.Contains("is-invalid", cut.Find("#organisationAddress").ClassList);
        Assert.Equal(V["Validation_Organisation_AddressMaxLength"].Value,
            cut.Find("#organisationAddress + .invalid-feedback").TextContent.Trim());
        await _service.DidNotReceive().UpdateAsync(Arg.Any<Guid>(), Arg.Any<SaveExternalOrganisationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ClearFieldMessage_When_ResubmittedWithValidValue()
    {
        var cut = Render<OrganisationForm>();
        cut.Find("#organisationName").Change("City Council");
        cut.Find("#organisationEmail").Change("bad");
        await cut.Find("form").SubmitAsync();

        cut.Find("#organisationEmail").Change("info@council.example");
        await cut.Find("form").SubmitAsync();

        Assert.Empty(cut.FindAll(".invalid-feedback"));
        await _service.Received(1).CreateAsync(Arg.Any<SaveExternalOrganisationRequest>(), Arg.Any<CancellationToken>());
    }

    // ── Service refusals (FR-003) ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_ShowDuplicateMessage_When_ServiceRefusesDuplicateName()
    {
        var duplicate = V["Validation_Organisation_NameDuplicate"].Value;
        _service.CreateAsync(Arg.Any<SaveExternalOrganisationRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException(duplicate, nameof(ExternalOrganisation), "CreateAsync"));
        var cut = Render<OrganisationForm>();

        cut.Find("#organisationName").Change("city council");
        await cut.Find("form").SubmitAsync();

        var error = cut.Find("#organisationFormError");
        Assert.Contains("alert-danger", error.ClassList);
        Assert.Equal(duplicate, error.TextContent.Trim());
        Assert.DoesNotContain("/organisations/", NavUri);
    }

    [Fact]
    public async Task Should_ShowUnexpectedMessage_When_ServiceFailsUnexpectedly()
    {
        _service.UpdateAsync(Arg.Any<Guid>(), Arg.Any<SaveExternalOrganisationRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DataAccessException("db down", nameof(ExternalOrganisation), "UpdateAsync", _existing.Id));
        var cut = RenderEdit();

        await cut.Find("form").SubmitAsync();

        Assert.Equal(L["Organisations_Error_Unexpected"].Value, cut.Find("#organisationFormError").TextContent.Trim());
    }

    [Fact]
    public void Should_ShowNoErrorBanner_When_FormFirstRendered()
    {
        var cut = Render<OrganisationForm>();

        Assert.Empty(cut.FindAll("#organisationFormError"));
    }
}
