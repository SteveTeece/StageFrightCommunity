using Bunit;
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
/// bUnit tests for ContactEditor, the inline add/edit form on the Contacts tab (spec 032, US2):
/// field-level messages with no service call (FR-004/FR-008), add and edit raising OnSaved, a
/// service refusal shown in the editor banner, and Cancel.
/// </summary>
public class ContactEditorTests : LocalizedTestContext
{
    private readonly IExternalOrganisationContactService _service = Substitute.For<IExternalOrganisationContactService>();
    private readonly Guid _organisationId = Guid.NewGuid();
    private int _savedCount;
    private int _cancelledCount;

    private readonly ExternalOrganisationContact _existing = new()
    {
        Id = Guid.NewGuid(),
        Name = "Jo Bloggs",
        Role = "Venue manager",
        Phone = "02 9000 0000",
        Mobile = "0400 000 000",
        Email = "jo@riverside.example"
    };

    public ContactEditorTests()
    {
        Services.AddSingleton(_service);
        Services.AddScoped<ExternalOrganisationValidationService>();
    }

    private IStringLocalizer<OrganisationsResource> L =>
        Services.GetRequiredService<IStringLocalizer<OrganisationsResource>>();

    private IStringLocalizer<ValidationResource> V =>
        Services.GetRequiredService<IStringLocalizer<ValidationResource>>();

    private IRenderedComponent<ContactEditor> RenderEditor(ExternalOrganisationContact? contact = null) =>
        Render<ContactEditor>(p => p
            .Add(e => e.OrganisationId, _organisationId)
            .Add(e => e.Contact, contact)
            .Add(e => e.OnSaved, () => _savedCount++)
            .Add(e => e.OnCancelled, () => _cancelledCount++));

    private async Task AssertNothingSavedAsync()
    {
        await _service.DidNotReceive().AddAsync(Arg.Any<Guid>(), Arg.Any<SaveExternalOrganisationContactRequest>(), Arg.Any<CancellationToken>());
        await _service.DidNotReceive().UpdateAsync(Arg.Any<Guid>(), Arg.Any<SaveExternalOrganisationContactRequest>(), Arg.Any<CancellationToken>());
        Assert.Equal(0, _savedCount);
    }

    // ── Layout ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_RenderFiveEmptyFieldsWithLabels_When_AddingContact()
    {
        var cut = RenderEditor();

        foreach (var id in new[] { "contactName", "contactRole", "contactPhone", "contactMobile", "contactEmail" })
            Assert.Equal(string.Empty, cut.Find($"#{id}").GetAttribute("value") ?? string.Empty);
        Assert.Equal("email", cut.Find("#contactEmail").GetAttribute("type"));
        Assert.Contains(L["Organisations_Contacts_ColName"].Value, cut.Find("label[for=contactName]").TextContent);
        Assert.Contains(L["Organisations_Contacts_ColMobile"].Value, cut.Find("label[for=contactMobile]").TextContent);
        Assert.Equal(L["Organisations_Contacts_SaveButton"].Value, cut.Find("#saveContactButton").TextContent.Trim());
        Assert.Equal(L["Organisations_Contacts_CancelButton"].Value, cut.Find("#cancelContactButton").TextContent.Trim());
    }

    [Fact]
    public void Should_PrefillFields_When_EditingContact()
    {
        var cut = RenderEditor(_existing);

        Assert.Equal("Jo Bloggs", cut.Find("#contactName").GetAttribute("value"));
        Assert.Equal("Venue manager", cut.Find("#contactRole").GetAttribute("value"));
        Assert.Equal("02 9000 0000", cut.Find("#contactPhone").GetAttribute("value"));
        Assert.Equal("0400 000 000", cut.Find("#contactMobile").GetAttribute("value"));
        Assert.Equal("jo@riverside.example", cut.Find("#contactEmail").GetAttribute("value"));
    }

    // ── Field-level messages (FR-004 / FR-008) ───────────────────────────────────────────────

