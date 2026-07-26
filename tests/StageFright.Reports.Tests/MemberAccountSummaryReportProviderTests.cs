using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for MemberAccountSummaryReportProvider:
/// - Opening balance, period transactions, closing balance
/// - Fee aging by DueDate from GL-derived remaining amounts (issue #244)
/// - Aging buckets always sum to the member's GL balance
/// - Members with no outstanding balance are excluded
/// - Archived members included when requested
/// </summary>
public class MemberAccountSummaryReportProviderTests
{
    private readonly IGLRepository _gl = Substitute.For<IGLRepository>();
    private readonly IMemberRepository _members = Substitute.For<IMemberRepository>();
    private readonly IMemberBalanceService _balances = Substitute.For<IMemberBalanceService>();
    private readonly MemberAccountSummaryReportProvider _sut;

    private static readonly DateTime Today = DateTime.UtcNow.Date;

    public MemberAccountSummaryReportProviderTests()
    {
        _sut = new MemberAccountSummaryReportProvider(_gl, _members, _balances);
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
        SetupOutstandingFees(m1.Id, MakeOutstandingFee(50m, Today.AddDays(10)));
        SetupOutstandingFees(m2.Id, MakeOutstandingFee(30m, Today.AddDays(10)));

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
        SetupOutstandingFees(archived.Id, MakeOutstandingFee(10m, Today.AddDays(-5)));

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
        SetupOutstandingFees(archived.Id, MakeOutstandingFee(10m, Today.AddDays(-5)));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.DoesNotContain(result.Sections, s => s.Heading != null && s.Heading.Contains("Old Member"));
    }

    [Fact]
    public async Task Should_ExcludeMember_When_BalanceIsZero()
    {
        var paidUp = MakeMember(Guid.NewGuid(), "Paid Up", false);
        var owing = MakeMember(Guid.NewGuid(), "Still Owing", false);
        SetupMembers(paidUp, owing);
        SetupBalance(paidUp.Id, 0m);
        SetupBalance(owing.Id, 20m);
        SetupMemberTransactions(paidUp.Id);
        SetupMemberTransactions(owing.Id);
        SetupOutstandingFees(paidUp.Id);
        SetupOutstandingFees(owing.Id, MakeOutstandingFee(20m, Today.AddDays(10)));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.DoesNotContain(result.Sections, s => s.Heading != null && s.Heading.Contains("Paid Up"));
        Assert.Contains(result.Sections, s => s.Heading != null && s.Heading.Contains("Still Owing"));
    }

    [Fact]
    public async Task Should_ExcludeMember_When_BalanceIsCredit()
    {
        var inCredit = MakeMember(Guid.NewGuid(), "In Credit", false);
        SetupMembers(inCredit);
        SetupBalance(inCredit.Id, -15m);
        SetupMemberTransactions(inCredit.Id);
        SetupOutstandingFees(inCredit.Id);

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.Empty(result.Sections);
    }

    [Fact]
    public async Task Should_AgeRemainingAmountOnly_When_FeePartiallyPaid()
    {
        var memberId = Guid.NewGuid();
        SetupMembers(MakeMember(memberId, "Partial Payer", false));
        SetupBalance(memberId, 40m);
        SetupMemberTransactions(memberId);

        // Original fee was 100, 60 already settled via GL — only 40 remains, 90+ days overdue
        SetupOutstandingFees(memberId, MakeOutstandingFee(40m, Today.AddDays(-100)));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var section = result.Sections.Single(s => s.Heading != null && s.Heading.Contains("Partial Payer"));
        Assert.Equal("Current: 0.00", section.SummaryRow!.Cells[1]);
        Assert.Equal("90+ days: 40.00", section.SummaryRow.Cells[4]);
        Assert.Equal("40.00", section.SummaryRow.Cells[5]);
    }

