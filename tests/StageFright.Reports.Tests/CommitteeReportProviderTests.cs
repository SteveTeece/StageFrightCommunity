using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;
using StageFright.Reports.Rendering;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for CommitteeReportProvider's year-summary redesign (issue #234):
/// - One ReportSection per year, most-recent-first, with a SummaryRow showing the record count (FR-001, FR-002)
/// - President/Secretary/Treasurer lines always shown, "Vacant" when unfilled (FR-003–FR-005)
/// - Other distinct position labels shown as their own alphabetically-ordered lines (FR-006)
/// - Blank positions grouped under "General Committee Members", listed last (FR-006a)
/// - Case-insensitive/trimmed matching (FR-007) and the existing Member Status filter (FR-008)
/// - Years with no matching records omitted (FR-009); duplicate position holders combined (FR-010)
/// - Year present on every detail row, verified against the existing PDF/CSV renderers (FR-011)
/// </summary>
public class CommitteeReportProviderTests
{
    private readonly ICommitteeMembershipRepository _committeeMemberships = Substitute.For<ICommitteeMembershipRepository>();
    private readonly IMemberRepository _members = Substitute.For<IMemberRepository>();
    private readonly CommitteeReportProvider _sut;

    public CommitteeReportProviderTests()
    {
        _sut = new CommitteeReportProvider(_committeeMemberships, _members);
    }

    // --- User Story 1: Year-grouped committee overview ---

    [Fact]
    public async Task Should_OrderSectionsMostRecentYearFirst_When_MultipleYearsExist()
    {
        var alice = MakeMember("Alice");
        var bob = MakeMember("Bob");
        var carol = MakeMember("Carol");
        SetupMembers(alice, bob, carol);
        SetupCommittee(alice.Id, MakeCommittee(alice.Id, 2026, "President"));
        SetupCommittee(bob.Id, MakeCommittee(bob.Id, 2026, "Secretary"));
        SetupCommittee(carol.Id, MakeCommittee(carol.Id, 2024, "Treasurer"));

        var result = await _sut.GenerateAsync(DefaultFilters());

        Assert.Equal(2, result.Sections.Count);
        Assert.Equal("2026", result.Sections[0].SummaryRow!.Cells[0]);
        Assert.Equal("2024", result.Sections[1].SummaryRow!.Cells[0]);
    }

    [Fact]
    public async Task Should_ShowYearAndRecordCountInSummaryRow_When_SingleYearHasMultipleRecords()
    {
        var alice = MakeMember("Alice");
        var bob = MakeMember("Bob");
        var carol = MakeMember("Carol");
        SetupMembers(alice, bob, carol);
        SetupCommittee(alice.Id, MakeCommittee(alice.Id, 2026, "President"));
        SetupCommittee(bob.Id, MakeCommittee(bob.Id, 2026, "Secretary"));
        SetupCommittee(carol.Id, MakeCommittee(carol.Id, 2026, "Treasurer"));

        var result = await _sut.GenerateAsync(DefaultFilters());

        Assert.Equal(["Year", "Positions Recorded"], result.SummaryColumns!.Select(c => c.Header));
        var section = Assert.Single(result.Sections);
        Assert.Equal(["2026", "3"], section.SummaryRow!.Cells);
    }

    [Fact]
    public async Task Should_ScopeYearRecordCountToFilter_When_MemberStatusFilterApplied()
    {
        var active = MakeMember("Active Member", status: MemberStatus.Active, isDeleted: false);
        var archived = MakeMember("Archived Member", status: MemberStatus.Inactive, isDeleted: true);
        SetupMembers(active, archived);
        SetupCommittee(active.Id, MakeCommittee(active.Id, 2026, "President"));
        SetupCommittee(archived.Id, MakeCommittee(archived.Id, 2026, "Secretary"));

        var activeOnly = await _sut.GenerateAsync(FiltersFor("Active Only"));
        var all = await _sut.GenerateAsync(FiltersFor("All"));

        Assert.Equal("1", Assert.Single(activeOnly.Sections).SummaryRow!.Cells[1]);
        Assert.Equal("2", Assert.Single(all.Sections).SummaryRow!.Cells[1]);
    }

    [Fact]
    public async Task Should_OmitYear_When_NoRecordsMatchFilter()
    {
        var active = MakeMember("Active Member", status: MemberStatus.Active, isDeleted: false);
        var archived = MakeMember("Archived Member", status: MemberStatus.Inactive, isDeleted: true);
        SetupMembers(active, archived);
        SetupCommittee(active.Id, MakeCommittee(active.Id, 2026, "President"));
        SetupCommittee(archived.Id, MakeCommittee(archived.Id, 2025, "Secretary"));

        var result = await _sut.GenerateAsync(FiltersFor("Active Only"));

        var section = Assert.Single(result.Sections);
        Assert.Equal("2026", section.SummaryRow!.Cells[0]);
    }

