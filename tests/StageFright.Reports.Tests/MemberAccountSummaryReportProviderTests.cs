using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for MemberAccountSummaryReportProvider:
/// - Opening balance, period transactions, closing balance
/// - Fee aging by DueDate: current / 30 / 60 / 90+ days
/// - Archived members included (IgnoreQueryFilters equivalent)
/// </summary>
public class MemberAccountSummaryReportProviderTests
{
    private readonly IGLRepository _gl = Substitute.For<IGLRepository>();
    private readonly IMemberRepository _members = Substitute.For<IMemberRepository>();
    private readonly IFeeRepository _fees = Substitute.For<IFeeRepository>();
    private readonly MemberAccountSummaryReportProvider _sut;

    private static readonly DateTime Today = new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc);

    public MemberAccountSummaryReportProviderTests()
    {
        _sut = new MemberAccountSummaryReportProvider(_gl, _members, _fees);
    }

    [Fact]
    public void ReportId_IsMemberAccountSummary()
    {
        Assert.Equal("member-account-summary", _sut.ReportId);
    }

    [Fact]
    public void ModuleName_IsFinance()
    {
        Assert.Equal("Finance", _sut.ModuleName);
    }

    [Fact]
    public async Task GenerateAsync_EachMemberHasOwnSection()
    {
        var m1 = MakeMember(Guid.NewGuid(), "Alice", false);
        var m2 = MakeMember(Guid.NewGuid(), "Bob", false);
        SetupMembers(m1, m2);
        SetupBalance(m1.Id, 50m);
        SetupBalance(m2.Id, 30m);
        SetupMemberTransactions(m1.Id);
        SetupMemberTransactions(m2.Id);
        SetupFees(m1.Id);
        SetupFees(m2.Id);

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.Contains(result.Sections, s => s.Heading != null && s.Heading.Contains("Alice"));
        Assert.Contains(result.Sections, s => s.Heading != null && s.Heading.Contains("Bob"));
    }

    [Fact]
    public async Task GenerateAsync_IncludesArchivedMembers()
    {
        var archived = MakeMember(Guid.NewGuid(), "Old Member", isDeleted: true);
        SetupMembers(archived);
        SetupBalance(archived.Id, 10m);
        SetupMemberTransactions(archived.Id);
        SetupFees(archived.Id);

        var filters = CurrentYearFilters();
        filters.Set("includeArchived", "true");
        var result = await _sut.GenerateAsync(filters);

        Assert.Contains(result.Sections, s => s.Heading != null && s.Heading.Contains("Old Member"));
    }

    [Fact]
    public async Task GenerateAsync_ExcludesArchivedMembers_ByDefault()
    {
        var archived = MakeMember(Guid.NewGuid(), "Old Member", isDeleted: true);
        SetupMembers(archived);
        SetupBalance(archived.Id, 10m);
        SetupMemberTransactions(archived.Id);
        SetupFees(archived.Id);

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.DoesNotContain(result.Sections, s => s.Heading != null && s.Heading.Contains("Old Member"));
    }

    [Fact]
    public async Task GenerateAsync_FeeAging_Current_WhenNotYetDue()
    {
        var memberId = Guid.NewGuid();
        var member = MakeMember(memberId, "Test Member", false);
        SetupMembers(member);
        SetupBalance(memberId, 100m);
        SetupMemberTransactions(memberId);

        // Fee due in the future (current)
        var fee = MakeFee(memberId, 100m, Today.AddDays(10));
        SetupFees(memberId, fee);

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var section = result.Sections.First(s => s.Heading != null && s.Heading.Contains("Test Member"));
        var allText = string.Join(" ", section.Rows.SelectMany(r => r.Cells));
        Assert.Contains("Current", allText);
    }

    [Fact]
    public async Task GenerateAsync_FeeAging_30Days_WhenOverdue30Days()
    {
        var memberId = Guid.NewGuid();
        var member = MakeMember(memberId, "Late Member", false);
        SetupMembers(member);
        SetupBalance(memberId, 100m);
        SetupMemberTransactions(memberId);

        var fee = MakeFee(memberId, 100m, Today.AddDays(-35)); // 35 days overdue
        SetupFees(memberId, fee);

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var section = result.Sections.First(s => s.Heading != null && s.Heading.Contains("Late Member"));
        var allText = string.Join(" ", section.Rows.SelectMany(r => r.Cells));
        Assert.Contains("30", allText);
    }

    [Fact]
    public async Task GenerateAsync_FeeAging_90Plus_WhenOverdue90Days()
    {
        var memberId = Guid.NewGuid();
        var member = MakeMember(memberId, "Very Late", false);
        SetupMembers(member);
        SetupBalance(memberId, 100m);
        SetupMemberTransactions(memberId);

        var fee = MakeFee(memberId, 100m, Today.AddDays(-100)); // >90 days
        SetupFees(memberId, fee);

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var section = result.Sections.First(s => s.Heading != null && s.Heading.Contains("Very Late"));
        var allText = string.Join(" ", section.Rows.SelectMany(r => r.Cells));
        Assert.Contains("90+", allText);
    }

    [Fact]
    public async Task GenerateAsync_TransactionsWithinMember_AreOrderedOldestFirst()
    {
        var memberId = Guid.NewGuid();
        var member = MakeMember(memberId, "Ledger Reader", false);
        SetupMembers(member);
        SetupBalance(memberId, 300m);
        SetupFees(memberId);

        // Seeded in shuffled (non-chronological) input order deliberately.
        var mid = MakeTransaction(memberId, new DateTime(2026, 2, 9, 0, 0, 0, DateTimeKind.Utc), "Mid");
        var newest = MakeTransaction(memberId, new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc), "Newest");
        var oldest = MakeTransaction(memberId, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Oldest");
        SetupMemberTransactions(memberId, newest, oldest, mid);

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var section = result.Sections.Single(s => s.Heading != null && s.Heading.Contains("Ledger Reader"));
        Assert.Equal("Opening Balance", section.Rows[0].Cells[0]);
        Assert.Equal("2026-01-01", section.Rows[1].Cells[0]);
        Assert.Equal("2026-02-09", section.Rows[2].Cells[0]);
        Assert.Equal("2026-02-16", section.Rows[3].Cells[0]);
        Assert.Equal("Closing Balance", section.Rows[4].Cells[0]);
        Assert.Equal("Aging", section.Rows[5].Cells[0]);
    }

    [Fact]
    public async Task GenerateAsync_ReportTitle_IsMemberAccountSummary()
    {
        SetupMembers();
        var result = await _sut.GenerateAsync(CurrentYearFilters());
        Assert.Equal("Member Account Summary", result.Title);
    }

    [Fact]
    public async Task GenerateAsync_SummaryColumns_HasSixHeadersAndEverySectionHasMatchingSummaryRow()
    {
        var m1 = MakeMember(Guid.NewGuid(), "Alice", false);
        var m2 = MakeMember(Guid.NewGuid(), "Bob", false);
        SetupMembers(m1, m2);
        SetupBalance(m1.Id, 50m);
        SetupBalance(m2.Id, 0m);
        SetupMemberTransactions(m1.Id);
        SetupMemberTransactions(m2.Id);
        SetupFees(m1.Id);
        SetupFees(m2.Id);

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.Equal(6, result.SummaryColumns?.Count);
        Assert.Equal(["Member", "Current", "30 Days", "60 Days", "90+ Days", "Balance"],
            result.SummaryColumns!.Select(c => c.Header));
        Assert.All(result.Sections, s => Assert.Equal(result.SummaryColumns!.Count, s.SummaryRow!.Cells.Count));
    }

    [Fact]
    public async Task GenerateAsync_MemberWithNoOutstandingFees_StillGetsSummaryRowWithZeroAgingCells()
    {
        var member = MakeMember(Guid.NewGuid(), "Paid Up", false);
        SetupMembers(member);
        SetupBalance(member.Id, 0m);
        SetupMemberTransactions(member.Id);
        SetupFees(member.Id);

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var section = result.Sections.Single(s => s.Heading != null && s.Heading.Contains("Paid Up"));
        Assert.NotNull(section.SummaryRow);
        Assert.Equal("Current: 0.00", section.SummaryRow!.Cells[1]);
        Assert.Equal("30 days: 0.00", section.SummaryRow.Cells[2]);
        Assert.Equal("60 days: 0.00", section.SummaryRow.Cells[3]);
        Assert.Equal("90+ days: 0.00", section.SummaryRow.Cells[4]);
    }

    // --- Helpers ---

    private void SetupMembers(params Member[] members)
    {
        _members.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Member>>(members.Where(m => !m.IsDeleted).ToList()));
        _members.GetArchivedAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Member>>(members.Where(m => m.IsDeleted).ToList()));
    }

    private void SetupBalance(Guid memberId, decimal balance)
    {
        _gl.GetMemberBalanceAsync(memberId, Arg.Any<CancellationToken>()).Returns(balance);
    }

    private void SetupMemberTransactions(Guid memberId, params Transaction[] transactions)
    {
        _gl.GetByMemberAsync(memberId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Transaction>>(transactions.ToList()));
    }

    private static Transaction MakeTransaction(Guid memberId, DateTime date, string description)
        => new()
        {
            Id = Guid.NewGuid(), MemberId = memberId, Date = date, Description = description,
            DebitAmount = 0m, CreditAmount = 10m, GLAccount = "1100", CreatedAt = DateTime.UtcNow
        };

    private void SetupFees(Guid memberId, params Fee[] fees)
    {
        _fees.GetByMemberAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Fee>>(fees.ToList()));
    }

    private static Member MakeMember(Guid id, string name, bool isDeleted)
        => new()
        {
            Id = id, Name = name, StreetAddress = "1 Test St",
            Status = isDeleted ? MemberStatus.Inactive : MemberStatus.Active,
            IsDeleted = isDeleted,
            JoinDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

    private static Fee MakeFee(Guid memberId, decimal amount, DateTime dueDate)
        => new()
        {
            Id = Guid.NewGuid(), MemberId = memberId, FeeType = FeeType.Annual,
            Amount = amount, FeeDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = dueDate, PaidAtCreation = false, CreatedAt = DateTime.UtcNow
        };

    private static ReportFilterValues CurrentYearFilters()
    {
        var f = new ReportFilterValues();
        f.Set("dateFrom", $"{DateTime.UtcNow.Year}-01-01");
        f.Set("dateTo", $"{DateTime.UtcNow.Year}-12-31");
        return f;
    }
}
