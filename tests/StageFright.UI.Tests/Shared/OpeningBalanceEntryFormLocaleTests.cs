using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Shared;

namespace StageFright.UI.Tests.Shared;

/// <summary>
/// bUnit locale-safety tests for OpeningBalanceEntryForm (spec 028 US2, FR-007…FR-009). The
/// balance <c>&lt;input type="number"&gt;</c> serialises its value invariant; under de-DE / fr-FR
/// the old <c>NumberStyles.Number</c> + <c>CultureInfo.CurrentCulture</c> parse read the period
/// as a group separator, so <c>1234.50</c> was queued as <c>123450</c>. After T035 the amount is
/// parsed through <c>MoneyInput.Parse</c> (invariant); the request handed to <c>OnSubmit</c>
/// carries exactly the typed value in every region (FR-008 — same as every other money field).
/// </summary>
public class OpeningBalanceEntryFormLocaleTests : LocalizedTestContext
{
    private readonly IOpeningBalanceService _openingBalanceService = Substitute.For<IOpeningBalanceService>();

    private static readonly Account BankAccount = new()
    {
        Id = Guid.NewGuid(), Name = "Bank", AccountNumber = "1200", Type = AccountType.Asset
    };

    public OpeningBalanceEntryFormLocaleTests()
    {
        Services.AddSingleton(_openingBalanceService);
        _openingBalanceService.ComputePlug(Arg.Any<IReadOnlyList<OpeningBalanceEntry>>(), Arg.Any<IReadOnlyList<Account>>())
            .Returns(ci => ci.ArgAt<IReadOnlyList<OpeningBalanceEntry>>(0).Sum(e => e.Amount));
    }

    private async Task<decimal> QueuedAmountUnderCultureAsync(string cultureName, string typed)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        try
        {
            RecordOpeningBalancesRequest? submitted = null;
            var cut = Render<OpeningBalanceEntryForm>(p => p
                .Add(x => x.Accounts, new[] { BankAccount })
                .Add(x => x.AsAtDate, new DateTime(2026, 7, 1))
                .Add(x => x.OnSubmit, EventCallback.Factory.Create<RecordOpeningBalancesRequest>(this, r => submitted = r)));

            cut.Find(".opening-balance-amount").Change(typed);
            await cut.Find(".wizard-post").ClickAsync(new MouseEventArgs());

            return submitted!.Entries.Single().Amount;
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public async Task Should_QueueExactlyTheTypedAmount_When_EnteredUnderACommaDecimalRegion(string cultureName)
    {
        Assert.Equal(1234.50m, await QueuedAmountUnderCultureAsync(cultureName, "1234.50"));
        Assert.Equal(1.50m, await QueuedAmountUnderCultureAsync(cultureName, "1.50"));
        Assert.Equal(-150.00m, await QueuedAmountUnderCultureAsync(cultureName, "-150.00"));
    }

    [Fact]
    public async Task Should_InterpretTheAmountIdentically_UnderGermanFrenchAndAustralianRegions()
    {
        var au = await QueuedAmountUnderCultureAsync("en-AU", "1234.50");
        var de = await QueuedAmountUnderCultureAsync("de-DE", "1234.50");
        var fr = await QueuedAmountUnderCultureAsync("fr-FR", "1234.50");

        Assert.Equal(1234.50m, au);
        Assert.Equal(au, de);
        Assert.Equal(au, fr);
    }
}
