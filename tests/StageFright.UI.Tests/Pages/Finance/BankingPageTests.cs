using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Finance;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for BankingPage (spec 033 US1): summary chips, the awaiting-deposit grid and its total,
/// the no-bank-account message, the legacy route and the history grid.
/// </summary>
public class BankingPageTests : LocalizedTestContext
{
    private readonly IBankingService _banking = Substitute.For<IBankingService>();
    private static readonly Guid DepositId = Guid.NewGuid();

    public BankingPageTests()
    {
        Services.AddSingleton(_banking);
        SetOverview();
        _banking.GetHistoryAsync(Arg.Any<CancellationToken>()).Returns(new List<BankingHistoryEntry>());
    }

    private void SetOverview(decimal pettyCash = 40m, IReadOnlyList<Account>? banks = null, params AwaitingDepositItem[] items)
    {
        _banking.GetOverviewAsync(Arg.Any<CancellationToken>()).Returns(new BankingOverview
        {
            AwaitingItems = items,
            UndepositedTotal = items.Sum(i => i.Amount),
            PettyCashBalance = pettyCash,
            CashAvailableToday = Math.Max(pettyCash, 0m),
            DepositBankAccounts = banks ?? [new Account
            {
                Id = Guid.NewGuid(), Name = "Community Bank", AccountNumber = "1110", Type = AccountType.Asset,
                IsBankAccount = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }]
        });
    }

    private static AwaitingDepositItem Item(string payer, string? reference, decimal amount, int day) => new()
    {
        Id = Guid.NewGuid(), ReceivedDate = new DateTime(2026, 3, day), Source = ReceivedItemSource.MemberPayment,
        PayerName = payer, Reference = reference, Amount = amount
    };

    [Fact]
    public void Should_ShowAwaitingRowsAndTotal_When_ChequesAreAwaiting()
    {
        SetOverview(40m, null, Item("Alice Smith", "CHQ 101", 50m, 3), Item("Town Hall", null, 120m, 5));

        var cut = Render<BankingPage>();

        Assert.Contains("Alice Smith", cut.Markup);
        Assert.Contains("CHQ 101", cut.Markup);
        Assert.Contains("Town Hall", cut.Markup);
        Assert.Contains(MoneyFormatter.Format(50m), cut.Markup);
        Assert.Equal(MoneyFormatter.Format(170m), cut.Find("#awaitingTotal").TextContent);
        Assert.Equal(MoneyFormatter.Format(170m), cut.Find("#undepositedTotal").TextContent);
    }

    [Fact]
    public void Should_ShowAwaitingColumnHeadings_When_ChequesAreAwaiting()
    {
        SetOverview(40m, null, Item("Alice Smith", "CHQ 101", 50m, 3));

        var cut = Render<BankingPage>();

        foreach (var heading in new[] { "Received", "Payer", "Reference", "Amount" })
            Assert.Contains(heading, cut.Markup);
    }

    [Fact]
    public void Should_ShowNothingAwaitingMessage_When_NoItemsAwait()
    {
        var cut = Render<BankingPage>();

        Assert.Contains("No cheques or other items are awaiting deposit.", cut.Markup);
    }

    [Fact]
    public void Should_ShowOpeningBalanceLabel_When_ItemHasNoPayer()
    {
        SetOverview(0m, null, new AwaitingDepositItem
        {
            Id = Guid.NewGuid(), ReceivedDate = new DateTime(2026, 1, 1),
            Source = ReceivedItemSource.OpeningBalance, Amount = 300m
        });

        var cut = Render<BankingPage>();

        Assert.Contains("Opening balance", cut.Markup);
    }

    [Fact]
    public void Should_ShowPettyCashChip_When_Rendered()
    {
        var cut = Render<BankingPage>();

        Assert.Equal(MoneyFormatter.Format(40m), cut.Find("#pettyCashBalance").TextContent);
        Assert.Contains("Petty Cash", cut.Markup);
    }

