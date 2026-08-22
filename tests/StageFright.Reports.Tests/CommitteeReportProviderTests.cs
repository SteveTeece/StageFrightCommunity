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
    private readonly ICommitteePositionRecordRepository _committeePositionRecords = Substitute.For<ICommitteePositionRecordRepository>();
    private readonly IMemberRepository _members = Substitute.For<IMemberRepository>();
    private readonly CommitteeReportProvider _sut;

    public CommitteeReportProviderTests()
    {
        _sut = new CommitteeReportProvider(_committeePositionRecords, _members);
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

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

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

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

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

        var activeOnly = await _sut.GenerateAsync(FiltersFor("Active Only"), TestContext.Current.CancellationToken);
        var all = await _sut.GenerateAsync(FiltersFor("All"), TestContext.Current.CancellationToken);

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

        var result = await _sut.GenerateAsync(FiltersFor("Active Only"), TestContext.Current.CancellationToken);

        var section = Assert.Single(result.Sections);
        Assert.Equal("2026", section.SummaryRow!.Cells[0]);
    }

    [Fact]
    public async Task Should_ReturnEmptySections_When_NoRecordsExist()
    {
        SetupMembers();

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

        Assert.Empty(result.Sections);
    }

    // --- US3: AGM-month term boundaries (spec 013) ---

    [Fact]
    public async Task Should_GroupByCommitteeTermLabelYear_When_RecordBelongsToATerm()
    {
        var alice = MakeMember("Alice");
        SetupMembers(alice);
        var term = MakeTerm(labelYear: 2027);
        SetupCommittee(alice.Id, MakeTermPositionRecord(alice.Id, term, officeHolderType: null));

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

        var section = Assert.Single(result.Sections);
        Assert.Equal("2027", section.SummaryRow!.Cells[0]);
    }

    [Fact]
    public async Task Should_GroupLegacyAndTermRecords_ByTheirOwnResolvedYear_When_BothExist()
    {
        var alice = MakeMember("Alice");
        var bob = MakeMember("Bob");
        SetupMembers(alice, bob);
        var term = MakeTerm(labelYear: 2027);
        SetupCommittee(alice.Id, MakeTermPositionRecord(alice.Id, term, officeHolderType: null));
        SetupCommittee(bob.Id, MakeCommittee(bob.Id, 2024, "Treasurer"));

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Sections.Count);
        Assert.Equal("2027", result.Sections[0].SummaryRow!.Cells[0]);
        Assert.Equal("2024", result.Sections[1].SummaryRow!.Cells[0]);
    }

    [Fact]
    public async Task Should_ResolveOfficeHolderTypeName_AsPositionLabel_When_RecordBelongsToATerm()
    {
        var alice = MakeMember("Alice");
        SetupMembers(alice);
        var term = MakeTerm(labelYear: 2027);
        var officeHolderType = new CommitteeOfficeHolderType
        {
            Id = Guid.NewGuid(), Name = "President", DisplayOrder = 0, IsBuiltIn = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        SetupCommittee(alice.Id, MakeTermPositionRecord(alice.Id, term, officeHolderType));

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

        var section = Assert.Single(result.Sections);
        Assert.Contains(section.Rows, r => r.Cells[1] == "President" && r.Cells[2] == "Alice");
    }

    [Fact]
    public async Task Should_GroupTermRecordWithNoOfficeHolderType_AsGeneralCommitteeMember_When_RecordBelongsToATerm()
    {
        var alice = MakeMember("Alice");
        SetupMembers(alice);
        var term = MakeTerm(labelYear: 2027);
        SetupCommittee(alice.Id, MakeTermPositionRecord(alice.Id, term, officeHolderType: null));

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

        var section = Assert.Single(result.Sections);
        var lastRow = section.Rows[^1];
        Assert.Equal("General Committee Members", lastRow.Cells[1]);
        Assert.Equal("Alice", lastRow.Cells[2]);
    }

    // --- User Story 4: Special elections — multi-holder dated display (FR-029) ---

    [Fact]
    public async Task Should_RenderNameOnly_NoDates_When_PositionHasSingleHolder()
    {
        var alice = MakeMember("Alice");
        SetupMembers(alice);
        var term = MakeTerm(labelYear: 2026);
        var president = new CommitteeOfficeHolderType
        {
            Id = Guid.NewGuid(), Name = "President", DisplayOrder = 0, IsBuiltIn = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        SetupCommittee(alice.Id, MakeTermPositionRecord(alice.Id, term, president));

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

        var section = Assert.Single(result.Sections);
        var presidentRow = section.Rows.Single(r => r.Cells[1] == "President");
        Assert.Equal("Alice", presidentRow.Cells[2]);
        Assert.DoesNotContain("(", presidentRow.Cells[2]);
    }

    [Fact]
    public async Task Should_RenderDatedHolderList_OrderedByStartDate_When_SpecialElectionReplacedAHolder()
    {
        var alice = MakeMember("Alice");
        var bob = MakeMember("Bob");
        SetupMembers(alice, bob);
        var term = MakeTerm(labelYear: 2026);
        var president = new CommitteeOfficeHolderType
        {
            Id = Guid.NewGuid(), Name = "President", DisplayOrder = 0, IsBuiltIn = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var outgoing = MakeTermPositionRecord(alice.Id, term, president);
        outgoing.StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        outgoing.EndDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var incoming = MakeTermPositionRecord(bob.Id, term, president);
        incoming.StartDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        incoming.EndDate = null;

        _committeePositionRecords.GetByMemberAsync(alice.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CommitteePositionRecord>>([outgoing]));
        _committeePositionRecords.GetByMemberAsync(bob.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CommitteePositionRecord>>([incoming]));

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

        var section = Assert.Single(result.Sections);
        var presidentRow = section.Rows.Single(r => r.Cells[1] == "President");
        var cell = presidentRow.Cells[2];

        // Alice (outgoing, dated) must appear before Bob (incoming, "present") — ordered by StartDate.
        Assert.True(cell.IndexOf("Alice", StringComparison.Ordinal) < cell.IndexOf("Bob", StringComparison.Ordinal));
        Assert.Contains("2026", cell);
        Assert.Contains("–present)", cell);
        Assert.DoesNotContain("Alice, Bob", cell);
    }

    // --- User Story 2: Role breakdown within each year ---

    [Fact]
    public async Task Should_ShowNamedRolesAsVacantOrFilled_When_YearGenerated()
    {
        var carol = MakeMember("Carol");
        SetupMembers(carol);
        SetupCommittee(carol.Id, MakeCommittee(carol.Id, 2026, "Treasurer"));

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

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

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

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

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

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

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

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

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

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

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

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

        var result = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);

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

        var report = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);
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

        var report = await _sut.GenerateAsync(DefaultFilters(), TestContext.Current.CancellationToken);
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
            _committeePositionRecords.GetByMemberAsync(member.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<CommitteePositionRecord>>([]));
        }
    }

    private void SetupCommittee(Guid memberId, params CommitteePositionRecord[] memberships)
    {
        _committeePositionRecords.GetByMemberAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CommitteePositionRecord>>(memberships.ToList()));
    }

    private static CommitteePositionRecord MakeCommittee(Guid memberId, int year, string position)
        => new()
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            Year = year,
            Position = position,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static CommitteeTerm MakeTerm(int labelYear)
        => new()
        {
            Id = Guid.NewGuid(),
            StartedByAgmId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow,
            EndDate = null,
            LabelYear = labelYear,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static CommitteePositionRecord MakeTermPositionRecord(Guid memberId, CommitteeTerm term, CommitteeOfficeHolderType? officeHolderType)
        => new()
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            CommitteeTermId = term.Id,
            CommitteeTerm = term,
            OfficeHolderTypeId = officeHolderType?.Id,
            OfficeHolderType = officeHolderType,
            StartDate = term.StartDate,
            EndDate = null,
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
