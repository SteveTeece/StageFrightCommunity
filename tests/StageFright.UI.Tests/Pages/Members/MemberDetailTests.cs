using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.UI.Pages.Members;

namespace StageFright.UI.Tests.Pages.Members;

/// <summary>
/// bUnit tests for MemberDetail — committee badge ARIA attribute and empty-history rendering.
/// </summary>
public class MemberDetailTests : BunitContext
{
    private readonly IMemberService _memberService = Substitute.For<IMemberService>();
    private readonly ICommitteeService _committeeService = Substitute.For<ICommitteeService>();

    private readonly Guid _memberId = Guid.NewGuid();
    private readonly int _currentYear = DateTime.UtcNow.Year;

    public MemberDetailTests()
    {
        Services.AddSingleton(_memberService);
        Services.AddSingleton(_committeeService);

        _memberService.GetByIdAsync(_memberId, Arg.Any<CancellationToken>())
            .Returns(new Member
            {
                Id = _memberId, Name = "Alice Smith",
                StreetAddress = "1 Test St", Status = MemberStatus.Active,
                JoinDate = DateTime.UtcNow, ActivateDate = DateTime.UtcNow.Date,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
    }

    [Fact]
    public async Task CurrentYearCommitteeRecord_RendersAriaBadge_WithCurrentYearLabel()
    {
        _committeeService.GetHistoryAsync(_memberId, Arg.Any<CancellationToken>())
            .Returns(new List<CommitteeMembership>
            {
                new()
                {
                    Id = Guid.NewGuid(), MemberId = _memberId,
                    Year = _currentYear, Position = "President",
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            });

        var cut = Render<MemberDetail>(p => p.Add(m => m.Id, _memberId));

        var badge = cut.Find("[role=status][aria-label='Current year']");
        Assert.NotNull(badge);
    }

    [Fact]
    public async Task CurrentYearRecord_IsWrapped_InStrongElement()
    {
        _committeeService.GetHistoryAsync(_memberId, Arg.Any<CancellationToken>())
            .Returns(new List<CommitteeMembership>
            {
                new()
                {
                    Id = Guid.NewGuid(), MemberId = _memberId,
                    Year = _currentYear, Position = "Treasurer",
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            });

        var cut = Render<MemberDetail>(p => p.Add(m => m.Id, _memberId));

        // Current year entry must be inside a <strong> element
        var strong = cut.Find("strong");
        Assert.Contains(_currentYear.ToString(), strong.TextContent);
        Assert.Contains("Treasurer", strong.TextContent);
    }

    [Fact]
    public async Task NoCommitteeHistory_DoesNotRender_HistorySection()
    {
        _committeeService.GetHistoryAsync(_memberId, Arg.Any<CancellationToken>())
            .Returns(new List<CommitteeMembership>());

        var cut = Render<MemberDetail>(p => p.Add(m => m.Id, _memberId));

        // No section with aria-label "Committee History"
        Assert.Empty(cut.FindAll("[aria-label='Committee History']"));
        Assert.Empty(cut.FindAll("section"));
    }

    [Fact]
    public async Task HistoricalRecord_NotCurrentYear_RendersAsPlainSpan_NotStrong()
    {
        var lastYear = _currentYear - 1;
        _committeeService.GetHistoryAsync(_memberId, Arg.Any<CancellationToken>())
            .Returns(new List<CommitteeMembership>
            {
                new()
                {
                    Id = Guid.NewGuid(), MemberId = _memberId,
                    Year = lastYear, Position = "Secretary",
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            });

        var cut = Render<MemberDetail>(p => p.Add(m => m.Id, _memberId));

        // Historical entry is in a <span>, not <strong>
        var spans = cut.FindAll("section span");
        Assert.Contains(spans, s => s.TextContent.Contains(lastYear.ToString()));
    }
}
