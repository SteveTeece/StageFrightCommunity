using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Agm;
using StageFright.UI.Pages.Events;

namespace StageFright.UI.Tests.Pages.Events;

/// <summary>
/// bUnit tests for RecordSpecialElection — outgoing/incoming member selection, replacement-date
/// input, and submit calling IAgmService.RecordSpecialElectionAsync (FR-026–FR-028).
/// </summary>
public class RecordSpecialElectionTests : BunitContext
{
    private readonly IAgmService _agmService = Substitute.For<IAgmService>();
    private readonly ICommitteeService _committeeService = Substitute.For<ICommitteeService>();
    private readonly IMemberService _memberService = Substitute.For<IMemberService>();

    private static readonly Guid TermId = Guid.NewGuid();
    private static readonly Guid OutgoingPositionId = Guid.NewGuid();
    private static readonly Guid OutgoingMemberId = Guid.NewGuid();
    private static readonly Guid IncomingMemberId = Guid.NewGuid();
    private static readonly Guid PresidentTypeId = Guid.NewGuid();

    public RecordSpecialElectionTests()
    {
        Services.AddSingleton(_agmService);
        Services.AddSingleton(_committeeService);
        Services.AddSingleton(_memberService);

        var outgoingMember = ActiveMember("Alice", OutgoingMemberId);
        var incomingMember = ActiveMember("Bob", IncomingMemberId);
        var officeHolderType = new CommitteeOfficeHolderType
        {
            Id = PresidentTypeId, Name = "President", DisplayOrder = 0, IsBuiltIn = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var outgoingPosition = new CommitteePositionRecord
        {
            Id = OutgoingPositionId, MemberId = OutgoingMemberId, Member = outgoingMember,
            CommitteeTermId = TermId, OfficeHolderTypeId = PresidentTypeId, OfficeHolderType = officeHolderType,
            StartDate = DateTime.UtcNow.AddMonths(-3), EndDate = null,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        _committeeService.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CommitteePositionRecord> { outgoingPosition });

        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { outgoingMember, incomingMember });

        _agmService.RecordSpecialElectionAsync(Arg.Any<RecordSpecialElectionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CommitteePositionRecord
            {
                Id = Guid.NewGuid(), MemberId = IncomingMemberId, CommitteeTermId = TermId,
                OfficeHolderTypeId = PresidentTypeId, StartDate = DateTime.UtcNow, EndDate = null,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
    }

    [Fact]
    public void Renders_OutgoingPositionDropdown_WithCurrentPositions()
    {
        var cut = Render<RecordSpecialElection>();

        var select = cut.Find("#outgoingPosition");
        Assert.Contains("President", select.TextContent);
        Assert.Contains("Alice", select.TextContent);
    }

    [Fact]
    public void Renders_IncomingMemberDropdown_WithActiveMembers()
    {
        var cut = Render<RecordSpecialElection>();

        var select = cut.Find("#incomingMember");
        Assert.Contains("Alice", select.TextContent);
        Assert.Contains("Bob", select.TextContent);
    }

    [Fact]
    public void Renders_ReplacementDateField()
    {
        var cut = Render<RecordSpecialElection>();

        cut.Find("#replacementDate");
    }

    [Fact]
    public void Save_ShowsValidationError_WhenNoSelectionMade()
    {
        var cut = Render<RecordSpecialElection>();

        cut.Find("button.btn-primary").Click();

        var alert = cut.Find(".alert-danger");
        Assert.Contains("Select the position", alert.TextContent);
    }

    [Fact]
    public async Task Save_CallsRecordSpecialElectionAsync_WithSelectedOutgoingIncomingAndDate()
    {
        var cut = Render<RecordSpecialElection>();

        cut.Find("#outgoingPosition").Change(OutgoingPositionId.ToString());
        cut.Find("#incomingMember").Change(IncomingMemberId.ToString());
        var replacementDate = DateTime.Today.AddDays(-1);
        cut.Find("#replacementDate").Change(replacementDate.ToString("yyyy-MM-dd"));

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _agmService.Received(1).RecordSpecialElectionAsync(
            Arg.Is<RecordSpecialElectionRequest>(r =>
                r!.OutgoingPositionRecordId == OutgoingPositionId &&
                r.IncomingMemberId == IncomingMemberId &&
                r.ReplacementDate.Date == replacementDate.Date),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_NavigatesToPastAgms_AfterSuccess()
    {
        var cut = Render<RecordSpecialElection>();
        cut.Find("#outgoingPosition").Change(OutgoingPositionId.ToString());
        cut.Find("#incomingMember").Change(IncomingMemberId.ToString());

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/events/agm", nav.Uri);
    }

    [Fact]
    public void NoCurrentPositions_ShowsWarning_NotForm()
    {
        _committeeService.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CommitteePositionRecord>());

        var cut = Render<RecordSpecialElection>();

        Assert.Contains("No current committee positions found", cut.Markup);
        Assert.Empty(cut.FindAll("#outgoingPosition"));
    }

    private static Member ActiveMember(string name, Guid id) => new()
    {
        Id = id, FirstName = name, LastName = "Test", StreetAddress = "1 St",
        Status = MemberStatus.Active, ActivateDate = DateTime.UtcNow.Date,
        JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
