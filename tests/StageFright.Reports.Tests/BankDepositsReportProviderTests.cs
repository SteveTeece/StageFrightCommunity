using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for BankDepositsReportProvider (issue #399): metadata, the empty state, deposits only
/// (withdrawals left out) in the service's newest-first order, and each row's date, description,
/// bank account, status and money-formatted total.
/// </summary>
public class BankDepositsReportProviderTests
{
    private readonly IBankingService _banking = Substitute.For<IBankingService>();
    private readonly BankDepositsReportProvider _sut;

    public BankDepositsReportProviderTests()
    {
        _sut = new BankDepositsReportProvider(_banking, RealLocalizer.Instance);
    }

    private void HistoryIs(params BankingHistoryEntry[] entries) =>
        _banking.GetHistoryAsync(Arg.Any<CancellationToken>()).Returns(entries.ToList());

    private static BankingHistoryEntry Deposit(DateTime date, string? description, decimal total, bool reversed = false) => new()
    {
        Id = Guid.NewGuid(), Type = BankingHistoryEntryType.Deposit, Date = date, Description = description,
        BankAccountName = "Community Bank", Total = total, IsReversed = reversed
    };

    [Fact]
    public void Should_DescribeFinanceReport_When_Created()
    {
        Assert.Equal("bank-deposits", _sut.ReportId);
        Assert.Equal("Bank Deposits", _sut.ReportName);
        Assert.Equal("Finance", _sut.ModuleName);
        Assert.Equal(55, _sut.DisplayOrder);
        Assert.Empty(_sut.Filters);
    }

    [Fact]
    public async Task Should_ShowNoneSubtitleAndNoSections_When_NoDepositsRecorded()
    {
        HistoryIs();

        var report = await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        Assert.Equal("Bank Deposits", report.Title);
        Assert.Equal("No deposits have been recorded.", report.SubTitle);
        Assert.Empty(report.Sections);
        Assert.Equal(["Date", "Description", "Bank account", "Status", "Deposit total"], report.Columns.Select(c => c.Header));
    }

    [Fact]
    public async Task Should_ListDepositsInHistoryOrderAndSkipWithdrawals_When_Generated()
    {
        var newest = Deposit(new DateTime(2026, 5, 10), "May takings", 250m);
        var older = Deposit(new DateTime(2026, 4, 2), "April takings", 1234.5m, reversed: true);
        var withdrawal = new BankingHistoryEntry
        {
            Id = Guid.NewGuid(), Type = BankingHistoryEntryType.Withdrawal, Date = new DateTime(2026, 4, 20),
            Description = "Float", BankAccountName = "Community Bank", Total = 100m
        };
        HistoryIs(newest, withdrawal, older);

        var report = await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        Assert.StartsWith("All deposits, newest first", report.SubTitle);
        var rows = Assert.Single(report.Sections).Rows;
        Assert.Equal(2, rows.Count);
        Assert.Equal(["2026-05-10", "May takings", "Community Bank", "Recorded", MoneyFormatter.Format(250m)], rows[0].Cells);
        Assert.Equal(["2026-04-02", "April takings", "Community Bank", "Reversed", MoneyFormatter.Format(1234.5m)], rows[1].Cells);
        Assert.Null(report.GrandTotal);
    }

    [Fact]
    public async Task Should_LeaveDescriptionBlank_When_DepositHasNone()
    {
        HistoryIs(Deposit(new DateTime(2026, 5, 10), null, 10m));

        var report = await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, report.Sections[0].Rows[0].Cells[1]);
    }
}
