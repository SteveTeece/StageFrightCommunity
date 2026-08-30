using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Agm;
using StageFright.UI.Pages.Events;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Events;

/// <summary>
/// bUnit tests for RecordAgm — loading an existing scheduled AGM by id, its not-found/
/// already-recorded/future-date guards, attendance/office-holder/general-committee selection,
/// FR-008 one-member-one-slot guard, save behavior, and post-save redirect.
/// </summary>
public class RecordAgmTests : LocalizedTestContext
{
    private readonly IAgmService _agmService = Substitute.For<IAgmService>();
    private readonly IMemberService _memberService = Substitute.For<IMemberService>();
    private readonly ICommitteeOfficeHolderTypeService _officeHolderTypeService = Substitute.For<ICommitteeOfficeHolderTypeService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    private static readonly Member Alice = ActiveMember("Alice");
    private static readonly Member Bob = ActiveMember("Bob");
    private static readonly Guid PresidentTypeId = Guid.NewGuid();
    private static readonly Guid SavedAgmId = Guid.NewGuid();

    public RecordAgmTests()
    {
        Services.AddSingleton(_agmService);
        Services.AddSingleton(_memberService);
        Services.AddSingleton(_officeHolderTypeService);
        Services.AddSingleton(_settingsService);

        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { Alice, Bob });

        _officeHolderTypeService.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CommitteeOfficeHolderType>
            {
                new() { Id = PresidentTypeId, Name = "President", DisplayOrder = 0, IsBuiltIn = true,
                         CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            });

        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new SettingsEntity
            {
                Id = Guid.NewGuid(), OrganizationName = "Test", AnnualFee = 50m, AttendanceFee = 5m,
                MembershipRenewalMonth = 1, GeneralCommitteeSeatCountTarget = 5,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        _agmService.GetByIdAsync(SavedAgmId, Arg.Any<CancellationToken>())
            .Returns(new AnnualGeneralMeeting
            {
                Id = SavedAgmId, Date = DateTime.Today, IsRecorded = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        _agmService.RecordAsync(SavedAgmId, Arg.Any<RecordAgmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AnnualGeneralMeeting
            {
                Id = SavedAgmId, Date = DateTime.Today, IsRecorded = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
    }

    private IRenderedComponent<RecordAgm> RenderForSavedAgm() =>
        Render<RecordAgm>(p => p.Add(x => x.Id, SavedAgmId));

    [Fact]
    public void AgmNotFound_ShowsGuardMessage()
    {
        var unknownId = Guid.NewGuid();
        _agmService.GetByIdAsync(unknownId, Arg.Any<CancellationToken>()).Returns((AnnualGeneralMeeting?)null);

        var cut = Render<RecordAgm>(p => p.Add(x => x.Id, unknownId));

        Assert.Contains("AGM not found", cut.Markup);
    }

    [Fact]
    public void AlreadyRecordedAgm_ShowsGuardMessage()
    {
        var recordedId = Guid.NewGuid();
        _agmService.GetByIdAsync(recordedId, Arg.Any<CancellationToken>())
            .Returns(new AnnualGeneralMeeting
            {
                Id = recordedId, Date = DateTime.Today, IsRecorded = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        var cut = Render<RecordAgm>(p => p.Add(x => x.Id, recordedId));

        Assert.Contains("already been recorded", cut.Markup);
    }

    [Fact]
    public void FutureDatedAgm_ShowsGuardMessage()
    {
        var futureId = Guid.NewGuid();
        _agmService.GetByIdAsync(futureId, Arg.Any<CancellationToken>())
            .Returns(new AnnualGeneralMeeting
            {
                Id = futureId, Date = DateTime.Today.AddDays(5), IsRecorded = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        var cut = Render<RecordAgm>(p => p.Add(x => x.Id, futureId));

        Assert.Contains("date has not yet arrived", cut.Markup);
    }

    [Fact]
    public void Renders_AttendanceGrid_WithActiveMembers()
    {
        var cut = RenderForSavedAgm();

        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
    }

    [Fact]
    public void Renders_OfficeHolderDropdown_ForEachActiveType()
    {
        var cut = RenderForSavedAgm();

        var select = cut.Find($"#office-{PresidentTypeId}");
        Assert.Contains("Alice", select.TextContent);
        Assert.Contains("Bob", select.TextContent);
    }

    [Fact]
    public void Renders_GeneralCommitteeCheckbox_ForEachActiveMember()
    {
        var cut = RenderForSavedAgm();

        cut.Find($"#general-{Alice.Id}");
        cut.Find($"#general-{Bob.Id}");
    }

    [Fact]
    public void SelectingSameMember_AsOfficeHolderAndGeneralCommittee_ShowsValidationError_OnSave()
    {
        var cut = RenderForSavedAgm();

        cut.Find($"#office-{PresidentTypeId}").Change(Alice.Id.ToString());
        cut.Find($"#general-{Alice.Id}").Change(true);
        cut.Find("button.btn-primary").Click();

        var alert = cut.Find(".alert-danger");
        Assert.Contains("more than one committee assignment", alert.TextContent);
    }

    [Fact]
    public async Task Save_DoesNotCallRecordAsync_WhenMemberDuplicated()
    {
        var cut = RenderForSavedAgm();

        cut.Find($"#office-{PresidentTypeId}").Change(Alice.Id.ToString());
        cut.Find($"#general-{Alice.Id}").Change(true);
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _agmService.DidNotReceive().RecordAsync(Arg.Any<Guid>(), Arg.Any<RecordAgmRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_CallsRecordAsync_WithSelectedAttendanceOfficeHoldersAndGeneralCommittee()
    {
        var cut = RenderForSavedAgm();

        cut.Find($"#agm-attended-{Alice.Id}").Change(true);
        cut.Find($"#office-{PresidentTypeId}").Change(Alice.Id.ToString());
        cut.Find($"#general-{Bob.Id}").Change(true);

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _agmService.Received(1).RecordAsync(
            SavedAgmId,
            Arg.Is<RecordAgmRequest>(r =>
                r!.AttendedMemberIds.Contains(Alice.Id) &&
                r.AllActiveMemberIds.Count == 2 &&
                r.OfficeHolderAssignments[PresidentTypeId] == Alice.Id &&
                r.GeneralCommitteeMemberIds.Contains(Bob.Id)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_NavigatesToAgmDetail_ForSavedAgm()
    {
        var cut = RenderForSavedAgm();

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"/events/agm/{SavedAgmId}", nav.Uri);
    }

    private static Member ActiveMember(string name) => new()
    {
        Id = Guid.NewGuid(), FirstName = name, LastName = "Test", StreetAddress = "1 St",
        Status = MemberStatus.Active, ActivateDate = DateTime.UtcNow.Date,
        JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
