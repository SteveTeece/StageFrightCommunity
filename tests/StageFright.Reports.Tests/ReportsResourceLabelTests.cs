using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Members;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Tests;

/// <summary>
/// T030 / FR-006: every report's name, column headers, section headings and subtotal/total
/// labels are sourced from <see cref="ReportsResource"/> — not hardcoded — and resolve to a
/// real neutral (en-AU) value rather than a missing-key echo. Assertions reference the
/// resource key via <see cref="ILocalizer"/> (FR-018), never a literal English string.
/// </summary>
public class ReportsResourceLabelTests
{
    private static readonly ILocalizer L = RealLocalizer.Instance;

    // --- Report names come from ReportsResource ---

    [Theory]
    [InlineData("Reports_MemberList_Name")]
    [InlineData("Reports_Committee_Name")]
    [InlineData("Reports_IncomeStatement_Name")]
    [InlineData("Reports_TrialBalance_Name")]
    [InlineData("Reports_AccountRegister_Name")]
    [InlineData("Reports_BalanceSheet_Name")]
    [InlineData("Reports_BankReconciliation_Name")]
    [InlineData("Reports_ChartOfAccounts_Name")]
    [InlineData("Reports_GeneralLedger_Name")]
    [InlineData("Reports_MemberAccountSummary_Name")]
    [InlineData("Reports_TaxSummary_Name")]
    public void Should_ResolveToNeutralValue_When_ReportNameKeyLookedUp(string key)
    {
        var value = L.Get<ReportsResource>(key);

        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.NotEqual(key, value); // a missing key echoes the key name back
    }

    // --- T040 / FR-006: PDF renderer chrome (page furniture + print-only sheet headings) ---

    [Theory]
    [InlineData("Reports_Render_GeneratedAt")]
    [InlineData("Reports_Render_PagePrefix")]
    [InlineData("Reports_Render_PageSeparator")]
    [InlineData("Reports_AttendanceRoll_Title")]
    [InlineData("Reports_AttendanceRoll_DateLine")]
    [InlineData("Reports_AttendanceRoll_NameColumn")]
    [InlineData("Reports_AttendanceRoll_PresentColumn")]
    [InlineData("Reports_AttendanceRoll_FeePaidColumn")]
    [InlineData("Reports_AgmResults_Title")]
    [InlineData("Reports_AgmResults_MeetingDateLine")]
    [InlineData("Reports_AgmResults_AttendanceLine")]
    [InlineData("Reports_AgmResults_ElectedPositionsHeading")]
    [InlineData("Reports_AgmResults_NoPositions")]
    [InlineData("Reports_AgmResults_PositionLabel")]
    [InlineData("Reports_AgmResults_GeneralCommitteeMemberLabel")]
    [InlineData("Reports_Sheet_NameColumn")]
    [InlineData("Reports_EventSheet_Title")]
    [InlineData("Reports_EventSheet_DateLine")]
    [InlineData("Reports_EventSheet_ParticipatedColumn")]
    [InlineData("Reports_AgmSheet_Title")]
    [InlineData("Reports_AgmSheet_AttendedColumn")]
    public void Should_ResolveToNeutralValue_When_RendererChromeKeyLookedUp(string key)
    {
        var value = L.Get<ReportsResource>(key);

        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.NotEqual(key, value); // a missing key echoes the key name back
    }

    [Fact]
    public void Should_SubstituteNamedPlaceholders_When_RendererChromeFormatKeysResolved()
    {
        Assert.Equal("Generated: 5 August 2026 14:30 UTC",
            L.Get<ReportsResource>("Reports_Render_GeneratedAt", "5 August 2026 14:30"));
        Assert.Equal("Rehearsal: 5 August 2026 at 19:00",
            L.Get<ReportsResource>("Reports_AttendanceRoll_DateLine", "5 August 2026", "19:00"));
        Assert.Equal("Attendance: 3 of 5 members attended",
            L.Get<ReportsResource>("Reports_AgmResults_AttendanceLine", 3, 5));
        Assert.Equal("Performance: 20 August 2026",
            L.Get<ReportsResource>("Reports_EventSheet_DateLine", "Performance", "20 August 2026"));
    }

    [Fact]
    public async Task Should_SourceReportNameFromResources_When_MemberListProviderQueried()
    {
        var members = Substitute.For<IMemberRepository>();
        members.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Member>>([]));
        var sut = new MemberListReportProvider(members, new AgeCalculationService(L), L);

        Assert.Equal(L.Get<ReportsResource>("Reports_MemberList_Name"), sut.ReportName);

