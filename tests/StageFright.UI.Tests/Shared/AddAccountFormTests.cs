using Bunit;
using Microsoft.AspNetCore.Components;
using StageFright.Core.Enums;
using StageFright.UI.Shared;

namespace StageFright.UI.Tests.Shared;

/// <summary>
/// bUnit tests for AddAccountForm — the shared add-account experience (FR-012/FR-016)
/// used both by the standalone Chart of Accounts page (immediate-create) and the setup
/// wizard's Chart of Accounts tab (deferred-queue). The component owns validation
/// (blank/length via DataAnnotations, duplicate against ExistingNames); only what the
/// caller's OnSubmit does with a valid submit differs.
/// </summary>
public class AddAccountFormTests : LocalizedTestContext
{
    [Fact]
    public async Task ValidSubmit_InvokesOnSubmit_WithEnteredValues()
    {
        NewAccountModel? submitted = null;
        var cut = Render<AddAccountForm>(p => p
            .Add(x => x.OnSubmit, EventCallback.Factory.Create<NewAccountModel>(this, m => submitted = m)));

        cut.Find("#account-name").Change("Petty Cash");
        cut.Find("#account-type").Change(nameof(AccountType.Asset));
        await cut.Find("form").SubmitAsync();

        Assert.NotNull(submitted);
        Assert.Equal("Petty Cash", submitted!.Name);
        Assert.Equal(AccountType.Asset, submitted.Type);
    }

    [Fact]
    public async Task BlankName_IsRejected_OnSubmitNotInvoked()
    {
        var invoked = false;
        var cut = Render<AddAccountForm>(p => p
            .Add(x => x.OnSubmit, EventCallback.Factory.Create<NewAccountModel>(this, _ => invoked = true)));

        await cut.Find("form").SubmitAsync();

        Assert.False(invoked);
        Assert.Contains("required", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateName_IsRejected_CaseInsensitively()
    {
        var invoked = false;
        var cut = Render<AddAccountForm>(p => p
            .Add(x => x.OnSubmit, EventCallback.Factory.Create<NewAccountModel>(this, _ => invoked = true))
            .Add(x => x.ExistingNames, new[] { "Community Bank Account" }));

        cut.Find("#account-name").Change("community bank account");
        await cut.Find("form").SubmitAsync();

        Assert.False(invoked);
        Assert.Contains("already exists", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BankFlagCheckbox_OnlyShown_ForAssetType()
    {
        var cut = Render<AddAccountForm>(p => p
            .Add(x => x.OnSubmit, EventCallback.Factory.Create<NewAccountModel>(this, _ => { })));

        // Default type is Income — no bank-flag checkbox yet.
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#account-is-bank"));

        cut.Find("#account-type").Change(nameof(AccountType.Asset));
        cut.Find("#account-is-bank");
    }

    [Fact]
    public void SubmitButtonText_Renders_WhenSpecified()
    {
        var cut = Render<AddAccountForm>(p => p
            .Add(x => x.OnSubmit, EventCallback.Factory.Create<NewAccountModel>(this, _ => { }))
            .Add(x => x.SubmitButtonText, "Queue Account"));

        Assert.Contains("Queue Account", cut.Markup);
    }

    [Fact]
    public async Task FormClears_AfterSuccessfulSubmit()
    {
        var cut = Render<AddAccountForm>(p => p
            .Add(x => x.OnSubmit, EventCallback.Factory.Create<NewAccountModel>(this, _ => { })));

        cut.Find("#account-name").Change("Petty Cash");
        await cut.Find("form").SubmitAsync();

        Assert.Equal(string.Empty, cut.Find("#account-name").GetAttribute("value") ?? string.Empty);
    }
}
