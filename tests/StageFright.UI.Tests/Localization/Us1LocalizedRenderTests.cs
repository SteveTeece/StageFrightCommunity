using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Plugins.Contracts;
using StageFright.UI.Layout;
using StageFright.UI.Modules.Members;
using StageFright.UI.Pages.Members;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Tests.Localization;

/// <summary>
/// FR-018 guard: the US1 slice renders its user-facing text from <see cref="IStringLocalizer{T}"/>
/// resources, not hardcoded English. Each test resolves the expected copy through the same
/// localizer the component uses and asserts it appears in the rendered markup — so re-wording a
/// <c>.resx</c> entry (or shipping a translation) changes the screen with no code change.
/// </summary>
public class Us1LocalizedRenderTests : LocalizedTestContext
{
    private readonly IMemberService _memberService = Substitute.For<IMemberService>();
    private readonly IMemberBalanceService _balanceService = Substitute.For<IMemberBalanceService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IDeviceThemePreferenceProvider _deviceTheme = Substitute.For<IDeviceThemePreferenceProvider>();

    private IStringLocalizer<MembersResource> Members => Services.GetRequiredService<IStringLocalizer<MembersResource>>();
    private IStringLocalizer<SharedResource> Shared => Services.GetRequiredService<IStringLocalizer<SharedResource>>();
    private IStringLocalizer<NavigationResource> Nav => Services.GetRequiredService<IStringLocalizer<NavigationResource>>();

    public Us1LocalizedRenderTests()
    {
        Services.AddSingleton(_memberService);
        Services.AddSingleton(_balanceService);
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(_deviceTheme);

        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _deviceTheme.GetPreference().Returns(PlatformThemePreference.Light);
        _memberService.GetByStatusAsync(Arg.Any<MemberStatus>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Member>)new List<Member>());
        _balanceService.GetAllMemberBalancesAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<MemberBalance>)new List<MemberBalance>());
    }

    [Fact]
    public void ShellLayout_RendersBrandTextFromNavigationResource()
    {
        var cut = Render<ShellLayout>();

        Assert.Contains(Nav["Nav_Sidebar_BrandText"].Value, cut.Markup);
        Assert.Contains(Nav["Nav_ThemeToggle_Label"].Value, cut.Markup);
    }

    [Fact]
    public void ShellLayout_RendersThemeNameViaLocalizeEnum()
    {
        var cut = Render<ShellLayout>();

        // Device pref is Light, so the shell shows the Light theme label from EnumsResource.
        Assert.Contains(Theme.Light.LocalizeEnum(), cut.Markup);
    }

    [Fact]
    public void ThemeProvider_RendersChildContent_AndCarriesNoLocalisedText()
    {
        var cut = Render<ThemeProvider>(p => p.AddChildContent("<span id=\"probe\">child</span>"));

        Assert.NotNull(cut.Find("#probe"));
        Assert.Contains("data-bs-theme", cut.Markup);
    }

    [Fact]
    public void MemberList_HeadingAndAddButton_ComeFromMembersResource()
    {
        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Member>)new List<Member>
            {
                new() { Id = Guid.NewGuid(), FirstName = "A", LastName = "B", StreetAddress = "1 St", Status = MemberStatus.Active, JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            });

        var cut = Render<MemberList>();

        Assert.Contains(Members["Members_List_Heading"].Value, cut.Markup);
        Assert.Contains(Members["Members_List_AddButton"].Value, cut.Markup);
        Assert.Contains(Members["Members_List_NameColumn"].Value, cut.Markup);
        Assert.Contains(Shared["Shared_Table_ActionsColumn"].Value, cut.Markup);
    }

    [Fact]
    public void MemberList_ActiveEmptyMessage_ComesFromMembersResource()
    {
        var cut = Render<MemberList>();

        Assert.Contains(Members["Members_List_EmptyActive"].Value, cut.Markup);
    }

    [Fact]
    public void MemberForm_HeadingAndActions_ComeFromResources()
    {
        var cut = Render<MemberForm>();

        Assert.Contains(Members["Members_Form_AddHeading"].Value, cut.Markup);
        Assert.Contains(Members["Members_Form_FirstNameLabel"].Value, cut.Markup);
        Assert.Contains(Shared["Shared_Action_Save"].Value, cut.Markup);
        Assert.Contains(Shared["Shared_Action_Cancel"].Value, cut.Markup);
    }

    [Fact]
    public void MembersTile_StatLabels_ComeFromResourcesAndLocalizeEnum()
    {
        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Member>)new List<Member>
            {
                new() { Id = Guid.NewGuid(), FirstName = "A", LastName = "B", StreetAddress = "1 St", Status = MemberStatus.Active, JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            });

        var cut = Render<MembersTile>();

        Assert.Contains(MemberStatus.Active.LocalizeEnum(), cut.Markup);
        Assert.Contains(MemberStatus.Inactive.LocalizeEnum(), cut.Markup);
        Assert.Contains(Members["Members_Tile_TotalLabel"].Value, cut.Markup);
        Assert.Contains(Members["Members_Tile_NoOutstandingFees"].Value, cut.Markup);
    }
}
