using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.UI.Pages.Events;

namespace StageFright.UI.Tests.Pages.Events;

/// <summary>
/// bUnit tests for AgmDetail — read-only attendance/position rendering (FR-011, FR-016, from
/// US1/T047) plus the archive action (FR-017, US5).
/// </summary>
public class AgmDetailTests : BunitContext
{
    private readonly IAgmService _agmService = Substitute.For<IAgmService>();
    private readonly ICommitteeService _committeeService = Substitute.For<ICommitteeService>();
    private readonly IAgmAttendanceRepository _attendanceRepository = Substitute.For<IAgmAttendanceRepository>();

    private static readonly Guid AgmId = Guid.NewGuid();

    public AgmDetailTests()
    {
        Services.AddSingleton(_agmService);
        Services.AddSingleton(_committeeService);
        Services.AddSingleton(_attendanceRepository);
    }

    private static AnnualGeneralMeeting MakeAgm(string? notes = null) => new()
    {
        Id = AgmId, Date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), Notes = notes,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    // --- Base read-only rendering (US1/T047) ---

    [Fact]
    public void Renders_MeetingDate()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm());
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<CommitteePositionRecord>());

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        Assert.Contains("15 March 2026", cut.Markup);
    }

    [Fact]
    public void Renders_Notes_WhenPresent()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm(notes: "Held at the community hall."));
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<CommitteePositionRecord>());

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        Assert.Contains("Held at the community hall.", cut.Markup);
    }

    [Fact]
    public void Renders_AttendanceCount()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm());
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>
        {
            new() { Id = Guid.NewGuid(), AnnualGeneralMeetingId = AgmId, MemberId = Guid.NewGuid(), Attended = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), AnnualGeneralMeetingId = AgmId, MemberId = Guid.NewGuid(), Attended = false, CreatedAt = DateTime.UtcNow }
        });
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<CommitteePositionRecord>());

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        Assert.Contains("1 of 2 members attended", cut.Markup);
    }

    [Fact]
    public void Renders_ElectedPositions_WithOfficeHolderTypeNameAndMember()
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = "Alice", LastName = "Test", StreetAddress = "1 St",
            JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var officeHolderType = new CommitteeOfficeHolderType
        {
            Id = Guid.NewGuid(), Name = "President", DisplayOrder = 0, IsBuiltIn = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var position = new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = member.Id, Member = member,
            OfficeHolderTypeId = officeHolderType.Id, OfficeHolderType = officeHolderType,
            StartDate = DateTime.UtcNow, EndDate = null, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm());
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<CommitteePositionRecord> { position });

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        Assert.Contains("President", cut.Markup);
        Assert.Contains("Alice", cut.Markup);
    }

    [Fact]
    public void Renders_MultipleHoldersForSamePosition_WithDatesAndOrderedByStartDate()
    {
        // Discovered during manual verification: a term-scoped AGM detail view (GetByAgmAsync
        // joins through the term the AGM started) can include a later special election's
        // replacement holder for the same office-holder type. Without dates, that reads as two
        // people simultaneously holding the one office — FR-029 requires the dated distinction.
        var officeHolderType = new CommitteeOfficeHolderType
        {
            Id = Guid.NewGuid(), Name = "President", DisplayOrder = 0, IsBuiltIn = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var outgoingMember = new Member
        {
            Id = Guid.NewGuid(), FirstName = "Alice", LastName = "Anderson", StreetAddress = "1 St",
            JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var incomingMember = new Member
        {
            Id = Guid.NewGuid(), FirstName = "Carol", LastName = "Cooper", StreetAddress = "3 St",
            JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var outgoing = new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = outgoingMember.Id, Member = outgoingMember,
            OfficeHolderTypeId = officeHolderType.Id, OfficeHolderType = officeHolderType,
            StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var incoming = new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = incomingMember.Id, Member = incomingMember,
            OfficeHolderTypeId = officeHolderType.Id, OfficeHolderType = officeHolderType,
            StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = null,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm());
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>())
            .Returns(new List<CommitteePositionRecord> { outgoing, incoming });

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        var presidentItem = cut.FindAll("li").Single(li => li.TextContent.Contains("President"));
        Assert.True(
            presidentItem.TextContent.IndexOf("Alice", StringComparison.Ordinal) <
            presidentItem.TextContent.IndexOf("Carol", StringComparison.Ordinal),
            "Outgoing holder (earlier StartDate) must appear before the incoming holder.");
        Assert.Contains("present", presidentItem.TextContent);
        Assert.DoesNotContain("Alice, Carol", presidentItem.TextContent);
    }

    [Fact]
    public void AgmNotFound_ShowsWarning_NotDetail()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns((AnnualGeneralMeeting?)null);

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        Assert.Contains("AGM not found", cut.Markup);
    }

    // --- Archive action (US5) ---

    [Fact]
    public void Renders_ArchiveButton()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm());
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<CommitteePositionRecord>());

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        cut.Find("button.btn-outline-danger");
    }

    [Fact]
    public async Task ClickingArchive_CallsAgmServiceArchiveAsync()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm());
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<CommitteePositionRecord>());

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));
        await cut.Find("button.btn-outline-danger").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _agmService.Received(1).ArchiveAsync(AgmId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClickingArchive_NavigatesBackToPastAgmsList()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm());
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<CommitteePositionRecord>());

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));
        await cut.Find("button.btn-outline-danger").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/events/agm", nav.Uri);
    }
}
