using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.UI.Pages.Organisations;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Tests.Pages.Organisations;

/// <summary>
/// bUnit tests for OrganisationDetail, the Details tab (spec 032, US1): the details card with a
/// multi-line address, the tab strip, and Archive / Restore (FR-006), including a restore refused
/// because another active organisation now uses the name.
/// </summary>
public class OrganisationDetailTests : LocalizedTestContext
{
    private readonly IExternalOrganisationService _service = Substitute.For<IExternalOrganisationService>();
    private readonly Guid _id = Guid.NewGuid();

    public OrganisationDetailTests()
    {
        Services.AddSingleton(_service);
        _service.GetByIdAsync(_id, Arg.Any<CancellationToken>()).Returns(Organisation(archived: false));
    }

    private IStringLocalizer<OrganisationsResource> L =>
        Services.GetRequiredService<IStringLocalizer<OrganisationsResource>>();

    private ExternalOrganisation Organisation(bool archived) => new()
    {
        Id = _id,
        Name = "Riverside Hall",
        Address = "1 River Rd\nRiverside NSW 2000",
        Email = "bookings@riverside.example",
        IsDeleted = archived,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private IRenderedComponent<OrganisationDetail> RenderDetail() =>
        Render<OrganisationDetail>(p => p.Add(d => d.Id, _id));

    // ── Details card ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_RenderNameAndDetailsWithMultiLineAddress_When_OrganisationExists()
    {
        var cut = RenderDetail();

        Assert.Equal("Riverside Hall", cut.Find("h1").TextContent.Trim());
        var address = cut.Find("#organisationDetailAddress");
        Assert.Equal("1 River Rd\nRiverside NSW 2000", address.TextContent);
        Assert.Contains("white-space: pre-line", address.GetAttribute("style"));
        Assert.Equal("bookings@riverside.example", cut.Find("#organisationDetailEmail").TextContent.Trim());
        Assert.Contains(L["Organisations_Detail_AddressLabel"].Value, cut.Markup);
        Assert.Contains(L["Organisations_Detail_EmailLabel"].Value, cut.Markup);
    }

    [Fact]
    public void Should_OmitAddressAndEmail_When_OrganisationHasNeither()
    {
        _service.GetByIdAsync(_id, Arg.Any<CancellationToken>())
            .Returns(new ExternalOrganisation { Id = _id, Name = "Riverside Hall" });

        var cut = RenderDetail();

        Assert.Empty(cut.FindAll("#organisationDetailAddress"));
        Assert.Empty(cut.FindAll("#organisationDetailEmail"));
    }

    [Fact]
    public void Should_MarkDetailsTabActive_When_Rendered()
    {
        var cut = RenderDetail();

        var details = cut.Find("#organisationTabDetails");
        Assert.Contains("active", details.ClassList);
        Assert.Equal("page", details.GetAttribute("aria-current"));
        Assert.DoesNotContain("active", cut.Find("#organisationTabContacts").ClassList);
    }

    [Fact]
    public void Should_ShowEditAndArchiveButNotRestore_When_OrganisationIsActive()
    {
        var cut = RenderDetail();

        Assert.Equal(L["Organisations_Detail_EditButton"].Value, cut.Find("#editOrganisationButton").TextContent.Trim());
        Assert.Equal(L["Organisations_Detail_ArchiveButton"].Value, cut.Find("#archiveOrganisationButton").TextContent.Trim());
        Assert.Empty(cut.FindAll("#restoreOrganisationButton"));
        Assert.DoesNotContain(L["Organisations_Detail_ArchivedBadge"].Value, cut.Markup);
    }

    [Fact]
    public void Should_ShowRestoreAndBadgeOnly_When_OrganisationIsArchived()
    {
        _service.GetByIdAsync(_id, Arg.Any<CancellationToken>()).Returns(Organisation(archived: true));

        var cut = RenderDetail();

        Assert.Equal(L["Organisations_Detail_RestoreButton"].Value, cut.Find("#restoreOrganisationButton").TextContent.Trim());
        Assert.Empty(cut.FindAll("#archiveOrganisationButton"));
        Assert.Empty(cut.FindAll("#editOrganisationButton"));
        Assert.Equal(L["Organisations_Detail_ArchivedBadge"].Value, cut.Find(".badge").TextContent.Trim());
    }

    [Fact]
    public void Should_ShowNotFound_When_IdIsUnknown()
    {
        var cut = Render<OrganisationDetail>(p => p.Add(d => d.Id, Guid.NewGuid()));

        Assert.Contains(L["Organisations_Detail_NotFound"].Value, cut.Markup);
        Assert.Empty(cut.FindAll("#organisationTabDetails"));
    }

    [Fact]
    public void Should_NavigateToEditForm_When_EditClicked()
    {
        var cut = RenderDetail();

        cut.Find("#editOrganisationButton").Click();

        Assert.EndsWith($"/organisations/{_id}/edit", Services.GetRequiredService<NavigationManager>().Uri);
    }

    // ── Archive and restore (FR-006) ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_ArchiveAndShowRestore_When_ArchiveClicked()
    {
        _service.GetByIdAsync(_id, Arg.Any<CancellationToken>())
            .Returns(Organisation(archived: false), Organisation(archived: true));
        var cut = RenderDetail();

        await cut.Find("#archiveOrganisationButton").ClickAsync(new MouseEventArgs());

        await _service.Received(1).ArchiveAsync(_id, Arg.Any<CancellationToken>());
        cut.Find("#restoreOrganisationButton");
        Assert.Empty(cut.FindAll("#archiveOrganisationButton"));
    }

    [Fact]
    public async Task Should_RestoreAndShowArchive_When_RestoreClicked()
    {
        _service.GetByIdAsync(_id, Arg.Any<CancellationToken>())
            .Returns(Organisation(archived: true), Organisation(archived: false));
        var cut = RenderDetail();

        await cut.Find("#restoreOrganisationButton").ClickAsync(new MouseEventArgs());

        await _service.Received(1).RestoreAsync(_id, Arg.Any<CancellationToken>());
        cut.Find("#archiveOrganisationButton");
        Assert.Empty(cut.FindAll("#organisationDetailError"));
    }

    [Fact]
    public async Task Should_ShowDuplicateMessage_When_RestoreRefusedBecauseNameTaken()
    {
        var duplicate = Services.GetRequiredService<IStringLocalizer<ValidationResource>>()["Validation_Organisation_NameDuplicate"].Value;
        _service.GetByIdAsync(_id, Arg.Any<CancellationToken>()).Returns(Organisation(archived: true));
        _service.RestoreAsync(_id, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException(duplicate, nameof(ExternalOrganisation), "RestoreAsync", _id));
        var cut = RenderDetail();

        await cut.Find("#restoreOrganisationButton").ClickAsync(new MouseEventArgs());

        var error = cut.Find("#organisationDetailError");
        Assert.Contains("alert-danger", error.ClassList);
        Assert.Equal(duplicate, error.TextContent.Trim());
        cut.Find("#restoreOrganisationButton");
    }

    [Fact]
    public async Task Should_ShowUnexpectedMessage_When_ArchiveFailsUnexpectedly()
    {
        _service.ArchiveAsync(_id, Arg.Any<CancellationToken>())
            .ThrowsAsync(new DataAccessException("db down", nameof(ExternalOrganisation), "ArchiveAsync", _id));
        var cut = RenderDetail();

        await cut.Find("#archiveOrganisationButton").ClickAsync(new MouseEventArgs());

        Assert.Equal(L["Organisations_Error_Unexpected"].Value, cut.Find("#organisationDetailError").TextContent.Trim());
        cut.Find("#archiveOrganisationButton");
    }
}