    [Fact]
    public async Task Should_ReturnEmptySections_When_NoRecordsExist()
    {
        SetupMembers();

        var result = await _sut.GenerateAsync(DefaultFilters());

        Assert.Empty(result.Sections);
    }

    // --- User Story 2: Role breakdown within each year ---

    [Fact]
    public async Task Should_ShowNamedRolesAsVacantOrFilled_When_YearGenerated()
    {
        var carol = MakeMember("Carol");
        SetupMembers(carol);
        SetupCommittee(carol.Id, MakeCommittee(carol.Id, 2026, "Treasurer"));

        var result = await _sut.GenerateAsync(DefaultFilters());

        var section = Assert.Single(result.Sections);
        Assert.Contains(section.Rows, r => r.Cells[1] == "President" && r.Cells[2] == "Vacant");
        Assert.Contains(section.Rows, r => r.Cells[1] == "Secretary" && r.Cells[2] == "Vacant");
        Assert.Contains(section.Rows, r => r.Cells[1] == "Treasurer" && r.Cells[2] == "Carol");
    }

    [Fact]
    public async Task Should_OrderOtherPositionsAlphabeticallyAfterNamedRoles_When_NonNamedPositionsExist()
    {
        var alice = MakeMember("Alice");
        var bob = MakeMember("Bob");
        var carol = MakeMember("Carol");
        SetupMembers(alice, bob, carol);
        SetupCommittee(alice.Id, MakeCommittee(alice.Id, 2026, "President"));
        SetupCommittee(bob.Id, MakeCommittee(bob.Id, 2026, "Welfare Officer"));
        SetupCommittee(carol.Id, MakeCommittee(carol.Id, 2026, "Publicity Officer"));

        var result = await _sut.GenerateAsync(DefaultFilters());

        var section = Assert.Single(result.Sections);
        Assert.Equal(
            ["President", "Secretary", "Treasurer", "Publicity Officer", "Welfare Officer"],
            section.Rows.Select(r => r.Cells[1]));
    }

    [Fact]
    public async Task Should_GroupBlankPositionsAsGeneralCommitteeMembersLast_When_PositionIsBlank()
    {
        var alice = MakeMember("Alice");
        var zoe = MakeMember("Zoe");
        var bob = MakeMember("Bob");
        SetupMembers(alice, zoe, bob);
        SetupCommittee(alice.Id, MakeCommittee(alice.Id, 2026, "President"));
        SetupCommittee(zoe.Id, MakeCommittee(zoe.Id, 2026, "   "));
        SetupCommittee(bob.Id, MakeCommittee(bob.Id, 2026, ""));

        var result = await _sut.GenerateAsync(DefaultFilters());

        var section = Assert.Single(result.Sections);
        var lastRow = section.Rows[^1];
        Assert.Equal("General Committee Members", lastRow.Cells[1]);
        Assert.Equal("Bob, Zoe", lastRow.Cells[2]);
    }

    [Fact]
    public async Task Should_CollapsePositionVariants_When_MatchingIsCaseInsensitiveAndTrimmed()
    {
        var alice = MakeMember("Alice");
        var bob = MakeMember("Bob");
        var carol = MakeMember("Carol");
        var dave = MakeMember("Dave");
        SetupMembers(alice, bob, carol, dave);
        SetupCommittee(alice.Id, MakeCommittee(alice.Id, 2026, " president"));
        SetupCommittee(bob.Id, MakeCommittee(bob.Id, 2026, "PRESIDENT"));
        SetupCommittee(carol.Id, MakeCommittee(carol.Id, 2026, "welfare officer"));
        SetupCommittee(dave.Id, MakeCommittee(dave.Id, 2026, "Welfare Officer "));

        var result = await _sut.GenerateAsync(DefaultFilters());

        var section = Assert.Single(result.Sections);
        Assert.Single(section.Rows, r => r.Cells[1] == "President");
        Assert.Equal("Alice, Bob", section.Rows.Single(r => r.Cells[1] == "President").Cells[2]);

        var otherPositionRows = section.Rows.Where(r =>
            r.Cells[1] is not ("President" or "Secretary" or "Treasurer" or "General Committee Members")).ToList();
        var welfareRow = Assert.Single(otherPositionRows);
        Assert.Equal("welfare officer", welfareRow.Cells[1]);
        Assert.Equal("Carol, Dave", welfareRow.Cells[2]);
    }

    [Fact]
    public async Task Should_ListDuplicatePositionHoldersTogether_When_MultipleMembersShareAPosition()
    {
        var alice = MakeMember("Alice");
        var bob = MakeMember("Bob");
        SetupMembers(alice, bob);
        SetupCommittee(alice.Id, MakeCommittee(alice.Id, 2026, "President"));
        SetupCommittee(bob.Id, MakeCommittee(bob.Id, 2026, "President"));

        var result = await _sut.GenerateAsync(DefaultFilters());

        var section = Assert.Single(result.Sections);
        var presidentRow = section.Rows.Single(r => r.Cells[1] == "President");
        Assert.Equal("Alice, Bob", presidentRow.Cells[2]);
    }

