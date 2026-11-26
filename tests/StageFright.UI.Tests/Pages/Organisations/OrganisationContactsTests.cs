using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Organisations;
using StageFright.UI.Pages.Organisations;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Tests.Pages.Organisations;

/// <summary>
/// bUnit tests for OrganisationContacts, the Contacts tab (spec 032, US2): the grid's five columns
/// (FR-008), Remove taking the row out (FR-009), the inline ContactEditor opening for add and edit,
/// and the add / edit / remove actions hidden while the organisation is archived.
/// </summary>
public class OrganisationContactsTests : LocalizedTestContext
{
    private readonly IExternalOrganisationService _organisations = Substitute.For<IExternalOrganisationService>();
    private readonly IExternalOrganisationContactService _contacts = Substitute.For<IExternalOrganisationContactService>();
    private readonly Guid _id = Guid.NewGuid();

    private readonly ExternalOrganisationContact _jo = Contact("Jo Bloggs", "Venue manager", "02 9000 0000", "0400 000 000", "jo@riverside.example");
    private readonly ExternalOrganisationContact _sam = Contact("Sam Lee", "Marketing officer", "02 9111 1111", "0411 111 111", "sam@riverside.example");

    public OrganisationContactsTests()
    {
        Services.AddSingleton(_organisations);
        Services.AddSingleton(_contacts);
        Services.AddScoped<ExternalOrganisationValidationService>();
        _organisations.GetByIdAsync(_id, Arg.Any<CancellationToken>()).Returns(Organisation(false, _jo, _sam));
    }

    private IStringLocalizer<OrganisationsResource> L =>
        Services.GetRequiredService<IStringLocalizer<OrganisationsResource>>();