    [Fact]
    public void Should_ShowNegativePettyCash_When_BalanceIsNegative()
    {
        SetOverview(-25m);

        var cut = Render<BankingPage>();

        Assert.Equal(MoneyFormatter.Format(-25m), cut.Find("#pettyCashBalance").TextContent);
        Assert.StartsWith("-", cut.Find("#pettyCashBalance").TextContent.Trim());
    }

    [Fact]
    public void Should_ShowNoBankMessageLinkingToAccounts_When_NoBankAccountExists()
    {
        SetOverview(40m, []);

        var cut = Render<BankingPage>();

        Assert.Contains("/finance/accounts", cut.Markup);
        Assert.Empty(cut.FindAll("#recordDeposit"));
        Assert.Empty(cut.FindAll("#recordWithdrawal"));
    }

    [Fact]
    public void Should_ShowActionButtons_When_BankAccountExists()
    {
        var cut = Render<BankingPage>();

        Assert.NotEmpty(cut.FindAll("#recordDeposit"));
        Assert.NotEmpty(cut.FindAll("#recordWithdrawal"));
        Assert.DoesNotContain("/finance/accounts", cut.Markup);
    }

    [Fact]
    public void Should_NavigateToDepositAndWithdrawal_When_ButtonsClicked()
    {
        var cut = Render<BankingPage>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find("#recordDeposit").Click();
        Assert.EndsWith("/finance/banking/deposit", nav.Uri);

        cut.Find("#recordWithdrawal").Click();
        Assert.EndsWith("/finance/banking/withdrawal", nav.Uri);
    }

    [Fact]
    public void Should_RenderBankingPage_When_LegacyRouteVisited()
    {
        var routes = typeof(BankingPage).GetCustomAttributes(typeof(RouteAttribute), false)
            .Cast<RouteAttribute>().Select(r => r.Template).ToList();

        Assert.Contains("/finance/banking", routes);
        Assert.Contains("/finance/bank-deposit", routes);
    }

    [Fact]
    public void Should_ListHistoryEntriesWithStatusAndLink_When_EntriesExist()
    {
        _banking.GetHistoryAsync(Arg.Any<CancellationToken>()).Returns(new List<BankingHistoryEntry>
        {
            new() { Id = DepositId, Type = BankingHistoryEntryType.Deposit, Date = new DateTime(2026, 3, 9),
                    BankAccountName = "Community Bank", Total = 220m, IsReversed = true },
            new() { Id = Guid.NewGuid(), Type = BankingHistoryEntryType.Withdrawal, Date = new DateTime(2026, 3, 1),
                    BankAccountName = "Community Bank", Total = 60m }
        });

        var cut = Render<BankingPage>();

        Assert.Contains("Deposit", cut.Markup);
        Assert.Contains("Withdrawal", cut.Markup);
        Assert.Contains("Reversed", cut.Markup);
        Assert.Contains(MoneyFormatter.Format(220m), cut.Markup);
        Assert.Single(cut.FindAll($"a[href='/finance/banking/deposits/{DepositId}']"));
    }

    [Fact]
    public void Should_ShowNoHistoryMessage_When_NothingRecorded()
    {
        var cut = Render<BankingPage>();

        Assert.Contains("No deposits or withdrawals have been recorded.", cut.Markup);
    }

    [Fact]
    public void Should_ShowTranslatedLoadError_When_OverviewFailsToLoad()
    {
        _banking.GetOverviewAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BankingOverview>(
                new StageFright.Core.Exceptions.DataAccessException("disk unavailable", "Account", "GetOverview")));

        var cut = Render<BankingPage>();

        Assert.Equal("Failed to load banking: disk unavailable", cut.Find("[role=alert]").TextContent.Trim());
    }

    [Fact]
    public void Should_ShowLoggedErrorTextNotExceptionText_When_OverviewFailsWithFrameworkException()
    {
        _banking.GetOverviewAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BankingOverview>(new InvalidOperationException("SQLite Error 1: no such table")));

        var cut = Render<BankingPage>();

        Assert.Equal("Failed to load banking: an unexpected error occurred. The details have been logged.",
            cut.Find("[role=alert]").TextContent.Trim());
    }
}