    [Fact]
    public async Task Should_SortMembersAlphabetically_When_MultipleMembersShareAPositionLine()
    {
        var bob = MakeMember("Bob");
        var alice = MakeMember("Alice");
        SetupMembers(bob, alice);
        SetupCommittee(bob.Id, MakeCommittee(bob.Id, 2026, "Welfare Officer"));
        SetupCommittee(alice.Id, MakeCommittee(alice.Id, 2026, "Welfare Officer"));

        var result = await _sut.GenerateAsync(DefaultFilters());

        var section = Assert.Single(result.Sections);
        var welfareRow = section.Rows.Single(r => r.Cells[1] == "Welfare Officer");
        Assert.Equal("Alice, Bob", welfareRow.Cells[2]);
    }

    // --- User Story 3: Exportable, consistent output ---

    [Fact]
    public async Task Should_SetFirstCellToSectionYear_When_RowsAreGenerated()
    {
        var alice = MakeMember("Alice");
        var bob = MakeMember("Bob");
        SetupMembers(alice, bob);
        SetupCommittee(alice.Id, MakeCommittee(alice.Id, 2026, "President"));
        SetupCommittee(bob.Id, MakeCommittee(bob.Id, 2025, "Secretary"));

        var result = await _sut.GenerateAsync(DefaultFilters());

        foreach (var section in result.Sections)
        {
            var expectedYear = section.SummaryRow!.Cells[0];
            Assert.All(section.Rows, r => Assert.Equal(expectedYear, r.Cells[0]));
        }
    }

    [Fact]
    public async Task Should_ContainYearPositionAndMembers_When_ExportedToCsv()
    {
        var alice = MakeMember("Alice");
        var bob = MakeMember("Bob");
        SetupMembers(alice, bob);
        SetupCommittee(alice.Id, MakeCommittee(alice.Id, 2026, "President"));
        SetupCommittee(bob.Id, MakeCommittee(bob.Id, 2026, "Welfare Officer"));

        var report = await _sut.GenerateAsync(DefaultFilters());
        var csv = new CsvReportExporter().Export(report);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains(lines, l => l.Contains("2026") && l.Contains("President") && l.Contains("Alice"));
        Assert.Contains(lines, l => l.Contains("2026") && l.Contains("Welfare Officer") && l.Contains("Bob"));
    }

    [Fact]
    public async Task Should_ReturnNonEmptyByteArray_When_RenderedToPdf()
    {
        var alice = MakeMember("Alice");
        var bob = MakeMember("Bob");
        var carol = MakeMember("Carol");
        SetupMembers(alice, bob, carol);
        SetupCommittee(alice.Id, MakeCommittee(alice.Id, 2026, "President"));
        SetupCommittee(bob.Id, MakeCommittee(bob.Id, 2026, "Welfare Officer"));
        SetupCommittee(carol.Id, MakeCommittee(carol.Id, 2026, ""));

        var report = await _sut.GenerateAsync(DefaultFilters());
        var bytes = new PdfReportRenderer().Render(report);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    // --- Helpers ---

    private static ReportFilterValues DefaultFilters() => FiltersFor("Active Only");

    private static ReportFilterValues FiltersFor(string memberFilter)
    {
        var filters = new ReportFilterValues();
        filters.Set("memberFilter", memberFilter);
        return filters;
    }

    private void SetupMembers(params Member[] members)
    {
        _members.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Member>>(
                members.Where(m => !m.IsDeleted && m.Status == MemberStatus.Active).ToList()));
        _members.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Member>>(members.Where(m => !m.IsDeleted).ToList()));
        _members.GetArchivedAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Member>>(members.Where(m => m.IsDeleted).ToList()));

        foreach (var member in members)
        {
            _committeeMemberships.GetByMemberAsync(member.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<CommitteeMembership>>([]));
        }
    }

    private void SetupCommittee(Guid memberId, params CommitteeMembership[] memberships)
    {
        _committeeMemberships.GetByMemberAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CommitteeMembership>>(memberships.ToList()));
    }

    private static CommitteeMembership MakeCommittee(Guid memberId, int year, string position)
        => new()
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            Year = year,
            Position = position,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static Member MakeMember(string name, MemberStatus status = MemberStatus.Active, bool isDeleted = false)
        => new()
        {
            Id = Guid.NewGuid(),
            FirstName = name,
            StreetAddress = "1 Test St",
            JoinDate = DateTime.UtcNow.AddYears(-1),
            Status = status,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
}