    [Fact]
    public async Task Should_ReduceOldestFeeFirst_When_UnallocatedCreditExists()
    {
        var memberId = Guid.NewGuid();
        SetupMembers(MakeMember(memberId, "Credit Holder", false));
        SetupBalance(memberId, 7m);
        SetupMemberTransactions(memberId);

        // Remaining fees total 10 but GL balance is 7: an unallocated 3.00 credit
        // must be walked off the oldest fee first so buckets sum to the balance.
        SetupOutstandingFees(memberId,
            MakeOutstandingFee(5m, Today.AddDays(-100), feeDate: Today.AddMonths(-6)),
            MakeOutstandingFee(5m, Today.AddDays(10), feeDate: Today.AddMonths(-1)));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var section = result.Sections.Single(s => s.Heading != null && s.Heading.Contains("Credit Holder"));
        Assert.Equal("Current: 5.00", section.SummaryRow!.Cells[1]);
        Assert.Equal("90+ days: 2.00", section.SummaryRow.Cells[4]);
        Assert.Equal("7.00", section.SummaryRow.Cells[5]);
    }

    [Fact]
    public async Task Should_SumAgingBucketsToBalance_When_FeesSpanAllBuckets()
    {
        var memberId = Guid.NewGuid();
        SetupMembers(MakeMember(memberId, "Bucket Tester", false));
        SetupBalance(memberId, 22m);
        SetupMemberTransactions(memberId);
        SetupOutstandingFees(memberId,
            MakeOutstandingFee(4m, Today.AddDays(5)),     // current
            MakeOutstandingFee(6m, Today.AddDays(-15)),   // 30 days
            MakeOutstandingFee(5m, Today.AddDays(-45)),   // 60 days
            MakeOutstandingFee(7m, Today.AddDays(-95)));  // 90+

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var section = result.Sections.Single(s => s.Heading != null && s.Heading.Contains("Bucket Tester"));
        Assert.Equal("Current: 4.00", section.SummaryRow!.Cells[1]);
        Assert.Equal("30 days: 6.00", section.SummaryRow.Cells[2]);
        Assert.Equal("60 days: 5.00", section.SummaryRow.Cells[3]);
        Assert.Equal("90+ days: 7.00", section.SummaryRow.Cells[4]);
        Assert.Equal("22.00", section.SummaryRow.Cells[5]);
    }