    private static ExternalOrganisationContact Contact(string name, string role, string phone, string mobile, string email) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Role = role,
        Phone = phone,
        Mobile = mobile,
        Email = email,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private ExternalOrganisation Organisation(bool archived, params ExternalOrganisationContact[] contacts) => new()
    {
        Id = _id,
        Name = "Riverside Hall",
        IsDeleted = archived,
        Contacts = contacts.ToList(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private IRenderedComponent<OrganisationContacts> RenderContacts() =>
        Render<OrganisationContacts>(p => p.Add(c => c.Id, _id));

    private static List<string> CellTexts(IRenderedComponent<OrganisationContacts> cut) =>
        cut.FindAll("td").Select(td => td.TextContent.Trim()).ToList();

    // ── Grid (FR-008) ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_ListNameRolePhoneMobileAndEmail_When_OrganisationHasContacts()
    {
        var cut = RenderContacts();

        var headers = cut.FindAll("th").Select(th => th.TextContent.Trim()).ToList();
        foreach (var key in new[] { "ColName", "ColRole", "ColPhone", "ColMobile", "ColEmail" })
            Assert.Contains(L[$"Organisations_Contacts_{key}"].Value, headers);

        var cells = CellTexts(cut);
        foreach (var value in new[] { "Jo Bloggs", "Venue manager", "02 9000 0000", "0400 000 000", "jo@riverside.example", "Sam Lee" })
            Assert.Contains(value, cells);
    }

    [Fact]
    public void Should_ShowEmptyState_When_OrganisationHasNoContacts()
    {
        _organisations.GetByIdAsync(_id, Arg.Any<CancellationToken>()).Returns(Organisation(false));

        var cut = RenderContacts();

        Assert.Contains(L["Organisations_Contacts_Empty"].Value, cut.Markup);
        Assert.Empty(cut.FindAll("td"));
        cut.Find("#addContactButton");
    }

    [Fact]
    public void Should_ShowNameAndMarkContactsTabActive_When_Rendered()
    {
        var cut = RenderContacts();

        Assert.Equal("Riverside Hall", cut.Find("h1").TextContent.Trim());
        var contacts = cut.Find("#organisationTabContacts");
        Assert.Contains("active", contacts.ClassList);
        Assert.Equal("page", contacts.GetAttribute("aria-current"));
        Assert.DoesNotContain("active", cut.Find("#organisationTabDetails").ClassList);
    }

    [Fact]
    public void Should_ShowNotFound_When_IdIsUnknown()
    {
        var cut = Render<OrganisationContacts>(p => p.Add(c => c.Id, Guid.NewGuid()));

        Assert.Contains(L["Organisations_Detail_NotFound"].Value, cut.Markup);
        Assert.Empty(cut.FindAll("#organisationTabContacts"));
    }

    // ── Remove (FR-009) ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_RemoveRowFromGrid_When_RemoveClicked()
    {
        _organisations.GetByIdAsync(_id, Arg.Any<CancellationToken>())
            .Returns(Organisation(false, _jo, _sam), Organisation(false, _sam));
        var cut = RenderContacts();

        cut.Find($"#removeContact-{_jo.Id}").Click();

        _contacts.Received(1).RemoveAsync(_jo.Id, Arg.Any<CancellationToken>());
        var cells = CellTexts(cut);
        Assert.DoesNotContain("Jo Bloggs", cells);
        Assert.Contains("Sam Lee", cells);
    }

    [Fact]
    public void Should_ShowErrorAndKeepRow_When_RemoveRefused()
    {
        _contacts.RemoveAsync(_jo.Id, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException("Organisation is archived.", nameof(ExternalOrganisationContact), "RemoveAsync", _jo.Id));
        var cut = RenderContacts();

        cut.Find($"#removeContact-{_jo.Id}").Click();

        Assert.Equal("Organisation is archived.", cut.Find("#organisationContactsError").TextContent.Trim());
        Assert.Contains("Jo Bloggs", CellTexts(cut));
    }

    [Fact]
    public void Should_ShowUnexpectedError_When_RemoveFailsUnexpectedly()
    {
        _contacts.RemoveAsync(_jo.Id, Arg.Any<CancellationToken>())
            .ThrowsAsync(new DataAccessException("db down", nameof(ExternalOrganisationContact), "RemoveAsync", _jo.Id));
        var cut = RenderContacts();

        cut.Find($"#removeContact-{_jo.Id}").Click();

        Assert.Equal(L["Organisations_Error_Unexpected"].Value, cut.Find("#organisationContactsError").TextContent.Trim());
    }

    // ── Archived organisation ────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_HideAddEditAndRemove_When_OrganisationIsArchived()
    {
        _organisations.GetByIdAsync(_id, Arg.Any<CancellationToken>()).Returns(Organisation(true, _jo));

        var cut = RenderContacts();

        Assert.Contains("Jo Bloggs", CellTexts(cut));
        Assert.Empty(cut.FindAll("#addContactButton"));
        Assert.Empty(cut.FindAll($"#editContact-{_jo.Id}"));
        Assert.Empty(cut.FindAll($"#removeContact-{_jo.Id}"));
        Assert.Equal(L["Organisations_Detail_ArchivedBadge"].Value, cut.Find(".badge").TextContent.Trim());
    }

    // ── Inline editor ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_NotShowEditor_When_FirstRendered()
    {
        var cut = RenderContacts();

        Assert.Empty(cut.FindComponents<ContactEditor>());
    }

    [Fact]
    public void Should_OpenEmptyEditor_When_AddClicked()
    {
        var cut = RenderContacts();

        cut.Find("#addContactButton").Click();

        var editor = cut.FindComponent<ContactEditor>();
        Assert.Null(editor.Instance.Contact);
        Assert.Equal(_id, editor.Instance.OrganisationId);
        Assert.Equal(string.Empty, cut.Find("#contactName").GetAttribute("value") ?? string.Empty);
    }

    [Fact]
    public void Should_OpenEditorWithContact_When_EditClicked()
    {
        var cut = RenderContacts();

        cut.Find($"#editContact-{_sam.Id}").Click();

        Assert.Same(_sam, cut.FindComponent<ContactEditor>().Instance.Contact);
        Assert.Equal("Sam Lee", cut.Find("#contactName").GetAttribute("value"));
    }

    [Fact]
    public async Task Should_CloseEditorAndShowNewRow_When_ContactAdded()
    {
        var added = Contact("Alex Kim", "Treasurer", "", "", "");
        _organisations.GetByIdAsync(_id, Arg.Any<CancellationToken>())
            .Returns(Organisation(false, _jo), Organisation(false, _jo, added));
        var cut = RenderContacts();

        cut.Find("#addContactButton").Click();
        cut.Find("#contactName").Change("Alex Kim");
        await cut.Find("form").SubmitAsync();

        await _contacts.Received(1).AddAsync(_id, Arg.Is<SaveExternalOrganisationContactRequest>(r => r!.Name == "Alex Kim"), Arg.Any<CancellationToken>());
        Assert.Empty(cut.FindComponents<ContactEditor>());
        Assert.Contains("Alex Kim", CellTexts(cut));
    }

    [Fact]
    public void Should_CloseEditor_When_Cancelled()
    {
        var cut = RenderContacts();

        cut.Find("#addContactButton").Click();
        cut.Find("#cancelContactButton").Click();

        Assert.Empty(cut.FindComponents<ContactEditor>());
    }
}
