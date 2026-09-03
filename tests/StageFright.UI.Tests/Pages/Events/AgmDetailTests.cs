using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Agm;
using StageFright.Reports.Rendering;
using StageFright.UI.Pages.Events;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Events;

/// <summary>
/// bUnit tests for AgmDetail — the scheduled-vs-recorded branch (FR-008), read-only
/// attendance/position rendering once recorded (FR-011, FR-016), the archive action (FR-017,
/// US5), the Print Attendance Report action (issue #302), the general-committee list box, and
/// the Print AGM Results action (issue #307).
/// </summary>
public class AgmDetailTests : LocalizedTestContext
{
    private readonly IAgmService _agmService = Substitute.For<IAgmService>();
    private readonly ICommitteeService _committeeService = Substitute.For<ICommitteeService>();
    private readonly IAgmAttendanceRepository _attendanceRepository = Substitute.For<IAgmAttendanceRepository>();
    private readonly IAgmAttendanceSheetService _agmAttendanceSheetService = Substitute.For<IAgmAttendanceSheetService>();
    private readonly IAgmAttendanceSheetPdfRenderer _agmAttendanceSheetPdfRenderer = Substitute.For<IAgmAttendanceSheetPdfRenderer>();
    private readonly IAgmResultsPdfRenderer _agmResultsPdfRenderer = Substitute.For<IAgmResultsPdfRenderer>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    private static readonly Guid AgmId = Guid.NewGuid();

    public AgmDetailTests()
    {
        Services.AddSingleton(_agmService);
        Services.AddSingleton(_committeeService);
        Services.AddSingleton(_attendanceRepository);
        Services.AddSingleton(_agmAttendanceSheetService);
        Services.AddSingleton(_agmAttendanceSheetPdfRenderer);
        Services.AddSingleton(_agmResultsPdfRenderer);
        Services.AddSingleton(_settingsService);

        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new SettingsEntity
            {
                Id = Guid.NewGuid(), OrganizationName = "Test Choir",
                AnnualFee = 50m, AttendanceFee = 10m,
                MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
                MinimumMemberAge = 0, SchemaVersion = "1.0.0",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
    }

    private static AnnualGeneralMeeting MakeAgm(string? notes = null, bool isRecorded = true) => new()
    {
        Id = AgmId, Date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), Notes = notes,
        IsRecorded = isRecorded, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
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

    // --- General committee members — list box, one row per name (issue #307) ---

    private static CommitteePositionRecord GeneralCommitteeMember(string firstName, string lastName)
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = firstName, LastName = lastName, StreetAddress = "1 St",
            JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        return new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = member.Id, Member = member,
            OfficeHolderTypeId = null, OfficeHolderType = null,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public void Renders_GeneralCommitteeMembers_AsSeparateListBoxRows_NotCommaJoined()
    {
        var positions = new List<CommitteePositionRecord>
        {
            GeneralCommitteeMember("Alice", "Anderson"),
            GeneralCommitteeMember("Bob", "Baker"),
            GeneralCommitteeMember("Carol", "Cooper")
        };

        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm());
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(positions);

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        var rows = cut.FindAll(".bordered-list-box-row");
        Assert.Equal(3, rows.Count);
        // SortableFullName is "Last, First" per-name, so a comma alone doesn't prove joining —
        // assert instead that no row's text spans more than one member (i.e. two names in one row).
        Assert.DoesNotContain(rows, r => r.TextContent.Contains("Anderson") && r.TextContent.Contains("Baker"));
        Assert.DoesNotContain(rows, r => r.TextContent.Contains("Baker") && r.TextContent.Contains("Cooper"));
        Assert.Contains(rows, r => r.TextContent.Contains("Anderson, Alice"));
        Assert.Contains(rows, r => r.TextContent.Contains("Baker, Bob"));
        Assert.Contains(rows, r => r.TextContent.Contains("Cooper, Carol"));
        Assert.Contains("General Committee Member", cut.Markup);
    }

