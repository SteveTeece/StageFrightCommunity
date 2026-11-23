using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.UI.Pages.Organisations;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Tests.Pages.Organisations;

/// <summary>
/// bUnit tests for OrganisationList (spec 032, US1): the grid's name, email and contact-count
/// columns (FR-001), the empty register, the name search that filters as typed and the
/// "show archived" switch (FR-007).
/// </summary>
public class OrganisationListTests : LocalizedTestContext
{
    private readonly IExternalOrganisationService _service = Substitute.For<IExternalOrganisationService>();

    private readonly ExternalOrganisation _council = Organisation("City Council", "info@council.example", contacts: 2);
    private readonly ExternalOrganisation _hall = Organisation("Riverside Hall", "bookings@riverside.example", contacts: 0);
    private readonly ExternalOrganisation _archived = Organisation("Old Printers", "sales@printers.example", contacts: 1, archived: true);

    public OrganisationListTests()
    {
        Services.AddSingleton(_service);

        _service.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ExternalOrganisation>)[_council, _hall]);
        _service.GetArchivedAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ExternalOrganisation>)[_archived]);
    }

    private IStringLocalizer<OrganisationsResource> L =>
        Services.GetRequiredService<IStringLocalizer<OrganisationsResource>>();

    private static ExternalOrganisation Organisation(string name, string email, int contacts, bool archived = false)
    {
        var organisation = new ExternalOrganisation
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            IsDeleted = archived,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        for (var i = 0; i < contacts; i++)
            organisation.Contacts.Add(new ExternalOrganisationContact
            {
                Id = Guid.NewGuid(), ExternalOrganisationId = organisation.Id, Name = $"Contact {i}"
            });
        return organisation;
    }

    private static List<string> CellTexts(IRenderedComponent<OrganisationList> cut) =>
        cut.FindAll("td").Select(td => td.TextContent.Trim()).ToList();

    [Fact]
    public void Should_ShowHeadingAndColumnHeaders_When_Rendered()
    {
        var cut = Render<OrganisationList>();

        Assert.Equal(L["Organisations_List_Heading"].Value, cut.Find("h1").TextContent.Trim());
        Assert.Contains(L["Organisations_List_ColName"].Value, cut.Markup);
        Assert.Contains(L["Organisations_List_ColEmail"].Value, cut.Markup);
        Assert.Contains(L["Organisations_List_ColContacts"].Value, cut.Markup);
    }

    [Fact]
    public void Should_ShowNameEmailAndContactCount_When_ActiveOrganisationsExist()
    {
        var cut = Render<OrganisationList>();

        var cells = CellTexts(cut);
        var councilRow = cells.IndexOf("City Council");
        Assert.True(councilRow >= 0);
        Assert.Equal("info@council.example", cells[councilRow + 1]);
        Assert.Equal("2", cells[councilRow + 2]);

        var hallRow = cells.IndexOf("Riverside Hall");
        Assert.True(hallRow >= 0);
        Assert.Equal("bookings@riverside.example", cells[hallRow + 1]);
        Assert.Equal("0", cells[hallRow + 2]);
    }

    [Fact]
    public void Should_HideArchivedOrganisations_When_SwitchIsOff()
    {
        var cut = Render<OrganisationList>();

        Assert.Equal("false", cut.Find("[role=switch]").GetAttribute("aria-checked"));
        Assert.DoesNotContain("Old Printers", cut.Markup);
        Assert.Empty(cut.FindAll(".badge"));
        _service.DidNotReceive().GetArchivedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Should_RevealArchivedRowsWithBadge_When_ShowArchivedSwitchTurnedOn()
    {
        var cut = Render<OrganisationList>();

        cut.Find("[role=switch]").Click();

        Assert.Equal("true", cut.Find("[role=switch]").GetAttribute("aria-checked"));
        Assert.Contains("City Council", cut.Markup);
        Assert.Contains("Old Printers", cut.Markup);
        var badge = Assert.Single(cut.FindAll(".badge"));
        Assert.Equal(L["Organisations_List_ArchivedBadge"].Value, badge.TextContent.Trim());
    }

    [Fact]
    public void Should_HideArchivedRowsAgain_When_SwitchTurnedOff()
    {
        var cut = Render<OrganisationList>();

        cut.Find("[role=switch]").Click();
        cut.Find("[role=switch]").Click();

        Assert.Contains("City Council", cut.Markup);
        Assert.DoesNotContain("Old Printers", cut.Markup);
    }

    [Fact]
    public void Should_ShowLabelForSwitch_When_Rendered()
    {
        var cut = Render<OrganisationList>();

        Assert.Equal(L["Organisations_List_ShowArchivedLabel"].Value,
            cut.Find("label[for=showArchivedSwitch]").TextContent.Trim());
    }

    [Fact]
    public void Should_FilterRowsAsTyped_When_PartOfNameEntered()
    {
        var cut = Render<OrganisationList>();

        cut.Find("#organisationSearch").Input("RIVER");

        Assert.Contains("Riverside Hall", cut.Markup);
        Assert.DoesNotContain("City Council", cut.Markup);
    }

    [Fact]
    public void Should_IgnoreSurroundingSpaces_When_SearchTermHasThem()
    {
        var cut = Render<OrganisationList>();

        cut.Find("#organisationSearch").Input("  council  ");

        Assert.Contains("City Council", cut.Markup);
        Assert.DoesNotContain("Riverside Hall", cut.Markup);
    }

    [Fact]
    public void Should_ShowEveryRow_When_SearchTermIsBlank()
    {
        var cut = Render<OrganisationList>();

        cut.Find("#organisationSearch").Input("river");
        cut.Find("#organisationSearch").Input("   ");

        Assert.Contains("Riverside Hall", cut.Markup);
        Assert.Contains("City Council", cut.Markup);
    }

    [Fact]
    public void Should_FilterArchivedRowsToo_When_SwitchOnAndSearchEntered()
    {
        var cut = Render<OrganisationList>();

        cut.Find("[role=switch]").Click();
        cut.Find("#organisationSearch").Input("print");

        Assert.Contains("Old Printers", cut.Markup);
        Assert.DoesNotContain("City Council", cut.Markup);
    }

    [Fact]
    public void Should_ShowNoMatchMessage_When_SearchMatchesNothing()
    {
        var cut = Render<OrganisationList>();

        cut.Find("#organisationSearch").Input("zzz");

        Assert.Contains(L["Organisations_List_NoMatch"].Value, cut.Markup);
        Assert.DoesNotContain(L["Organisations_List_Empty"].Value, cut.Markup);
        Assert.Empty(cut.FindAll("td"));
    }

    [Fact]
    public void Should_ShowEmptyStateAndAddAction_When_RegisterIsEmpty()
    {
        _service.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ExternalOrganisation>)[]);

        var cut = Render<OrganisationList>();

        Assert.Contains(L["Organisations_List_Empty"].Value, cut.Markup);
        Assert.Equal(L["Organisations_List_AddButton"].Value, cut.Find("#addOrganisationButton").TextContent.Trim());
        Assert.Empty(cut.FindAll("td"));
    }

    [Fact]
    public void Should_NavigateToNewForm_When_AddButtonClicked()
    {
        var cut = Render<OrganisationList>();

        cut.Find("#addOrganisationButton").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/organisations/new", nav.Uri);
    }

    [Fact]
    public async Task Should_NavigateToDetail_When_OrganisationNameClicked()
    {
        var cut = Render<OrganisationList>();

        await cut.Find($"#organisationLink-{_hall.Id}").ClickAsync(new MouseEventArgs());

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith($"/organisations/{_hall.Id}", nav.Uri);
    }
}