    [Fact]
    public async Task Should_ComputeZeroOpeningBalance_When_EntireBalanceAccruedDuringPeriod()
    {
        var memberId = Guid.NewGuid();
        SetupMembers(MakeMember(memberId, "New Debtor", false));
        // The member's whole $100 history is a single debit dated inside the report period —
        // before the period started they owed nothing, so Opening Balance must be 0.00.
        SetupBalance(memberId, 100m);
        SetupMemberTransactions(memberId,
            MakeDebitTransaction(memberId, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), "Fee accrued", 100m));
        SetupOutstandingFees(memberId, MakeOutstandingFee(100m, Today.AddDays(10)));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var section = result.Sections.Single(s => s.Heading != null && s.Heading.Contains("New Debtor"));
        Assert.Equal("Opening Balance", section.Rows[0].Cells[0]);
        Assert.Equal("0.00", section.Rows[0].Cells[4]);
    }

    [Fact]
    public async Task Should_ComputeOpeningBalance_When_PaymentDuringPeriodReducesPriorDebt()
    {
        var memberId = Guid.NewGuid();
        SetupMembers(MakeMember(memberId, "Partial Settler", false));
        // Balance today is 60; the only period activity is a 40 payment credit, so before the
        // period the member owed 100 (60 + 40), not 20 (60 - 40, the pre-fix sign-flipped result).
        SetupBalance(memberId, 60m);
        SetupMemberTransactions(memberId,
            MakeCreditTransaction(memberId, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), "Payment", 40m));
        SetupOutstandingFees(memberId, MakeOutstandingFee(60m, Today.AddDays(10)));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var section = result.Sections.Single(s => s.Heading != null && s.Heading.Contains("Partial Settler"));
        Assert.Equal("100.00", section.Rows[0].Cells[4]);
    }

    [Fact]
    public async Task Should_ExcludeTransactionRows_When_LinkedFeeIsFullyPaid()
    {
        var memberId = Guid.NewGuid();
        var paidFeeId = Guid.NewGuid();
        var unpaidFeeId = Guid.NewGuid();
        SetupMembers(MakeMember(memberId, "Mixed History", false));
        SetupBalance(memberId, 30m);
        SetupMemberTransactions(memberId,
            MakeDebitTransaction(memberId, Today.AddMonths(-2), "Paid fee accrual", 20m, paidFeeId),
            MakeCreditTransaction(memberId, Today.AddMonths(-2), "Paid fee cleared", 20m, paidFeeId),
            MakeDebitTransaction(memberId, Today.AddMonths(-1), "Unpaid fee accrual", 30m, unpaidFeeId));
        SetupOutstandingFees(memberId, MakeOutstandingFee(30m, Today.AddDays(10), feeId: unpaidFeeId));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var section = result.Sections.Single(s => s.Heading != null && s.Heading.Contains("Mixed History"));
        var descriptions = section.Rows.SelectMany(r => r.Cells).ToList();
        Assert.DoesNotContain("Paid fee accrual", descriptions);
        Assert.DoesNotContain("Paid fee cleared", descriptions);
        Assert.Contains("Unpaid fee accrual", descriptions);
    }

    [Fact]
    public async Task Should_IncludeTransactionRow_When_NotLinkedToAnyFee()
    {
        var memberId = Guid.NewGuid();
        SetupMembers(MakeMember(memberId, "Adjustment Case", false));
        SetupBalance(memberId, 10m);
        SetupMemberTransactions(memberId,
            MakeCreditTransaction(memberId, Today.AddMonths(-1), "Overpayment credit", 5m));
        SetupOutstandingFees(memberId, MakeOutstandingFee(10m, Today.AddDays(10)));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var section = result.Sections.Single(s => s.Heading != null && s.Heading.Contains("Adjustment Case"));
        var descriptions = section.Rows.SelectMany(r => r.Cells).ToList();
        Assert.Contains("Overpayment credit", descriptions);
    }

    [Fact]
    public async Task Should_KeepOpeningAndClosingBalanceAccurate_When_SomeTransactionsAreHiddenAsFullyPaid()
    {
        var memberId = Guid.NewGuid();
        var paidFeeId = Guid.NewGuid();
        var unpaidFeeId = Guid.NewGuid();
        SetupMembers(MakeMember(memberId, "Reconciled Debtor", false));
        // Closing balance = 30 (only the unpaid fee). The paid fee's accrual/clear pair nets to
        // zero, so the hidden rows must not distort Opening Balance even though they're not shown.
        SetupBalance(memberId, 30m);
        SetupMemberTransactions(memberId,
            MakeDebitTransaction(memberId, Today.AddMonths(-2), "Paid fee accrual", 20m, paidFeeId),
            MakeCreditTransaction(memberId, Today.AddMonths(-2), "Paid fee cleared", 20m, paidFeeId),
            MakeDebitTransaction(memberId, Today.AddMonths(-1), "Unpaid fee accrual", 30m, unpaidFeeId));
        SetupOutstandingFees(memberId, MakeOutstandingFee(30m, Today.AddDays(10), feeId: unpaidFeeId));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var section = result.Sections.Single(s => s.Heading != null && s.Heading.Contains("Reconciled Debtor"));
        Assert.Equal("Opening Balance", section.Rows[0].Cells[0]);
        Assert.Equal("0.00", section.Rows[0].Cells[4]);
        Assert.Equal("Closing Balance", section.Rows.Last(r => r.Cells[0] == "Closing Balance").Cells[0]);
        Assert.Equal("30.00", section.Rows.Single(r => r.Cells[0] == "Closing Balance").Cells[4]);
    }

    [Fact]
    public async Task GenerateAsync_TransactionsWithinMember_AreOrderedOldestFirst()
    {
        var memberId = Guid.NewGuid();
        var member = MakeMember(memberId, "Ledger Reader", false);
        SetupMembers(member);
        SetupBalance(memberId, 300m);
        SetupOutstandingFees(memberId, MakeOutstandingFee(300m, Today.AddDays(10)));

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
        Assert.Equal(5, section.Rows.Count);
    }

    [Fact]
    public async Task GenerateAsync_ReportTitle_IsMemberAccountSummary()
    {
        SetupMembers();
        var result = await _sut.GenerateAsync(CurrentYearFilters());
        Assert.Equal("Member Account Summary", result.Title);
    }

    [Fact]
    public async Task Should_NotIncludeAgingColumnOrRow_When_AgingAlreadyShownOnSummaryLine()
    {
        var memberId = Guid.NewGuid();
        SetupMembers(MakeMember(memberId, "No Duplicate Aging", false));
        SetupBalance(memberId, 20m);
        SetupMemberTransactions(memberId);
        SetupOutstandingFees(memberId, MakeOutstandingFee(20m, Today.AddDays(-45)));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.Equal(["Date / Item", "Description", "Debit", "Credit", "Balance"],
            result.Columns.Select(c => c.Header));

        var section = result.Sections.Single(s => s.Heading != null && s.Heading.Contains("No Duplicate Aging"));
        Assert.DoesNotContain(section.Rows, r => r.Cells[0] == "Aging");
        Assert.All(section.Rows, r => Assert.Equal(result.Columns.Count, r.Cells.Count));
    }

    [Fact]
    public async Task GenerateAsync_SummaryColumns_HasSixHeadersAndEverySectionHasMatchingSummaryRow()
    {
        var m1 = MakeMember(Guid.NewGuid(), "Alice", false);
        var m2 = MakeMember(Guid.NewGuid(), "Bob", false);
        SetupMembers(m1, m2);
        SetupBalance(m1.Id, 50m);
        SetupBalance(m2.Id, 30m);
        SetupMemberTransactions(m1.Id);
        SetupMemberTransactions(m2.Id);
        SetupOutstandingFees(m1.Id, MakeOutstandingFee(50m, Today.AddDays(10)));
        SetupOutstandingFees(m2.Id, MakeOutstandingFee(30m, Today.AddDays(10)));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.Equal(6, result.SummaryColumns?.Count);
        Assert.Equal(["Member", "Current", "30 Days", "60 Days", "90+ Days", "Balance"],
            result.SummaryColumns!.Select(c => c.Header));
        Assert.Equal(2, result.Sections.Count);
        Assert.All(result.Sections, s => Assert.Equal(result.SummaryColumns!.Count, s.SummaryRow!.Cells.Count));
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

    private static Transaction MakeDebitTransaction(Guid memberId, DateTime date, string description, decimal amount, Guid? feeId = null)
        => new()
        {
            Id = Guid.NewGuid(), MemberId = memberId, Date = date, Description = description,
            DebitAmount = amount, CreditAmount = 0m, GLAccount = "1200", FeeId = feeId, CreatedAt = DateTime.UtcNow
        };

    private static Transaction MakeCreditTransaction(Guid memberId, DateTime date, string description, decimal amount, Guid? feeId = null)
        => new()
        {
            Id = Guid.NewGuid(), MemberId = memberId, Date = date, Description = description,
            DebitAmount = 0m, CreditAmount = amount, GLAccount = "1200", FeeId = feeId, CreatedAt = DateTime.UtcNow
        };

    private void SetupOutstandingFees(Guid memberId, params OutstandingFee[] fees)
    {
        _balances.GetOutstandingFeesAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OutstandingFee>>(fees.ToList()));
    }

    private static Member MakeMember(Guid id, string name, bool isDeleted)
        => new()
        {
            Id = id, FirstName = name, StreetAddress = "1 Test St",
            Status = isDeleted ? MemberStatus.Inactive : MemberStatus.Active,
            IsDeleted = isDeleted,
            JoinDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

    private static OutstandingFee MakeOutstandingFee(decimal remaining, DateTime dueDate, DateTime? feeDate = null, Guid? feeId = null)
        => new()
        {
            FeeId = feeId ?? Guid.NewGuid(), FeeType = FeeType.Annual,
            FeeDate = feeDate ?? dueDate, DueDate = dueDate, RemainingAmount = remaining
        };

    private static ReportFilterValues CurrentYearFilters()
    {
        var f = new ReportFilterValues();
        f.Set("dateFrom", $"{DateTime.UtcNow.Year}-01-01");
        f.Set("dateTo", $"{DateTime.UtcNow.Year}-12-31");
        return f;
    }
}
