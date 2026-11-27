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
/// bUnit tests for OpeningBalancesPage — the read-only view of the opening balances recorded at
/// setup (issue #398): every account with its balance (zero where none), the as-at caption, the
/// journal-entry link for corrections, and no way to post.
/// </summary>
public class OpeningBalancesPageTests : LocalizedTestContext
{
    private readonly IOpeningBalanceService _openingService = Substitute.For<IOpeningBalanceService>();

    private static readonly Guid CashAccountId = Guid.NewGuid();
    private static readonly Guid LoanAccountId = Guid.NewGuid();

    public OpeningBalancesPageTests()
    {
        Services.AddSingleton(_openingService);
        _openingService.GetRecordedOpeningBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(Recorded(new DateTime(2026, 7, 1), cash: 5000m, loan: -200m, plug: 4800m));
    }

    [Fact]
    public void Should_ShowEveryAccountBalanceAndPlug_When_Loaded()
    {
        var cut = Render<OpeningBalancesPage>();

        var amounts = cut.FindAll(".opening-balance-recorded-amount").Select(a => a.TextContent).ToList();
        Assert.Equal([MoneyFormatter.Format(5000m), MoneyFormatter.Format(-200m)], amounts);
        Assert.Contains("Petty Cash", cut.Markup);
        Assert.Contains("Committee Loan", cut.Markup);
        Assert.Equal(MoneyFormatter.Format(4800m), cut.Find(".opening-balances-plug").TextContent);
    }

    [Fact]
    public void Should_ShowAsAtDateAndLockedCaption_When_BalancesRecorded()
    {
        var cut = Render<OpeningBalancesPage>();

        var caption = cut.Find(".opening-balances-locked").TextContent;
        Assert.Contains("1 July 2026", caption);
        Assert.Contains("locked", caption);
    }

    [Fact]
    public void Should_ShowZeroBalances_When_RecordedAsZero()
    {
        _openingService.GetRecordedOpeningBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(Recorded(new DateTime(2026, 7, 1), cash: 0m, loan: 0m, plug: 0m));

        var cut = Render<OpeningBalancesPage>();

        Assert.All(cut.FindAll(".opening-balance-recorded-amount"),
            a => Assert.Equal(MoneyFormatter.Format(0m), a.TextContent));
        Assert.Contains("1 July 2026", cut.Find(".opening-balances-locked").TextContent);
    }

    [Fact]
    public void Should_SayEveryAccountOpenedAtZero_When_NoneEverRecorded()
    {
        _openingService.GetRecordedOpeningBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(Recorded(asAt: null, cash: 0m, loan: 0m, plug: 0m));

        var cut = Render<OpeningBalancesPage>();

        Assert.Contains("opened at zero", cut.Find(".opening-balances-locked").TextContent);
    }

    [Fact]
    public void Should_OfferNoWayToPost_When_Loaded()
    {
        var cut = Render<OpeningBalancesPage>();

        Assert.Empty(cut.FindAll("input"));
        Assert.Empty(cut.FindAll(".wizard-post"));
        Assert.Empty(cut.FindAll(".wizard-next"));
        _openingService.DidNotReceive().RecordOpeningBalancesAsync(
            Arg.Any<RecordOpeningBalancesRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Should_NavigateToJournalEntry_When_JournalLinkClicked()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = Render<OpeningBalancesPage>();

        cut.Find(".opening-balances-journal-link").Click();

        Assert.EndsWith("/finance/journal", nav.Uri);
    }

    [Fact]
    public void Should_ShowErrorAlert_When_LoadFails()
    {
        _openingService.GetRecordedOpeningBalancesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<RecordedOpeningBalances>>(_ => throw new InvalidOperationException("boom"));

        var cut = Render<OpeningBalancesPage>();

        Assert.Contains("Failed to load accounts", cut.Find(".alert-danger").TextContent);
        Assert.Empty(cut.FindAll(".opening-balances-recorded"));
    }

    private static RecordedOpeningBalances Recorded(DateTime? asAt, decimal cash, decimal loan, decimal plug) => new()
    {
        AsAtDate = asAt,
        Lines =
        [
            new RecordedOpeningBalanceLine
            {
                Account = new Account { Id = CashAccountId, Name = "Petty Cash", Type = AccountType.Asset, AccountNumber = "1100" },
                Amount = cash
            },
            new RecordedOpeningBalanceLine
            {
                Account = new Account { Id = LoanAccountId, Name = "Committee Loan", Type = AccountType.Liability, AccountNumber = "2000" },
                Amount = loan
            }
        ],
        Plug = plug
    };
}
