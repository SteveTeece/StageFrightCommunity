using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup.Tabs;

/// <summary>
/// bUnit tests for ChartOfAccountsTab (US4) — hosts the shared AddAccountForm in queuing
/// mode (OnAdd instead of an immediate IAccountService.CreateAsync) plus a BorderedListBox
/// of what's queued so far. Nothing here calls CreateAsync (FR-013); IAccountService is
/// only used to seed the duplicate-check name set from real accounts.
/// </summary>
public class ChartOfAccountsTabTests : BunitContext
{
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();

    public ChartOfAccountsTabTests()
    {
        Services.AddSingleton(_accountService);
        _accountService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Account>
        {
            new() { Id = Guid.NewGuid(), Name = "Cash on Hand", Type = AccountType.Asset, AccountNumber = "1100", IsSystem = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        });
        _accountService.GetArchivedAsync(Arg.Any<CancellationToken>()).Returns(new List<Account>());
    }

    private IRenderedComponent<ChartOfAccountsTab> RenderTab(
        IReadOnlyList<QueuedAccountRequest>? queued = null,
        EventCallback<QueuedAccountRequest>? onAdd = null,
        EventCallback<QueuedAccountRequest>? onRemove = null) =>
        Render<ChartOfAccountsTab>(p => p
            .Add(x => x.QueuedAccounts, queued ?? Array.Empty<QueuedAccountRequest>())
            .Add(x => x.OnAdd, onAdd ?? EventCallback.Factory.Create<QueuedAccountRequest>(this, _ => { }))
            .Add(x => x.OnRemove, onRemove ?? EventCallback.Factory.Create<QueuedAccountRequest>(this, _ => { })));

    [Fact]
    public async Task ValidSubmit_InvokesOnAdd_WithQueuedAccount()
    {
        QueuedAccountRequest? added = null;
        var cut = RenderTab(onAdd: EventCallback.Factory.Create<QueuedAccountRequest>(this, a => added = a));

        cut.Find("#account-name").Change("Petty Cash");
        cut.Find("#account-type").Change(nameof(AccountType.Asset));
        cut.Find("#account-is-bank").Change(true);
        await cut.Find("form").SubmitAsync();

        Assert.NotNull(added);
        Assert.Equal("Petty Cash", added!.Name);
        Assert.Equal(AccountType.Asset, added.Type);
        Assert.True(added.IsBankAccount);
        Assert.NotEqual(Guid.Empty, added.ClientId);
    }

    [Fact]
    public async Task BlankName_IsRejected_OnAddNotInvoked()
    {
        var invoked = false;
        var cut = RenderTab(onAdd: EventCallback.Factory.Create<QueuedAccountRequest>(this, _ => invoked = true));

        await cut.Find("form").SubmitAsync();

        Assert.False(invoked);
    }

    [Fact]
    public async Task DuplicateName_AgainstRealAccount_IsRejected_CaseInsensitively()
    {
        var invoked = false;
        var cut = RenderTab(onAdd: EventCallback.Factory.Create<QueuedAccountRequest>(this, _ => invoked = true));

        cut.Find("#account-name").Change("cash on hand");
        await cut.Find("form").SubmitAsync();

        Assert.False(invoked);
        Assert.Contains("already exists", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateName_AgainstAlreadyQueuedAccount_IsRejected_CaseInsensitively()
    {
        var invoked = false;
        var queued = new[] { new QueuedAccountRequest(Guid.NewGuid(), "Petty Cash", AccountType.Asset, false) };
        var cut = RenderTab(queued: queued, onAdd: EventCallback.Factory.Create<QueuedAccountRequest>(this, _ => invoked = true));

        cut.Find("#account-name").Change("petty cash");
        await cut.Find("form").SubmitAsync();

        Assert.False(invoked);
        Assert.Contains("already exists", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BankFlagCheckbox_OnlyShown_ForAssetType()
    {
        var cut = RenderTab();

        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#account-is-bank"));

        cut.Find("#account-type").Change(nameof(AccountType.Asset));
        cut.Find("#account-is-bank");
    }

    [Fact]
    public void QueuedAccounts_RenderInBorderedListBox()
    {
        var queued = new[]
        {
            new QueuedAccountRequest(Guid.NewGuid(), "Petty Cash", AccountType.Asset, true),
            new QueuedAccountRequest(Guid.NewGuid(), "Grant Income", AccountType.Income, false)
        };
        var cut = RenderTab(queued: queued);

        Assert.Contains("Petty Cash", cut.Markup);
        Assert.Contains("Bank/Cash", cut.Markup);
        Assert.Contains("Grant Income", cut.Markup);
    }

    [Fact]
    public void EmptyQueue_ShowsEmptyText()
    {
        var cut = RenderTab();

        Assert.Contains("No accounts queued yet.", cut.Markup);
    }

    [Fact]
    public void ClickingRemove_InvokesOnRemove_WithTheClickedAccount()
    {
        var toRemove = new QueuedAccountRequest(Guid.NewGuid(), "Petty Cash", AccountType.Asset, false);
        QueuedAccountRequest? removed = null;
        var cut = RenderTab(
            queued: new[] { toRemove },
            onRemove: EventCallback.Factory.Create<QueuedAccountRequest>(this, a => removed = a));

        cut.Find(".bordered-list-box-remove").Click();

        Assert.Equal(toRemove, removed);
    }

    [Fact]
    public void ExistingAccounts_RenderInReadOnlyBorderedListBox_WithSystemBadge_EvenWhenNothingQueued()
    {
        var cut = RenderTab();

        Assert.Contains("Existing Accounts", cut.Markup);
        Assert.Contains("Cash on Hand", cut.Markup);
        Assert.Contains("System", cut.Markup);
        // Read-only: no remove button for the existing-accounts list itself.
        Assert.Empty(cut.FindAll(".bordered-list-box-remove"));
    }
}