    [Fact]
    public async Task Should_ShowNameMessageAndNotSubmit_When_NameIsBlank()
    {
        var cut = RenderEditor();

        cut.Find("#contactName").Change("   ");
        await cut.Find("form").SubmitAsync();

        Assert.Contains("is-invalid", cut.Find("#contactName").ClassList);
        Assert.Equal(V["Validation_OrganisationContact_NameRequired"].Value, cut.Find(".invalid-feedback").TextContent.Trim());
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Should_ShowEmailMessageAndNotSubmit_When_EmailIsInvalid()
    {
        var cut = RenderEditor();

        cut.Find("#contactName").Change("Sam Lee");
        cut.Find("#contactEmail").Change("not-an-email");
        await cut.Find("form").SubmitAsync();

        Assert.Contains("is-invalid", cut.Find("#contactEmail").ClassList);
        Assert.DoesNotContain("is-invalid", cut.Find("#contactName").ClassList);
        Assert.Equal(V["Validation_OrganisationContact_EmailInvalid"].Value, cut.Find(".invalid-feedback").TextContent.Trim());
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Should_ClearFieldMessage_When_CorrectedAndResubmitted()
    {
        var cut = RenderEditor();
        await cut.Find("form").SubmitAsync();

        cut.Find("#contactName").Change("Sam Lee");
        await cut.Find("form").SubmitAsync();

        Assert.Empty(cut.FindAll(".invalid-feedback"));
        Assert.Equal(1, _savedCount);
    }

    // ── Save ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_AddContactAndRaiseOnSaved_When_NewContactIsValid()
    {
        var cut = RenderEditor();

        cut.Find("#contactName").Change("Sam Lee");
        cut.Find("#contactRole").Change("Marketing officer");
        cut.Find("#contactPhone").Change("02 9111 1111");
        cut.Find("#contactMobile").Change("0411 111 111");
        cut.Find("#contactEmail").Change("sam@riverside.example");
        await cut.Find("form").SubmitAsync();

        await _service.Received(1).AddAsync(_organisationId, Arg.Is<SaveExternalOrganisationContactRequest>(r =>
            r!.Name == "Sam Lee" &&
            r.Role == "Marketing officer" &&
            r.Phone == "02 9111 1111" &&
            r.Mobile == "0411 111 111" &&
            r.Email == "sam@riverside.example"), Arg.Any<CancellationToken>());
        Assert.Equal(1, _savedCount);
    }

    [Fact]
    public async Task Should_UpdateContactAndRaiseOnSaved_When_EditedContactIsValid()
    {
        var cut = RenderEditor(_existing);

        cut.Find("#contactRole").Change("Bookings");
        await cut.Find("form").SubmitAsync();

        await _service.Received(1).UpdateAsync(_existing.Id, Arg.Is<SaveExternalOrganisationContactRequest>(r =>
            r!.Name == "Jo Bloggs" && r.Role == "Bookings"), Arg.Any<CancellationToken>());
        await _service.DidNotReceive().AddAsync(Arg.Any<Guid>(), Arg.Any<SaveExternalOrganisationContactRequest>(), Arg.Any<CancellationToken>());
        Assert.Equal(1, _savedCount);
    }

    [Fact]
    public async Task Should_ShowRefusalAndNotRaiseOnSaved_When_ServiceRefuses()
    {
        _service.AddAsync(Arg.Any<Guid>(), Arg.Any<SaveExternalOrganisationContactRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException("Organisation is archived.", nameof(ExternalOrganisation), "AddAsync"));
        var cut = RenderEditor();

        cut.Find("#contactName").Change("Sam Lee");
        await cut.Find("form").SubmitAsync();

        Assert.Equal("Organisation is archived.", cut.Find("#contactEditorError").TextContent.Trim());
        Assert.Equal(0, _savedCount);
    }

    [Fact]
    public async Task Should_ShowUnexpectedError_When_ServiceFailsUnexpectedly()
    {
        _service.AddAsync(Arg.Any<Guid>(), Arg.Any<SaveExternalOrganisationContactRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DataAccessException("db down", nameof(ExternalOrganisationContact), "AddAsync"));
        var cut = RenderEditor();

        cut.Find("#contactName").Change("Sam Lee");
        await cut.Find("form").SubmitAsync();

        Assert.Equal(L["Organisations_Error_Unexpected"].Value, cut.Find("#contactEditorError").TextContent.Trim());
        Assert.Equal(0, _savedCount);
    }

    // ── Cancel ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_RaiseOnCancelledWithoutSaving_When_CancelClicked()
    {
        var cut = RenderEditor();

        cut.Find("#contactName").Change("Sam Lee");
        cut.Find("#cancelContactButton").Click();

        Assert.Equal(1, _cancelledCount);
        await AssertNothingSavedAsync();
    }
}
