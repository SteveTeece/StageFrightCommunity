using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Reports;

namespace StageFright.UI.Tests.Pages.Reports;

/// <summary>
/// bUnit tests for BankDepositsReportView — the Reports → Bank Deposits on-screen body (issue #399):
/// deposits only, newest first as the service returns them, each linking to its report page, plus
/// the empty and load-error states.
/// </summary>
public class BankDepositsReportViewTests : LocalizedTestContext
{
    private readonly IBankingService _banking = Substitute.For<IBankingService>();

    public BankDepositsReportViewTests()
    {
        Services.AddSingleton(_banking);
    }

    private static BankingHistoryEntry Entry(BankingHistoryEntryType type, DateTime date, string? description, decimal total,
        bool reversed = false) => new()
    {
        Id = Guid.NewGuid(), Type = type, Date = date, Description = description,
        BankAccountName = "Community Bank", Total = total, IsReversed = reversed
    };

    [Fact]
    public void Should_ListDepositsWithLinksAndSkipWithdrawals_When_Rendered()
    {
        var newest = Entry(BankingHistoryEntryType.Deposit, new DateTime(2026, 5, 10), "May takings", 250m);
        var older = Entry(BankingHistoryEntryType.Deposit, new DateTime(2026, 4, 2), "April takings", 90m, reversed: true);
        var withdrawal = Entry(BankingHistoryEntryType.Withdrawal, new DateTime(2026, 4, 20), "Float", 100m);
        _banking.GetHistoryAsync(Arg.Any<CancellationToken>()).Returns(new List<BankingHistoryEntry> { newest, withdrawal, older });

        var cut = Render<BankDepositsReportView>();

        var links = cut.FindAll("a.deposit-link");
        Assert.Equal(["May takings", "April takings"], links.Select(a => a.TextContent.Trim()));
        Assert.Equal($"/reports/bank-deposits/{newest.Id}", links[0].GetAttribute("href"));
        Assert.Equal($"/reports/bank-deposits/{older.Id}", links[1].GetAttribute("href"));
        Assert.DoesNotContain("Float", cut.Markup);
        Assert.Contains(MoneyFormatter.Format(250m), cut.Markup);
        Assert.Contains("Reversed", cut.Markup);
        Assert.Contains("Recorded", cut.Markup);
    }

    [Fact]
    public void Should_LinkWithDefaultText_When_DepositHasNoDescription()
    {
        _banking.GetHistoryAsync(Arg.Any<CancellationToken>()).Returns(new List<BankingHistoryEntry>
        {
            Entry(BankingHistoryEntryType.Deposit, new DateTime(2026, 5, 10), null, 10m)
        });

        var cut = Render<BankDepositsReportView>();

        Assert.Equal("Bank deposit", cut.Find("a.deposit-link").TextContent.Trim());
    }

    [Fact]
    public void Should_ShowEmptyMessage_When_NoDepositsRecorded()
    {
        _banking.GetHistoryAsync(Arg.Any<CancellationToken>()).Returns(new List<BankingHistoryEntry>
        {
            Entry(BankingHistoryEntryType.Withdrawal, new DateTime(2026, 4, 20), "Float", 100m)
        });

        var cut = Render<BankDepositsReportView>();

        Assert.Equal("No deposits have been recorded.", cut.Find("#noDeposits").TextContent);
        Assert.Empty(cut.FindAll("a.deposit-link"));
    }

    [Fact]
    public void Should_ShowError_When_HistoryFailsToLoad()
    {
        _banking.GetHistoryAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<BankingHistoryEntry>>(new InvalidOperationException("boom")));

        var cut = Render<BankDepositsReportView>();

        Assert.Contains("InvalidOperationException", cut.Find("[role=alert]").TextContent);
        Assert.Empty(cut.FindAll("a.deposit-link"));
    }
}