    [Fact]
    public void Renders_SingleGeneralCommitteeMember_AsOneListBoxRow()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm());
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>())
            .Returns(new List<CommitteePositionRecord> { GeneralCommitteeMember("Alice", "Anderson") });

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        Assert.Single(cut.FindAll(".bordered-list-box-row"));
    }

    [Fact]
    public void NoGeneralCommitteeMembers_OmitsListBox()
    {
        var officeHolderType = new CommitteeOfficeHolderType
        {
            Id = Guid.NewGuid(), Name = "President", DisplayOrder = 0, IsBuiltIn = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = "Alice", LastName = "Test", StreetAddress = "1 St",
            JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
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

        Assert.Empty(cut.FindAll(".bordered-list-box"));
        Assert.DoesNotContain("General Committee Member", cut.Markup);
        Assert.Contains("President", cut.Markup);
    }

    [Fact]
    public void AgmNotFound_ShowsWarning_NotDetail()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns((AnnualGeneralMeeting?)null);

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        Assert.Contains("AGM not found", cut.Markup);
    }

    // --- Scheduled branch (FR-008) ---

    [Fact]
    public void ScheduledAgm_RendersDateAndNotesOnly_NoAttendanceOrPositions()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm(notes: "TBD venue", isRecorded: false));

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        Assert.Contains("15 March 2026", cut.Markup);
        Assert.Contains("TBD venue", cut.Markup);
        Assert.DoesNotContain("members attended", cut.Markup);
        Assert.DoesNotContain("Elected Positions", cut.Markup);
    }

    [Fact]
    public void ScheduledAgm_ShowsScheduledBadge()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm(isRecorded: false));

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        Assert.Contains("Scheduled", cut.Markup);
    }

    [Fact]
    public void RecordedAgm_ShowsRecordedBadge()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm(isRecorded: true));
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<CommitteePositionRecord>());

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        Assert.Contains("Recorded", cut.Markup);
    }

    [Fact]
    public void ScheduledAgm_RendersRecordButton_NavigatingToRecordRoute()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm(isRecorded: false));

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        var link = cut.Find("a.btn-primary");
        Assert.Equal($"/events/agm/{AgmId}/record", link.GetAttribute("href"));
    }

    // --- Print Attendance Report ---

    [Fact]
    public void PrintButton_Renders_OnceAgmLoads()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm());
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<CommitteePositionRecord>());

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        cut.Find("button[aria-label='Print attendance report']");
    }

    [Fact]
    public async Task ClickPrint_EmptyMembers_ShowsMessage_AndDoesNotRenderPdf()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm());
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<CommitteePositionRecord>());
        _agmAttendanceSheetService.GenerateAsync(AgmId, Arg.Any<CancellationToken>())
            .Returns(new AgmAttendanceSheetData { AgmDate = MakeAgm().Date, Members = Array.Empty<AgmAttendanceSheetMember>() });

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));
        await cut.Find("button[aria-label='Print attendance report']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("No attendance records found", cut.Markup);
        _agmAttendanceSheetPdfRenderer.DidNotReceive().Render(Arg.Any<AgmAttendanceSheetData>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ClickPrint_ServiceThrows_ShowsErrorMessage_AndDoesNotRenderPdf()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm());
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<CommitteePositionRecord>());
        _agmAttendanceSheetService.GenerateAsync(AgmId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AgmAttendanceSheetData>(new EntityNotFoundException("AnnualGeneralMeeting", AgmId, "GenerateAsync")));

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));
        await cut.Find("button[aria-label='Print attendance report']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("Unable to print", cut.Markup);
        _agmAttendanceSheetPdfRenderer.DidNotReceive().Render(Arg.Any<AgmAttendanceSheetData>(), Arg.Any<string>());
    }

    // --- Print AGM Results (issue #307) ---

    [Fact]
    public void ResultsPrintButton_Renders_OnceAgmLoads()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm());
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<CommitteePositionRecord>());

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));

        cut.Find("button[aria-label='Print AGM results report']");
    }

    [Fact]
    public async Task ClickPrintResults_SettingsServiceThrows_ShowsErrorMessage_AndDoesNotRenderPdf()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm());
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<CommitteePositionRecord>());
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SettingsEntity?>(new InvalidOperationException("boom")));

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));
        await cut.Find("button[aria-label='Print AGM results report']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("Unable to print AGM results report", cut.Markup);
        _agmResultsPdfRenderer.DidNotReceive().Render(Arg.Any<AgmResultsData>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ClickPrintResults_DoesNotTriggerAttendanceReportRenderer()
    {
        _agmService.GetByIdAsync(AgmId, Arg.Any<CancellationToken>()).Returns(MakeAgm());
        _attendanceRepository.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());
        _committeeService.GetByAgmAsync(AgmId, Arg.Any<CancellationToken>()).Returns(new List<CommitteePositionRecord>());
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SettingsEntity?>(new InvalidOperationException("boom")));

        var cut = Render<AgmDetail>(p => p.Add(x => x.Id, AgmId));
        await cut.Find("button[aria-label='Print AGM results report']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _agmAttendanceSheetPdfRenderer.DidNotReceive().Render(Arg.Any<AgmAttendanceSheetData>(), Arg.Any<string>());
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
