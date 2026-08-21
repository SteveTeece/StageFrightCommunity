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
/// ledger; OnSubmit bubbles the built request up for SetupWizard to queue.
/// </summary>
public class OpeningBalancesTabTests : BunitContext
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
        EventCallback<RecordOpeningBalancesRequest>? onSubmit = null) =>
        Render<OpeningBalancesTab>(p => p
            .Add(x => x.QueuedAccounts, queued ?? Array.Empty<QueuedAccountRequest>())
            .Add(x => x.OnSubmit, onSubmit ?? EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, _ => { })));

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
    public void OnSubmit_ReceivesBuiltRequest_WithAsAtDateAndNonZeroEntries()
    {
        RecordOpeningBalancesRequest? submitted = null;
        var cut = RenderTab(onSubmit: EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, r => submitted = r));

        cut.Find("#ob-tab-as-at-date").Change("2025-01-01");
        cut.FindAll(".opening-balance-amount")[0].Change("1000");
        cut.Find(".wizard-post").Click();

        Assert.NotNull(submitted);
        Assert.Equal(new DateTime(2025, 1, 1), submitted!.AsAtDate);
        Assert.Single(submitted.Entries);
        Assert.Equal(ExistingAccountId, submitted.Entries[0].AccountId);
        Assert.Equal(1000m, submitted.Entries[0].Amount);
    }

    [Fact]
    public void AsAtDate_DefaultsToToday()
    {
        var cut = RenderTab();

        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), cut.Find("#ob-tab-as-at-date").GetAttribute("value"));
    }
}
