using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Rehearsals;
using StageFright.UI.Pages.Rehearsals;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Rehearsals;

/// <summary>
/// bUnit tests for AttendanceGrid — member rendering, default checkbox state, save behavior, post-save lock.
/// </summary>
public class AttendanceGridTests : BunitContext
{
    private readonly IRehearsalService _rehearsalService = Substitute.For<IRehearsalService>();
    private readonly IAttendanceService _attendanceService = Substitute.For<IAttendanceService>();
    private readonly IMemberService _memberService = Substitute.For<IMemberService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    private static readonly Guid RehearsalId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly Rehearsal OpenRehearsal = new()
    {
        Id = RehearsalId,
        Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        Time = TimeSpan.FromHours(19),
        StoredAttendanceRate = null, // not yet recorded
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static readonly Rehearsal LockedRehearsal = new()
    {
        Id = RehearsalId,
        Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        Time = TimeSpan.FromHours(19),
        StoredAttendanceRate = 80m, // already recorded
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    public AttendanceGridTests()
    {
        Services.AddSingleton(_rehearsalService);
        Services.AddSingleton(_attendanceService);
        Services.AddSingleton(_memberService);
        Services.AddSingleton(_settingsService);

        // Default: open rehearsal (not yet recorded)
        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Rehearsal> { OpenRehearsal });

        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new SettingsEntity
            {
                Id = Guid.NewGuid(), OrganizationName = "Test",
                AnnualFee = 50m, AttendanceFee = 10m,
                MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
                MinimumMemberAge = 0, SchemaVersion = "1.0.0",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(new List<Member>
            {
                ActiveMember("Alice"),
                ActiveMember("Bob")
            });

        _memberService.GetByStatusAsync(MemberStatus.Inactive, Arg.Any<CancellationToken>())
            .Returns(new List<Member>());
    }

    [Fact]
    public void Renders_AllActiveMembers_InGrid()
    {
        var cut = RenderWithId();

        var rows = cut.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
    }

    [Fact]
    public void Default_MarkAsUnpaid_IsUnchecked()
    {
        var cut = RenderWithId();

        // All "Mark as unpaid" checkboxes should be unchecked by default
        var unpaidChecks = cut.FindAll("input[type=checkbox][id^='unpaid-']");
        Assert.All(unpaidChecks, cb => Assert.False(cb.HasAttribute("checked")));
    }

    [Fact]
    public void Default_Attended_IsUnchecked()
    {
        var cut = RenderWithId();

        var attendedChecks = cut.FindAll("input[type=checkbox][id^='attended-']");
        Assert.All(attendedChecks, cb => Assert.False(cb.HasAttribute("checked")));
    }

    [Fact]
    public void SaveButton_Renders()
    {
        var cut = RenderWithId();

        cut.Find("button.btn-primary");
    }

    [Fact]
    public async Task ClickSave_CallsAttendanceService_RecordBatchAsync()
    {
        var cut = RenderWithId();

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _attendanceService.Received(1).RecordBatchAsync(
            RehearsalId,
            Arg.Any<IReadOnlyList<AttendanceBatchItem>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void WhenAttendanceAlreadyRecorded_ShowsLockedMessage_AndNoSaveButton()
    {
        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Rehearsal> { LockedRehearsal });

        var cut = RenderWithId();

        Assert.Contains("immutable", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(cut.FindAll("button.btn-primary"));
    }

    // --- Helpers ---

    private IRenderedComponent<AttendanceGrid> RenderWithId() =>
        Render<AttendanceGrid>(p => p.Add(x => x.RehearsalId, RehearsalId));

    private static Member ActiveMember(string name) => new()
    {
        Id = Guid.NewGuid(), Name = name, StreetAddress = "1 St",
        Status = MemberStatus.Active, ActivateDate = DateTime.UtcNow.Date,
        JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
