using System.Globalization;
using Bunit;
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
/// bUnit locale-safety tests for JournalEntryPage (spec 028 US2, FR-007…FR-009). An
/// <c>&lt;input type="number"&gt;</c> serialises its value invariant ("1.50"); under a
/// comma-decimal region (fr-FR / de-DE) the old <c>NumberStyles.Number</c> +
/// <c>CultureInfo.CurrentCulture</c> parse read the period as a group separator and posted
/// <c>1.50</c> as <c>150</c>. After T034 the amount is parsed through <c>MoneyInput.Parse</c>
/// (invariant) so the running total equals exactly what was typed, in every region. The
/// rendered totals row is the observable proxy for the summed <c>line.Debit</c> / <c>line.Credit</c>.
/// </summary>
public class JournalEntryPageLocaleTests : LocalizedTestContext
{
    private readonly IGeneralJournalService _journalService = Substitute.For<IGeneralJournalService>();

    public JournalEntryPageLocaleTests()
    {
        Services.AddSingleton(_journalService);
        _journalService.GetJournalAccountsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                new()
                {
                    Id = Guid.NewGuid(), Name = "Hall Hire", Type = AccountType.Expense,
                    AccountNumber = "6000", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            });
    }

    private void UnderCulture(string cultureName, Action body)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        MoneyFormatter.Configure(CurrencyCatalog.Default);
        try { body(); }
        finally
        {
            CultureInfo.CurrentCulture = original;
            MoneyFormatter.Configure(CurrencyCatalog.Default);
        }
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    public void Should_PostExactlyTheTypedDebit_When_EnteredUnderACommaDecimalRegion(string cultureName)
        => UnderCulture(cultureName, () =>
        {
            var cut = Render<JournalEntryPage>();

            cut.FindAll(".journal-debit")[0].Change("1.50");

            // line.Debit must be 1.50m — never 150m (period misread as a thousands separator).
            Assert.Equal(MoneyFormatter.Format(1.50m), cut.Find(".journal-total-debits").TextContent);
            Assert.NotEqual(MoneyFormatter.Format(150m), cut.Find(".journal-total-debits").TextContent);
        });

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    public void Should_PostExactlyTheTypedCredit_When_AmountHasAGroupingLengthIntegerPart(string cultureName)
        => UnderCulture(cultureName, () =>
        {
            var cut = Render<JournalEntryPage>();

            cut.FindAll(".journal-credit")[0].Change("1234.50");

            Assert.Equal(MoneyFormatter.Format(1234.50m), cut.Find(".journal-total-credits").TextContent);
            Assert.NotEqual(MoneyFormatter.Format(123450m), cut.Find(".journal-total-credits").TextContent);
        });

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    public void Should_StoreOneAndAHalf_When_TypedAsThePlainDecimalOnePointFive(string cultureName)
        => UnderCulture(cultureName, () =>
        {
            var cut = Render<JournalEntryPage>();

            cut.FindAll(".journal-debit")[0].Change("1.5");

            Assert.Equal(MoneyFormatter.Format(1.5m), cut.Find(".journal-total-debits").TextContent);
            Assert.NotEqual(MoneyFormatter.Format(15m), cut.Find(".journal-total-debits").TextContent);
        });
}
