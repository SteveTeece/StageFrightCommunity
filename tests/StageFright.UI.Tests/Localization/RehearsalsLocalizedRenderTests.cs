using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Rehearsals;
using StageFright.Reports.Rendering;
using StageFright.UI.Pages.Rehearsals;
using StageFright.UI.Resources.Strings;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Localization;

/// <summary>
/// FR-018 guard for the Rehearsals module (US2, T032): every user-facing string on
/// <see cref="RehearsalList"/>, <see cref="RehearsalForm"/> and <see cref="AttendanceGrid"/>
/// is rendered from <see cref="IStringLocalizer{T}"/> resources, not hardcoded English. Each
/// test resolves the expected copy through the same localizer the component uses and asserts
/// it appears in the rendered markup, so re-wording a <c>.resx</c> entry changes the screen
/// with no code change.
/// </summary>
public class RehearsalsLocalizedRenderTests : LocalizedTestContext
{
    private readonly IRehearsalService _rehearsalService = Substitute.For<IRehearsalService>();
    private readonly IAttendanceService _attendanceService = Substitute.For<IAttendanceService>();
    private readonly IAttendanceRollService _attendanceRollService = Substitute.For<IAttendanceRollService>();
    private readonly IAttendanceRollPdfRenderer _attendanceRollPdfRenderer = Substitute.For<IAttendanceRollPdfRenderer>();
    private readonly IMemberService _memberService = Substitute.For<IMemberService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    private IStringLocalizer<RehearsalsResource> L => Services.GetRequiredService<IStringLocalizer<RehearsalsResource>>();
    private IStringLocalizer<SharedResource> Shared => Services.GetRequiredService<IStringLocalizer<SharedResource>>();
    private ILocalizer Loc => Services.GetRequiredService<ILocalizer>();

    private static readonly DateTime Today = DateTime.Today;

    public RehearsalsLocalizedRenderTests()
    {
        Services.AddSingleton(_rehearsalService);
        Services.AddSingleton(_attendanceService);
        Services.AddSingleton(_attendanceRollService);
        Services.AddSingleton(_attendanceRollPdfRenderer);
        Services.AddSingleton(_memberService);
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(Substitute.For<NavigationManager>());

        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Rehearsal>());
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(ASettings());
        _memberService.GetByStatusAsync(Arg.Any<MemberStatus>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Member>)new List<Member>());
    }

    [Fact]
    public void RehearsalList_ChromeAndEmptyState_ComeFromResources()
    {
        var cut = Render<RehearsalList>();

        Assert.Contains(L["Rehearsals_List_Heading"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_List_ScheduleButton"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_List_SearchPlaceholder"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_List_EmptyNone"].Value, cut.Markup);
    }

    [Fact]
    public void RehearsalList_GridColumnsAndActions_ComeFromResources()
    {
        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Rehearsal> { ARehearsal(Today) });

        var cut = Render<RehearsalList>();

        Assert.Contains(L["Rehearsals_List_DateColumn"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_List_NotesColumn"].Value, cut.Markup);
        Assert.Contains(Shared["Shared_Table_ActionsColumn"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_List_RecordAttendanceButton"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_List_PrintRollButton"].Value, cut.Markup);
        cut.Find($"button[aria-label='{Loc.Get<RehearsalsResource>("Rehearsals_List_PrintRollAriaLabel", Today.ToString("d MMM yyyy"))}']");
    }

    [Fact]
    public void RehearsalForm_HeadingLabelsAndActions_ComeFromResources()
    {
        var cut = Render<RehearsalForm>();

        Assert.Contains(L["Rehearsals_Form_Heading"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_Form_DateLabel"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_Form_TimeLabel"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_Form_NotesLabel"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_Form_ScheduleButton"].Value, cut.Markup);
        Assert.Contains(Shared["Shared_Action_Cancel"].Value, cut.Markup);
    }

    [Fact]
    public void AttendanceGrid_NotFound_MessageAndBackButton_ComeFromResources()
    {
        var cut = Render<AttendanceGrid>(p => p.Add(x => x.RehearsalId, Guid.NewGuid()));

        Assert.Contains(L["Rehearsals_Attendance_NotFound"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_Attendance_BackButton"].Value, cut.Markup);
    }

    [Fact]
    public void AttendanceGrid_RecordMode_HeadersAndActions_ComeFromResources()
    {
        var rehearsal = ARehearsal(Today);
        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Rehearsal> { rehearsal });
        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Member>)new List<Member> { AMember() });

        var cut = Render<AttendanceGrid>(p => p.Add(x => x.RehearsalId, rehearsal.Id));

        Assert.Contains(L["Rehearsals_Attendance_HeadingRecord"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_Attendance_AttendedColumn"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_Attendance_MemberColumn"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_Attendance_FeeColumn"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_Attendance_SaveButton"].Value, cut.Markup);
        Assert.Contains(L["Rehearsals_Attendance_SelectAllLabel"].Value, cut.Markup);
    }

    private static Rehearsal ARehearsal(DateTime date) => new()
    {
        Id = Guid.NewGuid(),
        Date = date,
        Time = TimeSpan.FromHours(19),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Member AMember() => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "A",
        LastName = "B",
        StreetAddress = "1 St",
        Status = MemberStatus.Active,
        JoinDate = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static SettingsEntity ASettings() => new()
    {
        Id = Guid.NewGuid(),
        OrganizationName = "Test Choir",
        AnnualFee = 50m,
        AttendanceFee = 25m,
        MembershipRenewalMonth = 1,
        MaxAgeRangeYears = 150,
        MinimumMemberAge = 0,
        SchemaVersion = "1.0.0",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
