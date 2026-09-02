using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Shared;

namespace StageFright.UI.Tests.Shared;

/// <summary>
/// bUnit tests for OpeningBalanceEntryForm — the shared opening-balance entry experience
/// (FR-017/FR-019) used both by the standalone Opening Balances page (immediate-post) and
/// the setup wizard's Opening Balances tab (deferred-queue). Folds the standalone page's
/// former Step 2 (enter) and Step 3 (confirm) into one entry table + submit action.
/// </summary>
public class OpeningBalanceEntryFormTests : LocalizedTestContext
{
    private readonly IOpeningBalanceService _openingBalanceService = Substitute.For<IOpeningBalanceService>();
    private static readonly Account BankAccount = new()
    {
        Id = Guid.NewGuid(), Name = "Bank", AccountNumber = "1200", Type = AccountType.Asset
    };

    public OpeningBalanceEntryFormTests()
    {
        Services.AddSingleton(_openingBalanceService);
        _openingBalanceService.ComputePlug(Arg.Any<IReadOnlyList<OpeningBalanceEntry>>(), Arg.Any<IReadOnlyList<Account>>())
            .Returns(ci => ci.ArgAt<IReadOnlyList<OpeningBalanceEntry>>(0).Sum(e => e.Amount));
    }

    [Fact]
    public void RendersOneRow_PerAccount()
    {
        var cut = Render<OpeningBalanceEntryForm>(p => p
            .Add(x => x.Accounts, new[] { BankAccount })
            .Add(x => x.AsAtDate, DateTime.Today)
            .Add(x => x.OnSubmit, EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, _ => { })));

        Assert.Contains("Bank", cut.Markup);
        Assert.Contains("1200", cut.Markup);
    }

    [Fact]
    public async Task NegativeAmount_IsAccepted()
    {
        RecordOpeningBalancesRequest? submitted = null;
        var cut = Render<OpeningBalanceEntryForm>(p => p
            .Add(x => x.Accounts, new[] { BankAccount })
            .Add(x => x.AsAtDate, DateTime.Today)
            .Add(x => x.OnSubmit, EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, r => submitted = r)));

        cut.Find(".opening-balance-amount").Change("-150.00");
        await cut.Find(".wizard-post").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.NotNull(submitted);
        Assert.Equal(-150.00m, submitted!.Entries.Single().Amount);
    }

    [Fact]
    public void Plug_RecalculatesLive_AsAmountsChange()
    {
        var cut = Render<OpeningBalanceEntryForm>(p => p
            .Add(x => x.Accounts, new[] { BankAccount })
            .Add(x => x.AsAtDate, DateTime.Today)
            .Add(x => x.OnSubmit, EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, _ => { })));

        cut.Find(".opening-balance-amount").Change("200.00");

        Assert.Contains("200.00", cut.Find(".opening-balances-plug").TextContent);
    }

    [Fact]
    public void AlreadyPostedWarning_Hidden_WhenShowAlreadyPostedWarningIsFalse()
    {
        var cut = Render<OpeningBalanceEntryForm>(p => p
            .Add(x => x.Accounts, new[] { BankAccount })
            .Add(x => x.AsAtDate, DateTime.Today)
            .Add(x => x.OnSubmit, EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, _ => { }))
            .Add(x => x.ShowAlreadyPostedWarning, false)
            .Add(x => x.HasExistingOpeningBalances, true));

        Assert.Empty(cut.FindAll(".opening-balances-warning"));
    }

    [Fact]
    public void AlreadyPostedWarning_Shown_WhenBothFlagsTrue()
    {
        var cut = Render<OpeningBalanceEntryForm>(p => p
            .Add(x => x.Accounts, new[] { BankAccount })
            .Add(x => x.AsAtDate, DateTime.Today)
            .Add(x => x.OnSubmit, EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, _ => { }))
            .Add(x => x.ShowAlreadyPostedWarning, true)
            .Add(x => x.HasExistingOpeningBalances, true));

        cut.Find(".opening-balances-warning");
    }

    [Fact]
    public async Task OnSubmit_ReceivesBuiltRequest_WithAsAtDateAndNonZeroEntriesOnly()
    {
        var secondAccount = new Account { Id = Guid.NewGuid(), Name = "Petty Cash", AccountNumber = "1100", Type = AccountType.Asset };
        RecordOpeningBalancesRequest? submitted = null;
        var asAtDate = new DateTime(2026, 7, 1);
        var cut = Render<OpeningBalanceEntryForm>(p => p
            .Add(x => x.Accounts, new[] { BankAccount, secondAccount })
            .Add(x => x.AsAtDate, asAtDate)
            .Add(x => x.OnSubmit, EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, r => submitted = r)));

        cut.FindAll(".opening-balance-amount")[0].Change("500.00");
        // Second account left at zero — must be excluded from Entries.
        await cut.Find(".wizard-post").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.NotNull(submitted);
        Assert.Equal(asAtDate, submitted!.AsAtDate);
        Assert.Single(submitted.Entries);
        Assert.Equal(BankAccount.Id, submitted.Entries[0].AccountId);
    }

    [Fact]
    public void SubmitDisabled_WhenNoNonZeroEntries()
    {
        var cut = Render<OpeningBalanceEntryForm>(p => p
            .Add(x => x.Accounts, new[] { BankAccount })
            .Add(x => x.AsAtDate, DateTime.Today)
            .Add(x => x.OnSubmit, EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, _ => { })));

        Assert.True(cut.Find(".wizard-post").HasAttribute("disabled"));
    }
}