        var data = await sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);
        Assert.Equal(L.Get<ReportsResource>("Reports_MemberList_Name"), data.Title);
    }

    // --- Column headers, section headings and total labels come from ReportsResource ---

    [Fact]
    public async Task Should_SourceColumnHeadersFromResources_When_TrialBalanceGenerated()
    {
        var data = await GenerateTrialBalance();

        Assert.Equal(
            new[]
            {
                L.Get<ReportsResource>("Reports_Column_Account"),
                L.Get<ReportsResource>("Reports_Column_Debit"),
                L.Get<ReportsResource>("Reports_Column_Credit")
            },
            data.Columns.Select(c => c.Header));
    }

    [Fact]
    public async Task Should_SourceSectionHeadingsFromResources_When_TrialBalanceGenerated()
    {
        var data = await GenerateTrialBalance();

        Assert.Equal(
            new[]
            {
                L.Get<ReportsResource>("Reports_Section_Assets"),
                L.Get<ReportsResource>("Reports_Section_Liabilities"),
                L.Get<ReportsResource>("Reports_Section_Equity"),
                L.Get<ReportsResource>("Reports_Section_Income"),
                L.Get<ReportsResource>("Reports_Section_Expenses")
            },
            data.Sections.Select(s => s.Heading));
    }

    [Fact]
    public async Task Should_SourceGrandTotalLabelFromResources_When_TrialBalanceGenerated()
    {
        var data = await GenerateTrialBalance();

        Assert.NotNull(data.GrandTotal);
        Assert.Equal(L.Get<ReportsResource>("Reports_TrialBalance_TotalsRow"), data.GrandTotal!.Cells[0]);
    }

    [Fact]
    public async Task Should_SourceSectionAndSubtotalLabelsFromResources_When_IncomeStatementGenerated()
    {
        var data = await GenerateIncomeStatement();

        Assert.Equal(L.Get<ReportsResource>("Reports_Section_Income"), data.Sections[0].Heading);
        Assert.Equal(L.Get<ReportsResource>("Reports_Section_Expenses"), data.Sections[1].Heading);
        Assert.Equal(L.Get<ReportsResource>("Reports_IncomeStatement_TotalIncome"), data.Sections[0].Subtotal!.Cells[0]);
        Assert.Equal(L.Get<ReportsResource>("Reports_IncomeStatement_TotalExpenses"), data.Sections[1].Subtotal!.Cells[0]);
        Assert.Equal(L.Get<ReportsResource>("Reports_IncomeStatement_Surplus"), data.GrandTotal!.Cells[0]);
    }

    // --- Nothing generated leaks a raw key or a blank label ---

    [Fact]
    public async Task Should_NotLeaveRawKeyOrBlankLabel_When_RepresentativeReportsGenerated()
    {
        var reports = new[]
        {
            await GenerateTrialBalance(),
            await GenerateIncomeStatement()
        };

        foreach (var data in reports)
        {
            AssertResolvedLabel(data.Title);
            foreach (var col in data.Columns)
                AssertResolvedLabel(col.Header);
            foreach (var section in data.Sections)
            {
                if (section.Heading is not null)
                    AssertResolvedLabel(section.Heading);
                if (section.Subtotal is { Cells.Count: > 0 })
                    AssertResolvedLabel(section.Subtotal.Cells[0]);
            }
            if (data.GrandTotal is { Cells.Count: > 0 })
                AssertResolvedLabel(data.GrandTotal.Cells[0]);
        }
    }

    private static void AssertResolvedLabel(string label)
    {
        Assert.False(string.IsNullOrWhiteSpace(label));
        Assert.DoesNotContain("Reports_", label, StringComparison.Ordinal);
    }

    // --- Helpers ---

    private static async Task<ReportData> GenerateTrialBalance()
    {
        var gl = Substitute.For<IGLRepository>();
        var accounts = Substitute.For<IAccountRepository>();
        var settings = Substitute.For<ISettingsRepository>();
        gl.GetBalanceTotalsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((0m, 0m));
        accounts.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Account>>([]));
        accounts.GetArchivedAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Account>>([]));
        gl.GetAccountMovementsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<Guid, (decimal Debits, decimal Credits)>>(
                new Dictionary<Guid, (decimal, decimal)>()));

        var sut = new TrialBalanceReportProvider(gl, accounts, settings, L);
        return await sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);
    }

    private static async Task<ReportData> GenerateIncomeStatement()
    {
        var gl = Substitute.For<IGLRepository>();
        var accounts = Substitute.For<IAccountRepository>();
        var settings = Substitute.For<ISettingsRepository>();
        accounts.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Account>>([]));
        accounts.GetArchivedAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Account>>([]));
        gl.GetAccountMovementsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<Guid, (decimal Debits, decimal Credits)>>(
                new Dictionary<Guid, (decimal, decimal)>()));

        var sut = new IncomeStatementReportProvider(gl, accounts, settings, L);
        return await sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);
    }
}
