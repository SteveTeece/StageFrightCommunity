using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup.Tabs;

/// <summary>
/// bUnit tests for OpeningBalancesTab (US2) — hosts the shared OpeningBalanceEntryForm
/// covering existing eligible accounts plus this session's queued Chart of Accounts
/// entries (each a placeholder Account keyed by ClientId). Nothing here posts to the
/// ledger; OnChange reports the full entry set up for SetupWizard to queue whenever an
/// amount or the as-at date changes (issue #382 — no separate Queue button).
/// </summary>
public class OpeningBalancesTabTests : LocalizedTestContext
{
    private readonly IOpeningBalanceService _openingBalanceService = Substitute.For<IOpeningBalanceService>();

    private static readonly Guid ExistingAccountId = Guid.NewGuid();

    public OpeningBalancesTabTests()
    {
        Services.AddSingleton(_openingBalanceService);
        _openingBalanceService.GetOpeningBalanceAccountsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                new()
                {
                    Id = ExistingAccountId, Name = "Cash on Hand", Type = AccountType.Asset,
                    AccountNumber = "1100", IsSystem = true, IsBankAccount = true,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            });
        _openingBalanceService.ComputePlug(Arg.Any<IReadOnlyList<OpeningBalanceEntry>>(), Arg.Any<IReadOnlyList<Account>>())
            .Returns(ci => ci.ArgAt<IReadOnlyList<OpeningBalanceEntry>>(0).Sum(e => e.Amount));
    }

    private IRenderedComponent<OpeningBalancesTab> RenderTab(
        IReadOnlyList<QueuedAccountRequest>? queued = null,
        EventCallback<RecordOpeningBalancesRequest>? onChange = null) =>
        Render<OpeningBalancesTab>(p => p
            .Add(x => x.QueuedAccounts, queued ?? Array.Empty<QueuedAccountRequest>())
            .Add(x => x.OnChange, onChange ?? EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, _ => { })));

    [Fact]
    public void RendersOneRow_PerExistingAccount()
    {
        var cut = RenderTab();

        Assert.Contains("Cash on Hand", cut.Markup);
        Assert.Single(cut.FindAll(".opening-balance-amount"));
    }

    [Fact]
    public void RendersOneRow_PerQueuedAccount_Also()
    {
        var queued = new[] { new QueuedAccountRequest(Guid.NewGuid(), "Petty Cash", AccountType.Asset, true) };
        var cut = RenderTab(queued: queued);

        Assert.Contains("Cash on Hand", cut.Markup);
        Assert.Contains("Petty Cash", cut.Markup);
        Assert.Equal(2, cut.FindAll(".opening-balance-amount").Count);
    }

    [Fact]
    public void NeverShowsAlreadyPostedWarning()
    {
        // ShowAlreadyPostedWarning is hard-wired to false — first-run setup can never have
        // a prior OpeningBalance posting (research.md).
        var cut = RenderTab();

        Assert.Empty(cut.FindAll(".opening-balances-warning"));
    }

    [Fact]
    public void NegativeAmount_IsAccepted_UpdatesPlug()
    {
        var cut = RenderTab();

        cut.FindAll(".opening-balance-amount")[0].Change("-250");

        Assert.Contains("-250.00", cut.Find(".opening-balances-plug").TextContent);
    }

    [Fact]
    public void PlugRecalculates_AsAmountsChange()
    {
        var cut = RenderTab();

        cut.FindAll(".opening-balance-amount")[0].Change("500");

        Assert.Contains("500.00", cut.Find(".opening-balances-plug").TextContent);
    }

    [Fact]
    public void QueuedAccountAdded_GetsItsOwnRow_WhenParametersUpdate()
    {
        var cut = RenderTab();
        Assert.Single(cut.FindAll(".opening-balance-amount"));

        var queued = new[] { new QueuedAccountRequest(Guid.NewGuid(), "Grant Income", AccountType.Income, false) };
        cut.Render(p => p.Add(x => x.QueuedAccounts, queued));

        Assert.Contains("Grant Income", cut.Markup);
        Assert.Equal(2, cut.FindAll(".opening-balance-amount").Count);
    }

    [Fact]
    public void QueuedAccountRemoved_DropsItsRow_WhenParametersUpdate()
    {
        var queued = new[] { new QueuedAccountRequest(Guid.NewGuid(), "Grant Income", AccountType.Income, false) };
        var cut = RenderTab(queued: queued);
        Assert.Equal(2, cut.FindAll(".opening-balance-amount").Count);

        cut.Render(p => p.Add(x => x.QueuedAccounts, Array.Empty<QueuedAccountRequest>()));

        Assert.DoesNotContain("Grant Income", cut.Markup);
        Assert.Single(cut.FindAll(".opening-balance-amount"));
    }

    [Fact]
    public void Should_RenderNoQueueButton_When_TabShown()
    {
        // Issue #382: the separate "Queue Balances" click is gone — entries sync on change.
        var cut = RenderTab();

        Assert.Empty(cut.FindAll(".wizard-post"));
    }

    [Fact]
    public void Should_ReportBuiltRequest_When_AmountChanged()
    {
        RecordOpeningBalancesRequest? reported = null;
        var cut = RenderTab(onChange: EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, r => reported = r));

        cut.Find("#ob-tab-as-at-date").Change("2025-01-01");
        cut.FindAll(".opening-balance-amount")[0].Change("1000");

        Assert.NotNull(reported);
        Assert.Equal(new DateTime(2025, 1, 1), reported!.AsAtDate);
        Assert.Single(reported.Entries);
        Assert.Equal(ExistingAccountId, reported.Entries[0].AccountId);
        Assert.Equal(1000m, reported.Entries[0].Amount);
    }

    [Fact]
    public void Should_ReportEmptyEntries_When_OnlyAmountClearedToZero()
    {
        RecordOpeningBalancesRequest? reported = null;
        var cut = RenderTab(onChange: EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, r => reported = r));

        cut.FindAll(".opening-balance-amount")[0].Change("1000");
        cut.FindAll(".opening-balance-amount")[0].Change("");

        Assert.NotNull(reported);
        Assert.Empty(reported!.Entries);
    }

    [Fact]
    public void Should_ReReportEnteredBalances_UnderNewDate_When_AsAtDateChangedAfterEntry()
    {
        RecordOpeningBalancesRequest? reported = null;
        var cut = RenderTab(onChange: EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, r => reported = r));

        cut.FindAll(".opening-balance-amount")[0].Change("1000");
        cut.Find("#ob-tab-as-at-date").Change("2025-07-01");

        Assert.NotNull(reported);
        Assert.Equal(new DateTime(2025, 7, 1), reported!.AsAtDate);
        Assert.Single(reported.Entries);
        Assert.Equal(1000m, reported.Entries[0].Amount);
    }

    [Fact]
    public void Should_KeepDate_When_AsAtDateInputUnparseable()
    {
        RecordOpeningBalancesRequest? reported = null;
        var cut = RenderTab(onChange: EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, r => reported = r));

        cut.Find("#ob-tab-as-at-date").Change("not-a-date");

        Assert.NotNull(reported);
        Assert.Equal(DateTime.Today, reported!.AsAtDate);
        Assert.Empty(reported.Entries);
    }

    [Fact]
    public void Should_DropRemovedQueuedAccountEntry_When_AsAtDateChangedAfterRemoval()
    {
        var queuedId = Guid.NewGuid();
        var queued = new[] { new QueuedAccountRequest(queuedId, "Grant Income", AccountType.Income, false) };
        RecordOpeningBalancesRequest? reported = null;
        var cut = RenderTab(queued: queued,
            onChange: EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, r => reported = r));

        cut.FindAll(".opening-balance-amount")[0].Change("1000"); // Cash on Hand
        cut.FindAll(".opening-balance-amount")[1].Change("200");  // Grant Income
        cut.Render(p => p.Add(x => x.QueuedAccounts, Array.Empty<QueuedAccountRequest>()));
        cut.Find("#ob-tab-as-at-date").Change("2025-07-01");

        Assert.NotNull(reported);
        Assert.Single(reported!.Entries);
        Assert.Equal(ExistingAccountId, reported.Entries[0].AccountId);
    }

    [Fact]
    public void AsAtDate_DefaultsToToday()
    {
        var cut = RenderTab();

        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), cut.Find("#ob-tab-as-at-date").GetAttribute("value"));
    }
}
